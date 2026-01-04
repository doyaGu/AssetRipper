using System.Diagnostics;
using System.Globalization;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Constants;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Relations;

/// <summary>
/// Emits relations/asset_dependencies.ndjson aligned with the v2 schema.
/// Resolves Unity PPtr references into AssetPK pairs enriched with edge metadata.
/// </summary>
public sealed class AssetDependencyExporter
{
    private static readonly TimeSpan DependencyNoProgressTimeout = TimeSpan.FromSeconds(15);
    private const long DependencyStallIterationThreshold = 50_000;
    private const long NullPointerAbortThreshold = 100_000;
    private const int RepeatedPointerBreakThreshold = 256;

    private readonly Options _options;
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly CompressionKind _compressionKind;
    private readonly bool _enableIndex;
    
    // Dependency resolution cache to improve performance
    private readonly Dictionary<(string collectionId, long pathId), bool> _assetExistenceCache = new();

    public AssetDependencyExporter(Options options, CompressionKind compressionKind, bool enableIndex)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonSettings = JsonSettingsFactory.CreateDefault();
        _compressionKind = compressionKind;
        _enableIndex = enableIndex;
    }

    /// <summary>
    /// Exports asset dependency relations from the game data.
    /// </summary>
    public DomainExportResult Export(GameData gameData)
    {
        if (gameData is null)
        {
            throw new ArgumentNullException(nameof(gameData));
        }

        Logger.Info(LogCategory.Export, "Exporting asset dependency relations...");
        Directory.CreateDirectory(_options.OutputPath);

        DependencyExportContext context = InitializeExportContext(gameData);

        try
        {
            ProcessAllCollections(context);
        }
        finally
        {
            context.Writer.Dispose();
        }

        FinalizeExportResult(context);

        Logger.Info(LogCategory.Export,
            $"Exported {context.EmittedCount} dependency relations (skipped {context.SkippedCount}) across {context.Writer.ShardCount} shards");

        return context.Result;
    }

    /// <summary>
    /// Initializes the export context with all necessary data structures and configurations.
    /// </summary>
    private DependencyExportContext InitializeExportContext(GameData gameData)
    {
        Logger.Info(LogCategory.Export, "Collecting asset collections for dependency export...");
        List<AssetCollection> collections = gameData.GameBundle.FetchAssetCollections().ToList();
        Logger.Info(LogCategory.Export, $"Collected {collections.Count} collections. Building lookup...");

        CollectionLookup collectionLookup = CollectionLookup.Build(collections);
        Logger.Info(LogCategory.Export, "Collection lookup ready. Beginning dependency resolution...");
        Logger.Info(LogCategory.Export, $"Resolving dependencies across {collections.Count} collections");

        long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : ExportConstants.DefaultMaxRecordsPerShard;
        long maxBytesPerShard = ExportConstants.DefaultMaxBytesPerShard;

        DomainExportResult result = new DomainExportResult(
            domain: "asset_dependencies",
            tableId: "relations/asset_dependencies",
            schemaPath: "Schemas/v2/relations/asset_dependencies.schema.json");

        ShardedNdjsonWriter writer = new ShardedNdjsonWriter(
            _options.OutputPath,
            result.ShardDirectory,
            _jsonSettings,
            maxRecordsPerShard,
            maxBytesPerShard,
            _compressionKind,
            seekableFrameSize: ExportConstants.DefaultSeekableFrameSize,
            collectIndexEntries: _enableIndex,
            descriptorDomain: result.TableId);

        return new DependencyExportContext(collections, collectionLookup, result, writer);
    }

    /// <summary>
    /// Processes all asset collections and their dependencies.
    /// </summary>
    private void ProcessAllCollections(DependencyExportContext context)
    {
        foreach (AssetCollection collection in context.Collections)
        {
            ProcessCollection(context, collection);
        }
    }

    /// <summary>
    /// Processes a single asset collection.
    /// </summary>
    private void ProcessCollection(DependencyExportContext context, AssetCollection collection)
    {
        string ownerCollectionId = context.CollectionLookup.GetCollectionId(collection);
        string collectionDisplayName = string.IsNullOrWhiteSpace(collection.Name) ? "<unnamed>" : collection.Name;
        int assetTotal = collection.Assets.Count;
        int dependencySlotCount = collection.Dependencies.Count;

        if (_options.Verbose)
        {
            Logger.Info(LogCategory.Export,
                $"[{ownerCollectionId}] Processing collection '{collectionDisplayName}' with {assetTotal} assets and {dependencySlotCount} dependency slots");
        }

        long collectionEmittedBefore = context.EmittedCount;
        long collectionSkippedBefore = context.SkippedCount;

        IEnumerable<IUnityObjectBase> orderedAssets = collection.Assets.Values.OrderBy(static asset => asset.PathID);
        int assetIndex = 0;

        foreach (IUnityObjectBase asset in orderedAssets)
        {
            assetIndex++;
            ProcessAsset(context, collection, ownerCollectionId, asset, assetIndex, assetTotal);
        }

        if (_options.Verbose)
        {
            long collectionEmittedDelta = context.EmittedCount - collectionEmittedBefore;
            long collectionSkippedDelta = context.SkippedCount - collectionSkippedBefore;
            Logger.Info(LogCategory.Export,
                $"[{ownerCollectionId}] Completed '{collectionDisplayName}' - emitted {collectionEmittedDelta}, skipped {collectionSkippedDelta} relations");
        }
    }

    /// <summary>
    /// Processes dependencies for a single asset.
    /// </summary>
    private void ProcessAsset(
        DependencyExportContext context,
        AssetCollection collection,
        string ownerCollectionId,
        IUnityObjectBase asset,
        int assetIndex,
        int assetTotal)
    {
        context.PerAssetEdges.Clear();
        long assetEmittedBefore = context.EmittedCount;
        long assetSkippedBefore = context.SkippedCount;

        AssetProcessingState state = new AssetProcessingState(ownerCollectionId, assetIndex, assetTotal, asset.PathID);

        if (_options.TraceDependencies)
        {
            Logger.Info(LogCategory.Export,
                $"[{ownerCollectionId}] Starting asset {assetIndex}/{assetTotal} (pathID {asset.PathID})");
        }

        IEnumerable<(string field, PPtr pptr)>? dependencies = FetchAssetDependencies(
            context, collection, asset, ownerCollectionId, assetIndex, assetTotal);

        if (dependencies is null)
        {
            return;
        }

        ProcessAssetDependencies(context, collection, ownerCollectionId, asset, dependencies, state);

        LogAssetCompletionIfNeeded(state, assetEmittedBefore, assetSkippedBefore, context.EmittedCount, context.SkippedCount);
    }

    /// <summary>
    /// Fetches dependencies for an asset with error handling and logging.
    /// </summary>
    private IEnumerable<(string field, PPtr pptr)>? FetchAssetDependencies(
        DependencyExportContext context,
        AssetCollection collection,
        IUnityObjectBase asset,
        string ownerCollectionId,
        int assetIndex,
        int assetTotal)
    {
        try
        {
            Stopwatch fetchTimer = Stopwatch.StartNew();
            IEnumerable<(string field, PPtr pptr)>? dependencies = asset.FetchDependencies();
            fetchTimer.Stop();

            if (_options.TraceDependencies && fetchTimer.ElapsedMilliseconds > 250)
            {
                Logger.Info(LogCategory.Export,
                    $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) FetchDependencies completed in {fetchTimer.ElapsedMilliseconds} ms");
            }

            return dependencies;
        }
        catch (Exception ex)
        {
            Logger.Warning(LogCategory.Export,
                $"Failed to fetch dependencies for asset {asset.PathID} in {collection.Name}: {ex.Message}");
            context.SkippedCount++;
            return null;
        }
    }

    /// <summary>
    /// Processes all dependencies for a single asset.
    /// </summary>
    private void ProcessAssetDependencies(
        DependencyExportContext context,
        AssetCollection collection,
        string ownerCollectionId,
        IUnityObjectBase asset,
        IEnumerable<(string field, PPtr pptr)> dependencies,
        AssetProcessingState state)
    {
        foreach ((string field, PPtr pointer) in dependencies)
        {
            state.EnumeratedDependencies++;
            state.UpdatePointerSnapshot(pointer, field);

            PointerSignature pointerKey = PointerSignature.From(field, pointer);

            if (IsNullPointer(pointer))
            {
                if (HandleNullPointer(context, state, pointerKey))
                {
                    break; // Abort enumeration
                }
                continue;
            }

            state.ConsecutiveNullPointers = 0;
            state.EnumeratedSinceLastProgress++;

            if (ProcessSingleDependency(context, collection, ownerCollectionId, asset, field, pointer, pointerKey, state))
            {
                break; // Abort enumeration
            }
        }
    }

    /// <summary>
    /// Processes a single dependency pointer.
    /// </summary>
    private bool ProcessSingleDependency(
        DependencyExportContext context,
        AssetCollection collection,
        string ownerCollectionId,
        IUnityObjectBase asset,
        string field,
        PPtr pointer,
        PointerSignature pointerKey,
        AssetProcessingState state)
    {
        DependencyResolutionContext? resolutionContext = ResolveDependency(
            context.CollectionLookup, collection, ownerCollectionId, asset, field ?? string.Empty, pointer);

        if (resolutionContext is null || ShouldSkip(resolutionContext))
        {
            context.SkippedCount++;
            int repeatCount = IncrementPointerRepeat(state.PointerRepeatCounts, pointerKey);

            if (state.ReportNoProgressIfNeeded(_options.TraceDependencies))
            {
                return true;
            }

            if (state.ShouldAbortDueToRepeat(pointerKey, repeatCount))
            {
                return true;
            }

            return false;
        }

        DependencyKey perAssetKey = DependencyKey.FromRelation(resolutionContext.Relation);
        if (!context.PerAssetEdges.Add(perAssetKey))
        {
            context.SkippedCount++;
            int repeatCount = IncrementPointerRepeat(state.PointerRepeatCounts, pointerKey);

            if (state.ReportNoProgressIfNeeded(_options.TraceDependencies))
            {
                return true;
            }

            if (state.ShouldAbortDueToRepeat(pointerKey, repeatCount))
            {
                return true;
            }

            return false;
        }

        EmitDependencyRecord(context, resolutionContext, state, pointerKey);

        LogDependencyProgressIfNeeded(state, pointer, field);

        return state.ReportNoProgressIfNeeded(_options.TraceDependencies);
    }

    /// <summary>
    /// Emits a dependency record to the writer.
    /// </summary>
    private void EmitDependencyRecord(
        DependencyExportContext context,
        DependencyResolutionContext resolutionContext,
        AssetProcessingState state,
        PointerSignature pointerKey)
    {
        state.PointerRepeatCounts.Remove(pointerKey);
        state.PointerRepeatLogged?.Remove(pointerKey);

        string stableKey = BuildStableKey(resolutionContext.Relation);
        string? indexKey = _enableIndex ? stableKey : null;

        context.Writer.WriteRecord(resolutionContext.Relation, stableKey, indexKey);
        context.EmittedCount++;

        state.EnumeratedSinceLastProgress = 0;
        state.NoProgressTimer.Restart();
    }

    /// <summary>
    /// Checks if a pointer is effectively null.
    /// </summary>
    private static bool IsNullPointer(PPtr pointer)
    {
        return pointer.PathID == 0 && pointer.FileID == 0;
    }

    /// <summary>
    /// Handles null pointer logic. Returns true if enumeration should abort.
    /// </summary>
    private bool HandleNullPointer(DependencyExportContext context, AssetProcessingState state, PointerSignature pointerKey)
    {
        context.SkippedCount++;
        state.ConsecutiveNullPointers++;

        int repeatCount = IncrementPointerRepeat(state.PointerRepeatCounts, pointerKey);
        if (state.ShouldAbortDueToRepeat(pointerKey, repeatCount))
        {
            return true;
        }

        if (state.ConsecutiveNullPointers >= NullPointerAbortThreshold)
        {
            string pointerDetails = state.HasPointerSnapshot
                ? $"fileID={state.LastPointer.FileID}, pathID={state.LastPointer.PathID}"
                : "<unavailable>";

            Logger.Warning(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) aborting dependency enumeration after {state.ConsecutiveNullPointers:N0} consecutive null references (last pointer {pointerDetails}, field='{state.LastField ?? "<null>"}')");
            return true;
        }

        state.EnumeratedSinceLastProgress = 0;
        state.NoProgressTimer.Restart();
        return false;
    }

    /// <summary>
    /// Logs dependency processing progress if needed.
    /// </summary>
    /// <param name="state">The asset processing state.</param>
    /// <param name="pointer">The current PPtr being processed.</param>
    /// <param name="field">The field name for the dependency (may be null).</param>
    private void LogDependencyProgressIfNeeded(AssetProcessingState state, PPtr pointer, string? field)
    {
        if (!_options.TraceDependencies)
        {
            return;
        }

        if (state.EnumeratedDependencies % ExportConstants.DependencyProgressLogInterval == 0)
        {
            Logger.Info(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) enumerated {state.EnumeratedDependencies} dependencies so far");
        }

        if (state.DependencyProgressTimer.ElapsedMilliseconds >= 5_000)
        {
            state.DependencyProgressTimer.Restart();
            Logger.Info(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) still processing - enumerated {state.EnumeratedDependencies} dependencies so far (last pointer fileID={pointer.FileID}, pathID={pointer.PathID}, field='{field ?? "<null>"}')");
        }
    }

    /// <summary>
    /// Logs asset completion metrics if needed.
    /// </summary>
    private void LogAssetCompletionIfNeeded(
        AssetProcessingState state,
        long assetEmittedBefore,
        long assetSkippedBefore,
        long currentEmittedCount,
        long currentSkippedCount)
    {
        if (!_options.TraceDependencies)
        {
            return;
        }

        if (state.AssetIndex % 500 == 0 || state.EnumeratedDependencies > 2000)
        {
            long assetEmittedDelta = currentEmittedCount - assetEmittedBefore;
            long assetSkippedDelta = currentSkippedCount - assetSkippedBefore;
            Logger.Info(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) processed {state.EnumeratedDependencies} dependencies => emitted {assetEmittedDelta}, skipped {assetSkippedDelta}");
        }

        state.AssetStopwatch.Stop();
        state.DependencyProgressTimer.Stop();
        state.NoProgressTimer.Stop();

        if (state.AssetStopwatch.ElapsedMilliseconds > 1_000)
        {
            Logger.Info(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) completed in {state.AssetStopwatch.ElapsedMilliseconds} ms");
        }

        if (state.EnumeratedDependencies > 0 && state.EnumeratedDependencies < ExportConstants.DependencyProgressLogInterval)
        {
            long assetEmittedDelta = currentEmittedCount - assetEmittedBefore;
            long assetSkippedDelta = currentSkippedCount - assetSkippedBefore;
            Logger.Info(LogCategory.Export,
                $"[{state.OwnerCollectionId}] Asset {state.AssetIndex}/{state.AssetTotal} (pathID {state.AssetPathId}) processed {state.EnumeratedDependencies} dependencies => emitted {assetEmittedDelta}, skipped {assetSkippedDelta}");
        }
    }

    /// <summary>
    /// Finalizes the export result with shard and index information.
    /// </summary>
    private void FinalizeExportResult(DependencyExportContext context)
    {
        context.Result.Shards.AddRange(context.Writer.ShardDescriptors);
        if (_enableIndex)
        {
            context.Result.IndexEntries.AddRange(context.Writer.IndexEntries);
        }
    }

    private DependencyResolutionContext? ResolveDependency(
        CollectionLookup collectionLookup,
        AssetCollection ownerCollection,
        string ownerCollectionId,
        IUnityObjectBase owner,
        string fieldName,
        PPtr pointer)
    {
        AssetPrimaryKey from = new AssetPrimaryKey
        {
            CollectionId = ownerCollectionId,
            PathId = owner.PathID
        };

        string? sanitizedField = string.IsNullOrWhiteSpace(fieldName) ? null : fieldName;

        AssetCollection? targetCollection = null;
        string? targetCollectionName = null;
        string targetCollectionId = FileConstants.MissingCollectionId;
        bool collectionResolved = false;
        bool assetResolved = false;
        bool targetIsBuiltin = false;
        string? notes = null;

        long targetPathId = pointer.PathID;

        if (pointer.FileID == 0)
        {
            targetCollection = ownerCollection;
            targetCollectionName = ownerCollection.Name;
            collectionResolved = true;
            targetCollectionId = ownerCollectionId;
            assetResolved = ownerCollection.TryGetAsset(targetPathId, out _);
            if (!assetResolved)
            {
                notes = string.Format(
                    CultureInfo.InvariantCulture,
                    "Target asset pathID {0} not found in owning collection",
                    targetPathId);
            }

            targetIsBuiltin = collectionLookup.IsBuiltinCollectionId(targetCollectionId);
        }
        else if (pointer.FileID > 0)
        {
            IReadOnlyList<AssetCollection?> dependencies = ownerCollection.Dependencies;
            FileIdentifier identifier = default;
            bool hasIdentifier = TryGetDependencyIdentifier(ownerCollection, pointer.FileID, out identifier);
            string? dependencyName = hasIdentifier ? identifier.GetFilePath() : null;

            if (pointer.FileID < dependencies.Count)
            {
                targetCollection = dependencies[pointer.FileID];
                if (targetCollection is not null)
                {
                    targetCollectionName = targetCollection.Name;
                    collectionResolved = true;
                    targetCollectionId = collectionLookup.GetCollectionId(targetCollection);
                    assetResolved = targetCollection.TryGetAsset(targetPathId, out _);
                    if (!assetResolved)
                    {
                        notes = FormatMissingDependencyNote(pointer.FileID, targetPathId, dependencyName, "asset not found in dependency");
                    }

                    targetIsBuiltin = collectionLookup.IsBuiltinCollectionId(targetCollectionId);
                }
            }

            if (!collectionResolved && hasIdentifier && collectionLookup.TryResolve(identifier, out CollectionMatch match))
            {
                targetCollection = match.Collection;
                targetCollectionName = targetCollection.Name;
                collectionResolved = true;
                targetCollectionId = match.CollectionId;
                assetResolved = targetCollection.TryGetAsset(targetPathId, out _);
                if (!assetResolved)
                {
                    notes ??= FormatMissingDependencyNote(pointer.FileID, targetPathId, dependencyName, "asset not found in resolved collection");
                }

                targetIsBuiltin = match.IsBuiltin;
            }

            if (!collectionResolved && collectionLookup.TryResolveBuiltinByName(dependencyName, out CollectionMatch builtinMatch))
            {
                targetCollection = builtinMatch.Collection;
                targetCollectionName = targetCollection.Name;
                collectionResolved = true;
                targetCollectionId = builtinMatch.CollectionId;
                assetResolved = targetCollection.TryGetAsset(targetPathId, out _);
                if (!assetResolved)
                {
                    notes ??= FormatMissingDependencyNote(pointer.FileID, targetPathId, dependencyName, "asset not found in built-in dependency");
                }

                targetIsBuiltin = true;
            }

            if (!collectionResolved)
            {
                string? normalized = dependencyName is null
                    ? null
                    : SpecialFileNames.FixFileIdentifier(dependencyName).Replace('\\', '/').Trim();

                if (!string.IsNullOrEmpty(normalized) && collectionLookup.IsBuiltinAlias(normalized))
                {
                    targetIsBuiltin = true;
                    notes ??= FormatMissingDependencyNote(pointer.FileID, targetPathId, dependencyName, "built-in dependency not present in bundle");
                }
                else
                {
                    notes ??= FormatMissingDependencyNote(pointer.FileID, targetPathId, dependencyName, string.Format(
                        CultureInfo.InvariantCulture,
                        "dependency slot unresolved (max index {0})",
                        Math.Max(0, dependencies.Count - 1)));
                }
            }
        }
        else
        {
            string? builtinName = DescribeBuiltinFromFileId(pointer.FileID);
            if (collectionLookup.TryResolveBuiltinByFileId(pointer.FileID, out CollectionMatch builtinMatch))
            {
                targetCollection = builtinMatch.Collection;
                targetCollectionName = targetCollection.Name;
                collectionResolved = true;
                targetCollectionId = builtinMatch.CollectionId;
                assetResolved = targetCollection.TryGetAsset(targetPathId, out _);
                if (!assetResolved)
                {
                    notes = FormatMissingDependencyNote(pointer.FileID, targetPathId, builtinName ?? targetCollectionName, "asset not found in built-in collection");
                }

                targetIsBuiltin = true;
            }
            else
            {
                targetIsBuiltin = builtinName is not null;
                if (targetIsBuiltin)
                {
                    notes = FormatMissingDependencyNote(pointer.FileID, targetPathId, builtinName, "built-in reference could not be resolved");
                }
                else
                {
                    notes = FormatMissingDependencyNote(pointer.FileID, targetPathId, null, "negative fileID unsupported");
                }
            }
        }

        string resolvedCollectionId = collectionResolved ? targetCollectionId : FileConstants.MissingCollectionId;

        // Determine dependency kind based on FileID and field path
        string kind = DetermineKind(pointer.FileID, fieldName);
        
        // Extract array index if present in field path (e.g., "m_Materials[2]" -> 2)
        int? arrayIndex = ExtractArrayIndex(fieldName);
        
        // Extract field type if available (would need asset type reflection - placeholder for now)
        string? fieldType = null; // TODO: Implement via reflection on asset type
        
        // Determine if field is nullable based on PPtr characteristics
        bool? isNullable = pointer.PathID == 0 ? true : (bool?)null;

        AssetDependencyRecord relation = new AssetDependencyRecord
        {
            From = from,
            To = new AssetPrimaryKey
            {
                CollectionId = resolvedCollectionId,
                PathId = targetPathId
            },
            Edge = new AssetDependencyEdge
            {
                Kind = kind,
                Field = fieldName, // Use original field name (required by schema)
                FieldType = fieldType,
                FileId = pointer.FileID,
                ArrayIndex = arrayIndex,
                IsNullable = isNullable,
                Optional = null // Legacy field, kept for backward compatibility
            },
            Status = DetermineStatus(ownerCollectionId, resolvedCollectionId, owner.PathID, targetPathId, collectionResolved, assetResolved, pointer),
            TargetType = null, // TODO: Extract from PPtr generic parameter via reflection
            Notes = notes
        };

        return new DependencyResolutionContext(relation, targetCollectionName, targetIsBuiltin);
    }

    /// <summary>
    /// Determines the resolution status of a dependency based on various resolution criteria.
    /// </summary>
    /// <param name="ownerCollectionId">The collection ID of the owning asset.</param>
    /// <param name="targetCollectionId">The collection ID of the target asset.</param>
    /// <param name="ownerPathId">The path ID of the owning asset.</param>
    /// <param name="targetPathId">The path ID of the target asset.</param>
    /// <param name="collectionResolved">Whether the target collection was successfully resolved.</param>
    /// <param name="assetResolved">Whether the target asset exists in the resolved collection.</param>
    /// <param name="pointer">The PPtr being resolved.</param>
    /// <returns>Status string: "Null", "SelfReference", "InvalidFileID", "Missing", "Resolved", or "External".</returns>
    private static string DetermineStatus(
        string ownerCollectionId,
        string targetCollectionId,
        long ownerPathId,
        long targetPathId,
        bool collectionResolved,
        bool assetResolved,
        PPtr pointer)
    {
        // Check for null reference (PathID == 0)
        if (pointer.PathID == 0)
        {
            return "Null";
        }

        // Check for self-reference
        if (string.Equals(ownerCollectionId, targetCollectionId, StringComparison.Ordinal) && ownerPathId == targetPathId)
        {
            return "SelfReference";
        }

        // Check for invalid FileID (exceeds dependency list bounds)
        if (pointer.FileID > 0 && !collectionResolved)
        {
            return "InvalidFileID";
        }

        if (!collectionResolved)
        {
            return "Missing";
        }

        if (!assetResolved)
        {
            return string.Equals(ownerCollectionId, targetCollectionId, StringComparison.Ordinal) ? "Missing" : "External";
        }

        if (string.Equals(ownerCollectionId, targetCollectionId, StringComparison.Ordinal))
        {
            return "Resolved";
        }

        return collectionResolved ? "External" : "Missing";
    }

    /// <summary>
    /// Determines the kind of dependency based on FileID and field path structure.
    /// </summary>
    /// <param name="fileId">The FileID of the dependency (0=internal, positive=external, negative=built-in).</param>
    /// <param name="fieldPath">The field path that may contain array indices or dictionary indicators.</param>
    /// <returns>A dependency kind string: "array_element", "dictionary_key", "dictionary_value", "internal", "external", or "pptr".</returns>
    private static string DetermineKind(int fileId, string fieldPath)
    {
        // Check if field path contains array indexing (e.g., "m_Materials[2]")
        if (!string.IsNullOrEmpty(fieldPath) && fieldPath.Contains('[') && fieldPath.Contains(']'))
        {
            return "array_element";
        }

        // Check for dictionary patterns (heuristic: contains "Key" or "Value" in field name)
        if (!string.IsNullOrEmpty(fieldPath))
        {
            if (fieldPath.Contains(".Key", StringComparison.OrdinalIgnoreCase))
            {
                return "dictionary_key";
            }
            if (fieldPath.Contains(".Value", StringComparison.OrdinalIgnoreCase))
            {
                return "dictionary_value";
            }
        }

        // Distinguish internal vs external based on FileID
        if (fileId == 0)
        {
            return "internal";
        }
        else if (fileId > 0)
        {
            return "external";
        }

        // Default to standard pptr
        return "pptr";
    }

    /// <summary>
    /// Extracts array index from field path (e.g., "m_Materials[2]" -> 2).
    /// Returns null if no array index present.
    /// </summary>
    /// <param name="fieldPath">The field path that may contain array index syntax like "field[index]".</param>
    /// <returns>The extracted array index if present and valid, otherwise null.</returns>
    private static int? ExtractArrayIndex(string fieldPath)
    {
        if (string.IsNullOrEmpty(fieldPath))
        {
            return null;
        }

        int openBracket = fieldPath.LastIndexOf('[');
        int closeBracket = fieldPath.LastIndexOf(']');

        if (openBracket > 0 && closeBracket > openBracket)
        {
            string indexStr = fieldPath.Substring(openBracket + 1, closeBracket - openBracket - 1);
            if (int.TryParse(indexStr, out int index) && index >= 0)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a dependency should be skipped based on export options.
    /// </summary>
    /// <param name="context">The dependency resolution context containing the relation and metadata.</param>
    /// <returns>True if the dependency should be skipped, false otherwise.</returns>
    private bool ShouldSkip(DependencyResolutionContext context)
    {
        AssetDependencyRecord relation = context.Relation;

        if (_options.SkipSelfRefs && string.Equals(relation.Status, "SelfReference", StringComparison.Ordinal))
        {
            return true;
        }

        if (_options.MinimalDeps && string.Equals(relation.From.CollectionId, relation.To.CollectionId, StringComparison.Ordinal))
        {
            return true;
        }

        if (_options.SkipBuiltinDeps)
        {
            if (context.TargetIsBuiltin)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(context.TargetCollectionName) && IsBuiltinCollectionName(context.TargetCollectionName))
            {
                return true;
            }

            if (IsBuiltinCollectionId(relation.To.CollectionId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines the collection ID for an asset collection, detecting built-in resources.
    /// </summary>
    /// <param name="collection">The asset collection to determine an ID for.</param>
    /// <returns>A collection ID string, either a built-in identifier (e.g., "BUILTIN-EXTRA") or a computed hash.</returns>
    private static string DetermineCollectionId(AssetCollection collection)
    {
        string? name = collection.Name;
        if (!string.IsNullOrEmpty(name))
        {
            string normalized = SpecialFileNames.FixFileIdentifier(name);
            if (SpecialFileNames.IsBuiltinExtra(normalized))
            {
                return "BUILTIN-EXTRA";
            }

            if (SpecialFileNames.IsDefaultResource(normalized))
            {
                return "BUILTIN-DEFAULT";
            }

            if (SpecialFileNames.IsEditorResource(normalized))
            {
                return "BUILTIN-EDITOR";
            }
        }

        return ExportHelper.ComputeCollectionId(collection);
    }

    /// <summary>
    /// Checks if a collection name corresponds to a built-in Unity resource.
    /// </summary>
    /// <param name="collectionName">The collection name to check.</param>
    /// <returns>True if the name matches a built-in resource pattern, false otherwise.</returns>
    private static bool IsBuiltinCollectionName(string collectionName)
    {
        if (string.IsNullOrEmpty(collectionName))
        {
            return false;
        }

        string normalized = SpecialFileNames.FixFileIdentifier(collectionName);
        return SpecialFileNames.IsBuiltinExtra(normalized)
            || SpecialFileNames.IsDefaultResource(normalized)
            || SpecialFileNames.IsEditorResource(normalized);
    }

    /// <summary>
    /// Checks if a collection ID represents a built-in Unity resource.
    /// </summary>
    /// <param name="collectionId">The collection ID to check.</param>
    /// <returns>True if the ID is a known built-in identifier, false otherwise.</returns>
    private static bool IsBuiltinCollectionId(string collectionId)
    {
        return string.Equals(collectionId, "BUILTIN-EXTRA", StringComparison.Ordinal)
            || string.Equals(collectionId, "BUILTIN-DEFAULT", StringComparison.Ordinal)
            || string.Equals(collectionId, "BUILTIN-EDITOR", StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to retrieve the FileIdentifier for a dependency at a given file index.
    /// </summary>
    /// <param name="collection">The owning collection containing the dependencies list.</param>
    /// <param name="fileIndex">The file index (FileID) to look up.</param>
    /// <param name="identifier">Output parameter containing the FileIdentifier if found.</param>
    /// <returns>True if the identifier was successfully retrieved, false otherwise.</returns>
    private static bool TryGetDependencyIdentifier(AssetCollection collection, int fileIndex, out FileIdentifier identifier)
    {
        identifier = default;
        if (fileIndex <= 0)
        {
            return false;
        }

        IReadOnlyList<AssetCollection?> dependencies = collection.Dependencies;
        if (fileIndex >= dependencies.Count)
        {
            return false;
        }

        AssetCollection? dependency = dependencies[fileIndex];
        if (dependency is null || string.IsNullOrWhiteSpace(dependency.Name))
        {
            return false;
        }

        identifier = new FileIdentifier
        {
            PathName = SpecialFileNames.FixFileIdentifier(dependency.Name),
            PathNameOrigin = dependency.Name,
            AssetPath = string.Empty,
            Type = AssetType.Serialized,
            Guid = default
        };
        return true;
    }

    /// <summary>
    /// Formats a diagnostic note message for an unresolved dependency.
    /// </summary>
    /// <param name="fileId">The FileID of the unresolved dependency.</param>
    /// <param name="pathId">The PathID of the unresolved dependency.</param>
    /// <param name="dependencyName">Optional name of the dependency file.</param>
    /// <param name="reason">The reason why the dependency couldn't be resolved.</param>
    /// <returns>A formatted diagnostic message string.</returns>
    private static string FormatMissingDependencyNote(int fileId, long pathId, string? dependencyName, string reason)
    {
        string baseMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Unresolved dependency: fileID={0}, pathID={1} ({2})",
            fileId,
            pathId,
            reason);

        if (string.IsNullOrWhiteSpace(dependencyName))
        {
            return baseMessage;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} [dependency={1}]",
            baseMessage,
            dependencyName);
    }

    /// <summary>
    /// Maps negative FileID values to their corresponding built-in resource names.
    /// </summary>
    /// <param name="fileId">The FileID to describe. Negative values represent built-in resources.</param>
    /// <returns>The built-in resource name, or null if the FileID is not a recognized built-in.</returns>
    private static string? DescribeBuiltinFromFileId(int fileId)
    {
        return fileId switch
        {
            -1 => SpecialFileNames.BuiltinExtraName2,
            -2 => SpecialFileNames.DefaultResourceName2,
            -3 => SpecialFileNames.EditorResourceName,
            _ => null
        };
    }

    /// <summary>
    /// Builds a stable, deterministic key for a dependency relation used for deduplication and indexing.
    /// </summary>
    /// <param name="relation">The dependency relation to generate a key for.</param>
    /// <returns>A stable string key in format "fromCollection:fromPath->toCollection:toPath:field".</returns>
    private static string BuildStableKey(AssetDependencyRecord relation)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}->{2}:{3}:{4}",
            relation.From.CollectionId,
            relation.From.PathId,
            relation.To.CollectionId,
            relation.To.PathId,
            relation.Edge.Field ?? string.Empty);
    }

    /// <summary>
    /// Increments and returns the repeat count for a pointer signature.
    /// Used to detect infinite loops caused by repeated pointer enumeration.
    /// </summary>
    /// <param name="repeatCounts">Dictionary tracking repeat counts per pointer signature.</param>
    /// <param name="key">The pointer signature to increment.</param>
    /// <returns>The updated repeat count for this pointer signature.</returns>
    private static int IncrementPointerRepeat(Dictionary<PointerSignature, int> repeatCounts, PointerSignature key)
    {
        if (repeatCounts.TryGetValue(key, out int existing))
        {
            existing++;
        }
        else
        {
            existing = 1;
        }

        repeatCounts[key] = existing;
        return existing;
    }

    private readonly struct PointerSignature : IEquatable<PointerSignature>
    {
        private PointerSignature(int fileId, long pathId, string field)
        {
            FileId = fileId;
            PathId = pathId;
            Field = field;
        }

        public int FileId { get; }
        public long PathId { get; }
        public string Field { get; }

        public static PointerSignature From(string? fieldName, PPtr pointer)
        {
            return new PointerSignature(pointer.FileID, pointer.PathID, fieldName ?? string.Empty);
        }

        public bool Equals(PointerSignature other)
        {
            return FileId == other.FileId
                && PathId == other.PathId
                && string.Equals(Field, other.Field, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is PointerSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(FileId);
            hash.Add(PathId);
            hash.Add(Field, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    private readonly struct DependencyKey
    {
        private DependencyKey(string targetCollectionId, long targetPathId, string field)
        {
            TargetCollectionId = targetCollectionId;
            TargetPathId = targetPathId;
            Field = field;
        }

        public string TargetCollectionId { get; }
        public long TargetPathId { get; }
        public string Field { get; }

        public static DependencyKey FromRelation(AssetDependencyRecord relation)
        {
            return new DependencyKey(
                relation.To.CollectionId,
                relation.To.PathId,
                relation.Edge.Field ?? string.Empty);
        }
    }

    private sealed class DependencyKeyComparer : IEqualityComparer<DependencyKey>
    {
        public static DependencyKeyComparer Instance { get; } = new DependencyKeyComparer();

        public bool Equals(DependencyKey x, DependencyKey y)
        {
            return x.TargetPathId == y.TargetPathId
                && string.Equals(x.TargetCollectionId, y.TargetCollectionId, StringComparison.Ordinal)
                && string.Equals(x.Field, y.Field, StringComparison.Ordinal);
        }

        public int GetHashCode(DependencyKey obj)
        {
            HashCode hash = new HashCode();
            hash.Add(obj.TargetCollectionId, StringComparer.Ordinal);
            hash.Add(obj.TargetPathId);
            hash.Add(obj.Field, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    private sealed class CollectionLookup
    {
        private readonly Dictionary<AssetCollection, string> _collectionIds;
        private readonly Dictionary<string, CollectionMatch> _byKey;
        private readonly Dictionary<string, CollectionMatch> _byCollectionId;
        private readonly Dictionary<string, CollectionMatch> _builtinByAlias;

        private CollectionLookup(
            Dictionary<AssetCollection, string> collectionIds,
            Dictionary<string, CollectionMatch> byKey,
            Dictionary<string, CollectionMatch> byCollectionId,
            Dictionary<string, CollectionMatch> builtinByAlias)
        {
            _collectionIds = collectionIds;
            _byKey = byKey;
            _byCollectionId = byCollectionId;
            _builtinByAlias = builtinByAlias;
        }

        public static CollectionLookup Build(IEnumerable<AssetCollection> collections)
        {
            Dictionary<AssetCollection, string> ids = new();
            Dictionary<string, CollectionMatch> byKey = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CollectionMatch> byCollectionId = new(StringComparer.Ordinal);
            Dictionary<string, CollectionMatch> builtinByAlias = new(StringComparer.Ordinal);

            foreach (AssetCollection collection in collections)
            {
                string collectionId = DetermineCollectionId(collection);
                ids[collection] = collectionId;
                bool isBuiltin = IsBuiltinCollectionName(collection.Name);
                CollectionMatch match = new CollectionMatch(collection, collectionId, isBuiltin);

                RegisterCollectionKeys(collection, match, byKey);
                byCollectionId[collectionId] = match;

                if (isBuiltin)
                {
                    foreach (string alias in ExpandBuiltinAliases(collection.Name))
                    {
                        builtinByAlias[alias] = match;
                        byKey.TryAdd(alias, match);
                    }
                }
            }

            return new CollectionLookup(ids, byKey, byCollectionId, builtinByAlias);
        }

        public string GetCollectionId(AssetCollection collection)
        {
            if (_collectionIds.TryGetValue(collection, out string? collectionId) && collectionId is not null)
            {
                return collectionId;
            }

            string computed = DetermineCollectionId(collection);
            _collectionIds[collection] = computed;
            return computed;
        }

        public bool TryResolve(FileIdentifier identifier, out CollectionMatch match)
        {
            if (TryResolve(identifier.GetFilePath(), out match))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(identifier.PathNameOrigin) && TryResolve(identifier.PathNameOrigin, out match))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(identifier.AssetPath) && TryResolve(identifier.AssetPath, out match))
            {
                return true;
            }

            match = default;
            return false;
        }

        public bool TryResolve(string? candidate, out CollectionMatch match)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                match = default;
                return false;
            }

            string normalized = NormalizeKey(candidate);
            if (normalized.Length == 0)
            {
                match = default;
                return false;
            }

            return _byKey.TryGetValue(normalized, out match);
        }

        public bool TryResolveBuiltinByName(string? candidate, out CollectionMatch match)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                match = default;
                return false;
            }

            string normalized = NormalizeKey(candidate);
            if (normalized.Length == 0)
            {
                match = default;
                return false;
            }

            return _builtinByAlias.TryGetValue(normalized, out match);
        }

        public bool TryResolveBuiltinByFileId(int fileId, out CollectionMatch match)
        {
            string? alias = GetBuiltinAliasForFileId(fileId);
            if (alias is null)
            {
                match = default;
                return false;
            }

            return _builtinByAlias.TryGetValue(alias, out match);
        }

        public bool IsBuiltinAlias(string normalizedAlias)
        {
            if (string.IsNullOrEmpty(normalizedAlias))
            {
                return false;
            }

            return _builtinByAlias.ContainsKey(normalizedAlias);
        }

        public bool IsBuiltinCollectionId(string collectionId)
        {
            return _byCollectionId.TryGetValue(collectionId, out CollectionMatch match) && match.IsBuiltin;
        }

        private static void RegisterCollectionKeys(AssetCollection collection, CollectionMatch match, Dictionary<string, CollectionMatch> map)
        {
            foreach (string alias in ExpandAliases(collection.Name))
            {
                map.TryAdd(alias, match);
            }

            foreach (string alias in ExpandAliases(collection.FilePath))
            {
                map.TryAdd(alias, match);
            }

            if (match.IsBuiltin)
            {
                foreach (string alias in ExpandBuiltinAliases(collection.Name))
                {
                    map.TryAdd(alias, match);
                }
            }
        }

        private static IEnumerable<string> ExpandAliases(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string normalized = NormalizeKey(value);
            if (normalized.Length == 0)
            {
                yield break;
            }

            yield return normalized;

            if (normalized.Contains('/'))
            {
                string fileName = Path.GetFileName(normalized);
                if (!string.IsNullOrEmpty(fileName))
                {
                    string fileAlias = NormalizeKey(fileName);
                    if (fileAlias.Length > 0)
                    {
                        yield return fileAlias;
                    }
                }

                string fileStem = Path.GetFileNameWithoutExtension(normalized);
                if (!string.IsNullOrEmpty(fileStem))
                {
                    string stemAlias = NormalizeKey(fileStem);
                    if (stemAlias.Length > 0)
                    {
                        yield return stemAlias;
                    }
                }
            }
        }

        private static IEnumerable<string> ExpandBuiltinAliases(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string normalized = NormalizeKey(value);
            if (normalized.Length == 0)
            {
                yield break;
            }

            yield return normalized;

            string normalizedExtra1 = NormalizeKey(SpecialFileNames.BuiltinExtraName1);
            string normalizedExtra2 = NormalizeKey(SpecialFileNames.BuiltinExtraName2);
            string normalizedDefault1 = NormalizeKey(SpecialFileNames.DefaultResourceName1);
            string normalizedDefault2 = NormalizeKey(SpecialFileNames.DefaultResourceName2);
            string normalizedEditor = NormalizeKey(SpecialFileNames.EditorResourceName);

            if (normalized == normalizedExtra1 || normalized == normalizedExtra2)
            {
                yield return normalizedExtra1;
                yield return normalizedExtra2;
                yield return NormalizeKey("BUILTIN-EXTRA");
            }
            else if (normalized == normalizedDefault1 || normalized == normalizedDefault2)
            {
                yield return normalizedDefault1;
                yield return normalizedDefault2;
                yield return NormalizeKey("BUILTIN-DEFAULT");
            }
            else if (normalized == normalizedEditor)
            {
                yield return normalizedEditor;
                yield return NormalizeKey("BUILTIN-EDITOR");
            }
        }

        private static string NormalizeKey(string value)
        {
            string fixedName = SpecialFileNames.FixFileIdentifier(value);
            return fixedName.Replace('\\', '/').Trim();
        }

        private static string? GetBuiltinAliasForFileId(int fileId)
        {
            return fileId switch
            {
                -1 => NormalizeKey(SpecialFileNames.BuiltinExtraName2),
                -2 => NormalizeKey(SpecialFileNames.DefaultResourceName2),
                -3 => NormalizeKey(SpecialFileNames.EditorResourceName),
                _ => null
            };
        }
    }

    private readonly struct CollectionMatch
    {
        public CollectionMatch(AssetCollection collection, string collectionId, bool isBuiltin)
        {
            Collection = collection;
            CollectionId = collectionId;
            IsBuiltin = isBuiltin;
        }

        public AssetCollection Collection { get; }
        public string CollectionId { get; }
        public bool IsBuiltin { get; }
    }

    private sealed class DependencyResolutionContext
    {
        public DependencyResolutionContext(AssetDependencyRecord relation, string? targetCollectionName, bool targetIsBuiltin)
        {
            Relation = relation;
            TargetCollectionName = targetCollectionName;
            TargetIsBuiltin = targetIsBuiltin;
        }

        public AssetDependencyRecord Relation { get; }
        public string? TargetCollectionName { get; }
        public bool TargetIsBuiltin { get; }
    }

    /// <summary>
    /// Encapsulates the dependency export context and shared state across collection processing.
    /// Named DependencyExportContext to avoid confusion with Orchestration.ExportContext.
    /// </summary>
    private sealed class DependencyExportContext
    {
        public DependencyExportContext(
            List<AssetCollection> collections,
            CollectionLookup collectionLookup,
            DomainExportResult result,
            ShardedNdjsonWriter writer)
        {
            Collections = collections;
            CollectionLookup = collectionLookup;
            Result = result;
            Writer = writer;
            PerAssetEdges = new HashSet<DependencyKey>(DependencyKeyComparer.Instance);
        }

        public List<AssetCollection> Collections { get; }
        public CollectionLookup CollectionLookup { get; }
        public DomainExportResult Result { get; }
        public ShardedNdjsonWriter Writer { get; }
        public HashSet<DependencyKey> PerAssetEdges { get; }
        public long EmittedCount { get; set; }
        public long SkippedCount { get; set; }
    }

    /// <summary>
    /// Tracks processing state for a single asset's dependencies.
    /// </summary>
    private sealed class AssetProcessingState
    {
        public AssetProcessingState(string ownerCollectionId, int assetIndex, int assetTotal, long assetPathId)
        {
            OwnerCollectionId = ownerCollectionId;
            AssetIndex = assetIndex;
            AssetTotal = assetTotal;
            AssetPathId = assetPathId;

            AssetStopwatch = Stopwatch.StartNew();
            DependencyProgressTimer = Stopwatch.StartNew();
            NoProgressTimer = Stopwatch.StartNew();
            PointerRepeatCounts = new Dictionary<PointerSignature, int>();
        }

        public string OwnerCollectionId { get; }
        public int AssetIndex { get; }
        public int AssetTotal { get; }
        public long AssetPathId { get; }

        public long EnumeratedDependencies { get; set; }
        public long EnumeratedSinceLastProgress { get; set; }
        public long ConsecutiveNullPointers { get; set; }
        public int StallWarningCount { get; set; }

        public PPtr LastPointer { get; private set; }
        public string? LastField { get; private set; }
        public bool HasPointerSnapshot { get; private set; }

        public Stopwatch AssetStopwatch { get; }
        public Stopwatch DependencyProgressTimer { get; }
        public Stopwatch NoProgressTimer { get; }

        public Dictionary<PointerSignature, int> PointerRepeatCounts { get; }
        public HashSet<PointerSignature>? PointerRepeatLogged { get; set; }

        public void UpdatePointerSnapshot(PPtr pointer, string? field)
        {
            LastPointer = pointer;
            LastField = field;
            HasPointerSnapshot = true;
        }

        public bool ShouldAbortDueToRepeat(PointerSignature signature, int repeatCount)
        {
            if (repeatCount < RepeatedPointerBreakThreshold)
            {
                return false;
            }

            PointerRepeatLogged ??= new HashSet<PointerSignature>();
            if (PointerRepeatLogged.Add(signature))
            {
                string displayField = string.IsNullOrEmpty(signature.Field) ? "<null>" : signature.Field;
                Logger.Warning(LogCategory.Export,
                    $"[{OwnerCollectionId}] Asset {AssetIndex}/{AssetTotal} (pathID {AssetPathId}) detected repeating pointer fileID={signature.FileId}, pathID={signature.PathId}, field='{displayField}' after {repeatCount} occurrences - terminating dependency enumeration");
            }

            return true;
        }

        public bool ReportNoProgressIfNeeded(bool traceDependencies)
        {
            if (NoProgressTimer.Elapsed < DependencyNoProgressTimeout)
            {
                return false;
            }

            if (EnumeratedSinceLastProgress < DependencyStallIterationThreshold)
            {
                return false;
            }

            if (StallWarningCount < 5)
            {
                string pointerDetails = HasPointerSnapshot
                    ? $"fileID={LastPointer.FileID}, pathID={LastPointer.PathID}"
                    : "<unavailable>";
                Logger.Warning(LogCategory.Export,
                    $"[{OwnerCollectionId}] Asset {AssetIndex}/{AssetTotal} (pathID {AssetPathId}) observed no new dependency edges across {EnumeratedDependencies} entries for {NoProgressTimer.ElapsedMilliseconds} ms (last pointer {pointerDetails}, field='{LastField ?? "<null>"}') - continuing enumeration");
            }
            else if (StallWarningCount == 5)
            {
                Logger.Warning(LogCategory.Export,
                    $"[{OwnerCollectionId}] Asset {AssetIndex}/{AssetTotal} (pathID {AssetPathId}) continuing despite repeated dependency stalls (further warnings suppressed)");
            }

            StallWarningCount++;
            EnumeratedSinceLastProgress = 0;
            NoProgressTimer.Restart();

            if (StallWarningCount >= 6 && LastPointer.FileID == 0 && LastPointer.PathID == 0)
            {
                string pointerDetails = HasPointerSnapshot
                    ? $"fileID={LastPointer.FileID}, pathID={LastPointer.PathID}"
                    : "<unavailable>";
                Logger.Warning(LogCategory.Export,
                    $"[{OwnerCollectionId}] Asset {AssetIndex}/{AssetTotal} (pathID {AssetPathId}) observed repeated stalled null references (last pointer {pointerDetails}, field='{LastField ?? "<null>"}') - continuing enumeration to avoid data loss");
                // Reset the stall counter so we do not emit the same warning indefinitely.
                StallWarningCount = 5;
                LastPointer = default;
                HasPointerSnapshot = false;
                return false;
            }

            return false;
        }
    }
}
