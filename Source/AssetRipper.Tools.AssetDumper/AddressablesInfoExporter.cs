using System;
using System.Collections.Generic;
using System.Linq;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets;
using AssetRipper.Import.Logging;
using Newtonsoft.Json;

namespace AssetRipper.Tools.AssetDumper;

internal class AddressablesInfoExporter
{
	private static readonly string[] AddressablesHints = { "Addressable", "Addressables" };

	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public AddressablesInfoExporter(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = JsonHelper.CreateSettings(_options);
	}

	public void ExportAddressablesReport(IReadOnlyList<AssetCollection> collections)
	{
		string addressablesPath = Path.Combine(_options.OutputPath, "Addressables");
		ExportHelper.EnsureDirectoryExists(addressablesPath);

		var entries = new List<Dictionary<string, object>>();

		foreach (AssetCollection collection in collections)
		{
			string collectionId = ExportHelper.ComputeCollectionId(collection);
			foreach (var asset in collection.Assets.Values)
			{
				if (!IsAddressablesAsset(collection, asset))
				{
					continue;
				}

				entries.Add(new Dictionary<string, object>
				{
					["collectionId"] = collectionId,
					["collectionName"] = collection.Name,
					["bundleName"] = collection.Bundle.Name,
					["pathID"] = asset.PathID,
					["classID"] = asset.ClassID,
					["className"] = asset.ClassName,
					["assetName"] = asset.GetBestName(),
					["originalPath"] = asset.OriginalPath ?? string.Empty
				});
			}
		}

		bool addressablesDetected = entries.Count > 0;
		if (addressablesDetected)
		{
			Logger.Info(LogCategory.Export, $"Detected {entries.Count} addressable assets across {entries.Select(e => e["collectionId"]).Distinct().Count()} collections.");
		}
		else
		{
			Logger.Info(LogCategory.Export, "No Addressables-specific assets detected. Writing placeholder report.");
		}

		var payload = new Dictionary<string, object>
		{
			["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			["addressablesUsed"] = addressablesDetected,
			["entryCount"] = entries.Count,
			["entries"] = entries
		};

		string filePath = Path.Combine(addressablesPath, "index.json");
		ExportHelper.WriteJsonFile(payload, filePath, _jsonSettings);
	}

	private static bool IsAddressablesAsset(AssetCollection collection, IUnityObjectBase asset)
	{
		if (MatchesHints(collection.Name))
		{
			return true;
		}

		if (MatchesHints(asset.ClassName))
		{
			return true;
		}

		if (!string.IsNullOrEmpty(asset.GetType().Name) && MatchesHints(asset.GetType().Name))
		{
			return true;
		}

		if (!string.IsNullOrEmpty(asset.GetBestName()) && MatchesHints(asset.GetBestName()))
		{
			return true;
		}

		if (!string.IsNullOrEmpty(asset.OriginalPath) && MatchesHints(asset.OriginalPath))
		{
			return true;
		}

		return false;

		static bool MatchesHints(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}

			foreach (string hint in AddressablesHints)
			{
				if (value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}
	}
}
