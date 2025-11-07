using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Metrics;

/// <summary>
/// Registry for managing available metrics collectors.
/// Follows the extensibility principle - new metrics types can be added without modifying core export logic.
/// </summary>
public sealed class MetricsCollectorRegistry
{
	private readonly Dictionary<string, Func<Options, IMetricsCollector>> _factories = new();

	public MetricsCollectorRegistry()
	{
		// Register built-in metrics collectors
		// More collectors will be added as they are implemented
	}

	/// <summary>
	/// Register a metrics collector factory.
	/// </summary>
	public void Register(string metricsId, Func<Options, IMetricsCollector> factory)
	{
		if (string.IsNullOrWhiteSpace(metricsId))
			throw new ArgumentException("Metrics ID cannot be null or whitespace", nameof(metricsId));
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));

		_factories[metricsId] = factory;
	}

	/// <summary>
	/// Create all registered metrics collectors.
	/// </summary>
	public IEnumerable<IMetricsCollector> CreateAll(Options options)
	{
		foreach (Func<Options, IMetricsCollector> factory in _factories.Values)
		{
			yield return factory(options);
		}
	}

	/// <summary>
	/// Create a specific metrics collector by ID.
	/// </summary>
	public IMetricsCollector? Create(string metricsId, Options options)
	{
		return _factories.TryGetValue(metricsId, out Func<Options, IMetricsCollector>? factory) ? factory(options) : null;
	}

	/// <summary>
	/// Get all registered metrics IDs.
	/// </summary>
	public IEnumerable<string> GetRegisteredIds() => _factories.Keys;
}
