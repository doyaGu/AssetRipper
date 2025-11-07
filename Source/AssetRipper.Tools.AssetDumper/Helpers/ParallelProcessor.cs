using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Provides utilities for parallel processing of collections with controlled concurrency.
/// Designed for CPU-bound work (record creation, serialization) while maintaining thread safety.
/// </summary>
public static class ParallelProcessor
{
	/// <summary>
	/// Default maximum degree of parallelism (number of CPU cores).
	/// </summary>
	public static int DefaultMaxParallelism => Environment.ProcessorCount;

	/// <summary>
	/// Default batch size for parallel processing.
	/// </summary>
	public static int DefaultBatchSize => 100;

	/// <summary>
	/// Process items in parallel with a processor function.
	/// Results are collected in order.
	/// </summary>
	/// <typeparam name="TInput">Input item type</typeparam>
	/// <typeparam name="TOutput">Output item type</typeparam>
	/// <param name="items">Items to process</param>
	/// <param name="processor">Processing function</param>
	/// <param name="maxParallelism">Maximum degree of parallelism (0 = auto)</param>
	/// <param name="batchSize">Batch size for processing</param>
	/// <returns>List of processed results in original order</returns>
	public static List<TOutput> ProcessInParallel<TInput, TOutput>(
		IEnumerable<TInput> items,
		Func<TInput, TOutput> processor,
		int maxParallelism = 0,
		int batchSize = 0)
	{
		if (processor == null)
			throw new ArgumentNullException(nameof(processor));

		int parallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
		int batch = batchSize > 0 ? batchSize : DefaultBatchSize;

		List<TInput> itemList = items as List<TInput> ?? items.ToList();
		if (itemList.Count == 0)
			return new List<TOutput>();

		// For small collections, use sequential processing
		if (itemList.Count < batch || parallelism == 1)
		{
			return itemList.Select(processor).ToList();
		}

		// Use ConcurrentBag to collect results, then restore order
		ConcurrentBag<(int index, TOutput result)> results = new();

		Parallel.For(0, itemList.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
		{
			TOutput result = processor(itemList[i]);
			results.Add((i, result));
		});

		// Restore original order
		return results.OrderBy(r => r.index).Select(r => r.result).ToList();
	}

	/// <summary>
	/// Process items in parallel with a processor function that may return null.
	/// Null results are filtered out.
	/// </summary>
	/// <typeparam name="TInput">Input item type</typeparam>
	/// <typeparam name="TOutput">Output item type (reference type)</typeparam>
	/// <param name="items">Items to process</param>
	/// <param name="processor">Processing function (may return null)</param>
	/// <param name="maxParallelism">Maximum degree of parallelism (0 = auto)</param>
	/// <param name="batchSize">Batch size for processing</param>
	/// <returns>List of non-null processed results</returns>
	public static List<TOutput> ProcessInParallelWithNulls<TInput, TOutput>(
		IEnumerable<TInput> items,
		Func<TInput, TOutput?> processor,
		int maxParallelism = 0,
		int batchSize = 0)
		where TOutput : class
	{
		if (processor == null)
			throw new ArgumentNullException(nameof(processor));

		int parallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
		int batch = batchSize > 0 ? batchSize : DefaultBatchSize;

		List<TInput> itemList = items as List<TInput> ?? items.ToList();
		if (itemList.Count == 0)
			return new List<TOutput>();

		// For small collections, use sequential processing
		if (itemList.Count < batch || parallelism == 1)
		{
			return itemList.Select(processor).Where(r => r != null).Cast<TOutput>().ToList();
		}

		ConcurrentBag<TOutput> results = new();

		Parallel.ForEach(itemList, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, item =>
		{
			TOutput? result = processor(item);
			if (result != null)
			{
				results.Add(result);
			}
		});

		return results.ToList();
	}

	/// <summary>
	/// Process items in parallel using TPL Dataflow for pipeline-style processing.
	/// Useful when you need to maintain order or have complex producer-consumer patterns.
	/// </summary>
	/// <typeparam name="TInput">Input item type</typeparam>
	/// <typeparam name="TOutput">Output item type</typeparam>
	/// <param name="items">Items to process</param>
	/// <param name="processor">Processing function</param>
	/// <param name="maxParallelism">Maximum degree of parallelism (0 = auto)</param>
	/// <returns>Task that completes when all items are processed, with results list</returns>
	public static async Task<List<TOutput>> ProcessInDataflowPipeline<TInput, TOutput>(
		IEnumerable<TInput> items,
		Func<TInput, TOutput> processor,
		int maxParallelism = 0)
	{
		if (processor == null)
			throw new ArgumentNullException(nameof(processor));

		int parallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;

		List<TOutput> results = new();
		object resultsLock = new();

		var processingBlock = new ActionBlock<TInput>(
			item =>
			{
				TOutput result = processor(item);
				lock (resultsLock)
				{
					results.Add(result);
				}
			},
			new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = parallelism,
				BoundedCapacity = parallelism * 2 // Limit memory usage
			});

		foreach (TInput item in items)
		{
			await processingBlock.SendAsync(item);
		}

		processingBlock.Complete();
		await processingBlock.Completion;

		return results;
	}

	/// <summary>
	/// Estimate optimal parallelism based on collection size and work complexity.
	/// </summary>
	/// <param name="itemCount">Number of items to process</param>
	/// <param name="estimatedWorkPerItemMs">Estimated milliseconds per item</param>
	/// <returns>Recommended parallelism level</returns>
	public static int EstimateOptimalParallelism(int itemCount, double estimatedWorkPerItemMs)
	{
		if (itemCount < 10 || estimatedWorkPerItemMs < 1)
			return 1; // Sequential for small/fast work

		// For CPU-bound work, use processor count
		// For I/O-bound work (estimatedWorkPerItemMs > 100), could use higher parallelism
		int maxParallelism = estimatedWorkPerItemMs > 100 
			? DefaultMaxParallelism * 2 
			: DefaultMaxParallelism;

		// Don't create more threads than items
		return Math.Min(itemCount, maxParallelism);
	}
}
