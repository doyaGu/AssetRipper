using System;
using System.Collections.Generic;
using System.Linq;
using AssetRipper.Assets.Bundles;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.Processing;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal class ResourceInfoExporter
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public ResourceInfoExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateSettings(_options);
	}

	public void ExportResources(GameData gameData)
	{
		var resourceMap = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
		CollectBundleResources(gameData.GameBundle, resourceMap);

		string resourcesOutputPath = Path.Combine(_options.OutputPath, "Resources");
		ExportHelper.EnsureDirectoryExists(resourcesOutputPath);

		var allocatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var indexEntries = new List<Dictionary<string, object>>();
		int totalResourceCount = 0;

		foreach (var (bundleName, resources) in resourceMap.OrderBy(entry => entry.Key, StringComparer.Ordinal))
		{
			string bundleId = ExportHelper.ComputeStableHash(bundleName ?? string.Empty);

			string safeBundleName = ExportHelper.SanitizeFileName(bundleName ?? string.Empty);
			if (string.IsNullOrWhiteSpace(safeBundleName))
			{
				safeBundleName = "bundle";
			}

			string fileName = $"{safeBundleName}.json";
			if (!allocatedNames.Add(fileName))
			{
				fileName = $"{safeBundleName}_{bundleId}.json";
				allocatedNames.Add(fileName);
			}

			string filePath = Path.Combine(resourcesOutputPath, fileName);

			var bundleDocument = new Dictionary<string, object>
			{
				["bundleName"] = bundleName ?? string.Empty,
				["bundleId"] = bundleId,
				["resourceCount"] = resources.Count,
				["resources"] = resources
			};

			ExportHelper.WriteJsonFile(bundleDocument, filePath, _jsonSettings);

			indexEntries.Add(new Dictionary<string, object>
			{
				["bundleName"] = bundleName ?? string.Empty,
				["bundleId"] = bundleId,
				["file"] = fileName,
				["resourceCount"] = resources.Count
			});

			totalResourceCount += resources.Count;
			Logger.Debug(LogCategory.Export, $"Exported {resources.Count} resources for bundle {bundleName}");
		}

		if (indexEntries.Count == 0)
		{
			Logger.Info(LogCategory.Export, "No resources found to export. Writing empty Resources/index.json placeholder.");
		}
		else
		{
			Logger.Info(LogCategory.Export, $"Exported {totalResourceCount} resources across {indexEntries.Count} bundles.");
		}

		var indexDocument = new Dictionary<string, object>
		{
			["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			["bundleCount"] = indexEntries.Count,
			["resourceCount"] = totalResourceCount,
			["bundles"] = indexEntries
		};

		string indexFile = Path.Combine(resourcesOutputPath, "index.json");
		ExportHelper.WriteJsonFile(indexDocument, indexFile, _jsonSettings);
	}

	private void CollectBundleResources(Bundle bundle, Dictionary<string, List<Dictionary<string, object>>> resourceMap)
	{
		if (bundle.Resources.Count > 0)
		{
			if (!resourceMap.TryGetValue(bundle.Name, out var resources))
			{
				resources = new List<Dictionary<string, object>>();
				resourceMap[bundle.Name] = resources;
			}

			foreach (ResourceFile resource in bundle.Resources)
			{
				try
				{
					resources.Add(CreateResourceEntry(bundle.Name, resource));
				}
				catch (Exception ex)
				{
					Logger.Warning(LogCategory.Export, $"Failed to capture resource {resource.Name} in bundle {bundle.Name}: {ex.Message}");
				}
			}
		}

		foreach (Bundle child in bundle.Bundles)
		{
			CollectBundleResources(child, resourceMap);
		}
	}

	private Dictionary<string, object> CreateResourceEntry(string? bundleName, ResourceFile resource)
	{
		long size = 0;
		try
		{
			size = resource.Stream.Length;
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to read resource size for {resource.Name} in bundle {bundleName}: {ex.Message}");
		}

		string stableKey = string.Join("|", bundleName ?? string.Empty, resource.Name ?? string.Empty, resource.FilePath ?? string.Empty);

		return new Dictionary<string, object>
		{
			["resourceId"] = ExportHelper.ComputeStableHash(stableKey),
			["name"] = resource.Name ?? string.Empty,
			["nameFixed"] = resource.NameFixed ?? string.Empty,
			["filePath"] = resource.FilePath ?? string.Empty,
			["bundleName"] = bundleName ?? string.Empty,
			["size"] = size,
			["type"] = resource.GetType().Name
		};
	}
}
