using AssetRipper.Assets.Collections;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;
using AssetRipper.Tools.AssetDumper.Models;
using AssetRipper.Tools.AssetDumper.Models.Facts;
using AssetRipper.Tools.AssetDumper.Models.Relations;
using AssetRipper.Tools.AssetDumper.Models.Common;
using Newtonsoft.Json;
using System.Reflection;
using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Generators;

/// <summary>
/// Generates <c>manifest.json</c> for the AssetDumper v2 export layout.
/// </summary>
internal sealed class ManifestGenerator
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public ManifestGenerator(Options options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_jsonSettings = new JsonSerializerSettings
		{
			Formatting = _options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};
	}

	public void GenerateManifest(
		GameData gameData,
		IReadOnlyList<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes = null,
		Manifest? baseline = null)
	{
		if (gameData is null)
		{
			throw new ArgumentNullException(nameof(gameData));
		}

		if (domainResults is null)
		{
			throw new ArgumentNullException(nameof(domainResults));
		}

		FinalizeAndWriteManifest(CreateProducer(gameData), domainResults, indexes, baseline);
	}

	public void GenerateManifest(
		ManifestProducer producer,
		IReadOnlyList<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes = null,
		Manifest? baseline = null)
	{
		if (producer is null)
		{
			throw new ArgumentNullException(nameof(producer));
		}

		if (domainResults is null)
		{
			throw new ArgumentNullException(nameof(domainResults));
		}

		FinalizeAndWriteManifest(producer, domainResults, indexes, baseline);
	}

	private void FinalizeAndWriteManifest(
		ManifestProducer producer,
		IReadOnlyList<DomainExportResult> domainResults,
		Dictionary<string, ManifestIndex>? indexes,
		Manifest? baseline)
	{
		if (producer is null)
		{
			throw new ArgumentNullException(nameof(producer));
		}

		ManifestAssembler assembler = new ManifestAssembler(_options);
		Manifest manifest = assembler.Assemble(producer, domainResults, indexes, baseline);
		WriteManifest(manifest);
	}

	private ManifestProducer CreateProducer(GameData gameData)
	{
		ManifestProducer producer = new()
		{
			Name = "AssetDumper",
			Version = GetToolVersion(),
			AssetRipperVersion = GetAssetRipperVersion()
		};

		AssetCollection? firstCollection = gameData.GameBundle.FetchAssetCollections().FirstOrDefault();
		if (firstCollection != null)
		{
			producer.UnityVersion = firstCollection.Version.ToString();
			producer.ProjectName = gameData.GameBundle.Name;
		}

		return producer;
	}

	private void WriteManifest(Manifest manifest)
	{
		string manifestPath = Path.Combine(_options.OutputPath, "manifest.json");
		string json = JsonConvert.SerializeObject(manifest, _jsonSettings);
		File.WriteAllText(manifestPath, json);
	}

	private string GetToolVersion()
	{
		try
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			Version? version = assembly.GetName().Version;
			return version?.ToString() ?? "unknown";
		}
		catch
		{
			return "unknown";
		}
	}

	private string GetAssetRipperVersion()
	{
		try
		{
			Assembly? assetRipperAssembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(static assembly => assembly.GetName().Name?.Contains("AssetRipper", StringComparison.OrdinalIgnoreCase) == true);

			if (assetRipperAssembly != null)
			{
				Version? version = assetRipperAssembly.GetName().Version;
				return version?.ToString() ?? "unknown";
			}

			return "unknown";
		}
		catch
		{
			return "unknown";
		}
	}
}
