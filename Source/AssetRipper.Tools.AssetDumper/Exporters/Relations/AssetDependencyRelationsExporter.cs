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
using Newtonsoft.Json;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Helpers;
using AssetRipper.Tools.AssetDumper.Writers;

namespace AssetRipper.Tools.AssetDumper.Exporters.Relations;

/// <summary>
/// Emits relations/asset_dependencies.ndjson aligned with the v2 schema.
/// Resolves Unity PPtr references into AssetPK pairs enriched with edge metadata.
/// </summary>
public sealed class AssetDependencyRelationsExporter
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

    public AssetDependencyRelationsExporter(Options options, CompressionKind compressionKind, bool enableIndex)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };
        _compressionKind = compressionKind;
        _enableIndex = enableIndex;
    }

    public DomainExportResult Export(GameData gameData)
    {
        if (gameData is null)
        {
            throw new ArgumentNullException(nameof(gameData));
        }

        Logger.Info(LogCategory.Export, "Exporting asset dependency relations...");
        Directory.CreateDirectory(_options.OutputPath);

        Logger.Info(LogCategory.Export, "Collecting asset collections for dependency export...");
        List<AssetCollection> collections = gameData.GameBundle.FetchAssetCollections().ToList();
        Logger.Info(LogCategory.Export, $"Collected {collections.Count} collections. Building lookup...");
        CollectionLookup collectionLookup = CollectionLookup.Build(collections);
        Logger.Info(LogCategory.Export, "Collection lookup ready. Beginning dependency resolution...");
        Logger.Info(LogCategory.Export, $"Resolving dependencies across {collections.Count} collections");

        long maxRecordsPerShard = _options.ShardSize > 0 ? _options.ShardSize : 100_000;
        long maxBytesPerShard = 100 * 1024 * 1024;

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
            seekableFrameSize: 2 * 1024 * 1024,
            collectIndexEntries: _enableIndex,
            descriptorDomain: result.TableId);

        HashSet<DependencyKey> perAssetEdges = new HashSet<DependencyKey>(DependencyKeyComparer.Instance);

        long emittedCount = 0;
        long skippedCount = 0;
        bool traceDependencies = _options.TraceDependencies;

        try
        {
            foreach (AssetCollection collection in collections)
            {
                string ownerCollectionId = collectionLookup.GetCollectionId(collection);
                string collectionDisplayName = string.IsNullOrWhiteSpace(collection.Name) ? "<unnamed>" : collection.Name;
                int assetTotal = collection.Assets.Count;
                int dependencySlotCount = collection.Dependencies.Count;
                IEnumerable<IUnityObjectBase> orderedAssets = collection.Assets.Values.OrderBy(static asset => asset.PathID);

                if (_options.Verbose)
                {
                    Logger.Info(LogCategory.Export,
                        $"[{ownerCollectionId}] Processing collection '{collectionDisplayName}' with {assetTotal} assets and {dependencySlotCount} dependency slots");
                }

                long collectionEmittedBefore = emittedCount;
                long collectionSkippedBefore = skippedCount;
                int assetIndex = 0;

                foreach (IUnityObjectBase asset in orderedAssets)
                {
                    assetIndex++;
                    perAssetEdges.Clear();
                    long assetEmittedBefore = emittedCount;
                    long assetSkippedBefore = skippedCount;
                    long enumeratedDependencies = 0;
                    Stopwatch assetStopwatch = Stopwatch.StartNew();
                    Stopwatch dependencyProgressTimer = Stopwatch.StartNew();
                    Stopwatch noProgressTimer = Stopwatch.StartNew();
                    long enumeratedSinceLastProgress = 0;
                    PPtr lastPointer = default;
                    string? lastField = null;
                    bool hasPointerSnapshot = false;
                    int stallWarningCount = 0;
                    long consecutiveNullPointers = 0;
                    Dictionary<PointerSignature, int> pointerRepeatCounts = new Dictionary<PointerSignature, int>();
                    HashSet<PointerSignature>? pointerRepeatLogged = null;

                    bool ShouldAbortDueToRepeat(PointerSignature signature, int repeatCount)
                    {
                        if (repeatCount < RepeatedPointerBreakThreshold)
                        {
                            return false;
                        }

                        pointerRepeatLogged ??= new HashSet<PointerSignature>();
                        if (pointerRepeatLogged.Add(signature))
                        {
                            string displayField = string.IsNullOrEmpty(signature.Field) ? "<null>" : signature.Field;
                            Logger.Warning(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) detected repeating pointer fileID={signature.FileId}, pathID={signature.PathId}, field='{displayField}' after {repeatCount} occurrences �?terminating dependency enumeration");
                        }

                        return true;
                    }

                    if (traceDependencies)
                    {
                        Logger.Info(LogCategory.Export,
                            $"[{ownerCollectionId}] Starting asset {assetIndex}/{assetTotal} (pathID {asset.PathID})");
                    }

                    IEnumerable<(string field, PPtr pptr)>? dependencies;
                    try
                    {
                        Stopwatch fetchTimer = Stopwatch.StartNew();
                        dependencies = asset.FetchDependencies();
                        fetchTimer.Stop();
                        if (traceDependencies && fetchTimer.ElapsedMilliseconds > 250)
                        {
                            Logger.Info(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) FetchDependencies completed in {fetchTimer.ElapsedMilliseconds} ms");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(LogCategory.Export, $"Failed to fetch dependencies for asset {asset.PathID} in {collection.Name}: {ex.Message}");
                        skippedCount++;
                        continue;
                    }

                    if (dependencies is null)
                    {
                        continue;
                    }

                    bool ReportNoProgressIfNeeded(long enumeratedSoFar)
                    {
                        if (noProgressTimer.Elapsed < DependencyNoProgressTimeout)
                        {
                            return false;
                        }

                        if (enumeratedSinceLastProgress < DependencyStallIterationThreshold)
                        {
                            return false;
                        }

                        if (stallWarningCount < 5)
                        {
                            string pointerDetails = hasPointerSnapshot
                                ? $"fileID={lastPointer.FileID}, pathID={lastPointer.PathID}"
                                : "<unavailable>";
                            Logger.Warning(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) observed no new dependency edges across {enumeratedSoFar} entries for {noProgressTimer.ElapsedMilliseconds} ms (last pointer {pointerDetails}, field='{lastField ?? "<null>"}') �?continuing enumeration");
                        }
                        else if (stallWarningCount == 5)
                        {
                            Logger.Warning(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) continuing despite repeated dependency stalls (further warnings suppressed)");
                        }

                        stallWarningCount++;
                        enumeratedSinceLastProgress = 0;
                        noProgressTimer.Restart();

                        if (stallWarningCount >= 6 && lastPointer.FileID == 0 && lastPointer.PathID == 0)
                        {
                            string pointerDetails = hasPointerSnapshot
                                ? $"fileID={lastPointer.FileID}, pathID={lastPointer.PathID}"
                                : "<unavailable>";
                            Logger.Warning(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) aborting dependency enumeration after repeated stalled null references (last pointer {pointerDetails}, field='{lastField ?? "<null>"}')");
                            return true;
                        }

                        return false;
                    }

                    foreach ((string field, PPtr pointer) in dependencies)
                    {
                        enumeratedDependencies++;
                        lastPointer = pointer;
                        lastField = field;
                        hasPointerSnapshot = true;

                        PointerSignature pointerKey = PointerSignature.From(field, pointer);
                        bool pointerIsEffectiveNull = pointer.PathID == 0 && pointer.FileID == 0;

                        if (pointerIsEffectiveNull)
                        {
                            skippedCount++;
                            consecutiveNullPointers++;

                            int repeatCount = IncrementPointerRepeat(pointerRepeatCounts, pointerKey);
                            if (ShouldAbortDueToRepeat(pointerKey, repeatCount))
                            {
                                break;
                            }

                            if (consecutiveNullPointers >= NullPointerAbortThreshold)
                            {
                                string pointerDetails = hasPointerSnapshot
                                    ? $"fileID={lastPointer.FileID}, pathID={lastPointer.PathID}"
                                    : "<unavailable>";
                                Logger.Warning(LogCategory.Export,
                                    $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) aborting dependency enumeration after {consecutiveNullPointers:N0} consecutive null references (last pointer {pointerDetails}, field='{lastField ?? "<null>"}')");
                                break;
                            }

                            enumeratedSinceLastProgress = 0;
                            noProgressTimer.Restart();
                            continue;
                        }

                        consecutiveNullPointers = 0;
                        enumeratedSinceLastProgress++;

                        DependencyResolutionContext? context = ResolveDependency(collectionLookup, collection, ownerCollectionId, asset, field ?? string.Empty, pointer);
                        if (context is null)
                        {
                            skippedCount++;
                            int repeatCount = IncrementPointerRepeat(pointerRepeatCounts, pointerKey);
                            if (ReportNoProgressIfNeeded(enumeratedDependencies))
                            {
                                break;
                            }

                            if (ShouldAbortDueToRepeat(pointerKey, repeatCount))
                            {
                                break;
                            }

                            continue;
                        }

                        if (ShouldSkip(context))
                        {
                            skippedCount++;
                            int repeatCount = IncrementPointerRepeat(pointerRepeatCounts, pointerKey);
                            if (ReportNoProgressIfNeeded(enumeratedDependencies))
                            {
                                break;
                            }

                            if (ShouldAbortDueToRepeat(pointerKey, repeatCount))
                            {
                                break;
                            }

                            continue;
                        }

                        DependencyKey perAssetKey = DependencyKey.FromRelation(context.Relation);
                        if (!perAssetEdges.Add(perAssetKey))
                        {
                            skippedCount++;
                            int repeatCount = IncrementPointerRepeat(pointerRepeatCounts, pointerKey);
                            if (ReportNoProgressIfNeeded(enumeratedDependencies))
                            {
                                break;
                            }

                            if (ShouldAbortDueToRepeat(pointerKey, repeatCount))
                            {
                                break;
                            }

                            continue;
                        }

                        pointerRepeatCounts.Remove(pointerKey);
                        pointerRepeatLogged?.Remove(pointerKey);

                        string stableKey = BuildStableKey(context.Relation);
                        string? indexKey = _enableIndex ? stableKey : null;
                        writer.WriteRecord(context.Relation, stableKey, indexKey);
                        emittedCount++;
                        enumeratedSinceLastProgress = 0;
                        noProgressTimer.Restart();

                        if (traceDependencies && enumeratedDependencies % ExportConstants.DependencyProgressLogInterval == 0)
                        {
                            Logger.Info(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) enumerated {enumeratedDependencies} dependencies so far");
                        }

                        if (traceDependencies && dependencyProgressTimer.ElapsedMilliseconds >= 5_000)
                        {
                            dependencyProgressTimer.Restart();
                            Logger.Info(LogCategory.Export,
                                $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) still processing �?enumerated {enumeratedDependencies} dependencies so far (last pointer fileID={pointer.FileID}, pathID={pointer.PathID}, field='{field}')");
                        }

                        if (ReportNoProgressIfNeeded(enumeratedDependencies))
                        {
                            break;
                        }
                    }

                    if (traceDependencies && (assetIndex % 500 == 0 || enumeratedDependencies > 2000))
                    {
                        long assetEmittedDelta = emittedCount - assetEmittedBefore;
                        long assetSkippedDelta = skippedCount - assetSkippedBefore;
                        Logger.Info(LogCategory.Export,
                            $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) processed {enumeratedDependencies} dependencies => emitted {assetEmittedDelta}, skipped {assetSkippedDelta}");
                    }

                    assetStopwatch.Stop();
                    dependencyProgressTimer.Stop();
                    noProgressTimer.Stop();

                    if (traceDependencies && assetStopwatch.ElapsedMilliseconds > 1_000)
                    {
                        Logger.Info(LogCategory.Export,
                            $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) completed in {assetStopwatch.ElapsedMilliseconds} ms");
                    }

                    if (traceDependencies && enumeratedDependencies > 0 && enumeratedDependencies < ExportConstants.DependencyProgressLogInterval)
                    {
                        long assetEmittedDelta = emittedCount - assetEmittedBefore;
                        long assetSkippedDelta = skippedCount - assetSkippedBefore;
                        Logger.Info(LogCategory.Export,
                            $"[{ownerCollectionId}] Asset {assetIndex}/{assetTotal} (pathID {asset.PathID}) processed {enumeratedDependencies} dependencies => emitted {assetEmittedDelta}, skipped {assetSkippedDelta}");
                    }
                }

                if (_options.Verbose)
                {
                    long collectionEmittedDelta = emittedCount - collectionEmittedBefore;
                    long collectionSkippedDelta = skippedCount - collectionSkippedBefore;
                    Logger.Info(LogCategory.Export,
                        $"[{ownerCollectionId}] Completed '{collectionDisplayName}' - emitted {collectionEmittedDelta}, skipped {collectionSkippedDelta} relations");
                }
            }
        }
        finally
        {
            writer.Dispose();
        }

        result.Shards.AddRange(writer.ShardDescriptors);
        if (_enableIndex)
        {
            result.IndexEntries.AddRange(writer.IndexEntries);
        }

        Logger.Info(LogCategory.Export, $"Exported {emittedCount} dependency relations (skipped {skippedCount}) across {writer.ShardCount} shards");
        return result;
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

        AssetDependencyRelation relation = new AssetDependencyRelation
        {
            From = from,
            To = new AssetPrimaryKey
            {
                CollectionId = resolvedCollectionId,
                PathId = targetPathId
            },
            Edge = new AssetDependencyEdge
            {
                Kind = "serializedRef",
                Field = sanitizedField,
                Optional = null
            },
            Status = DetermineStatus(ownerCollectionId, resolvedCollectionId, owner.PathID, targetPathId, collectionResolved, assetResolved),
            Notes = notes
        };

        return new DependencyResolutionContext(relation, targetCollectionName, targetIsBuiltin);
    }

    private static string DetermineStatus(
        string ownerCollectionId,
        string targetCollectionId,
        long ownerPathId,
        long targetPathId,
        bool collectionResolved,
        bool assetResolved)
    {
        if (string.Equals(ownerCollectionId, targetCollectionId, StringComparison.Ordinal) && ownerPathId == targetPathId)
        {
            return "SelfReference";
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

    private bool ShouldSkip(DependencyResolutionContext context)
    {
        AssetDependencyRelation relation = context.Relation;

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

    private static bool IsBuiltinCollectionId(string collectionId)
    {
        return string.Equals(collectionId, "BUILTIN-EXTRA", StringComparison.Ordinal)
            || string.Equals(collectionId, "BUILTIN-DEFAULT", StringComparison.Ordinal)
            || string.Equals(collectionId, "BUILTIN-EDITOR", StringComparison.Ordinal);
    }

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

    private static string BuildStableKey(AssetDependencyRelation relation)
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

        public static DependencyKey FromRelation(AssetDependencyRelation relation)
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
        public DependencyResolutionContext(AssetDependencyRelation relation, string? targetCollectionName, bool targetIsBuiltin)
        {
            Relation = relation;
            TargetCollectionName = targetCollectionName;
            TargetIsBuiltin = targetIsBuiltin;
        }

        public AssetDependencyRelation Relation { get; }
        public string? TargetCollectionName { get; }
        public bool TargetIsBuiltin { get; }

    }
}
