using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace AssetRipper.Tools.AssetDumper.Processors;

/// <summary>
/// Configuration for parallel processing operations.
/// </summary>
public class ParallelProcessingOptions
{
	/// <summary>
	/// Maximum degree of parallelism. If null, uses system default.
	/// </summary>
	public int? MaxDegreeOfParallelism { get; set; }

	/// <summary>
	/// Cancellation token for the operation.
	/// </summary>
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <summary>
	/// Whether to preserve order of results.
	/// </summary>
	public bool PreserveOrder { get; set; } = false;

	/// <summary>
	/// Size of internal processing buffers.
	/// </summary>
	public int BufferSize { get; set; } = 1000;

	/// <summary>
	/// Creates default parallel processing options based on system capabilities.
	/// </summary>
	public static ParallelProcessingOptions Default => new()
	{
		MaxDegreeOfParallelism = Environment.ProcessorCount,
		PreserveOrder = false
	};

	/// <summary>
	/// Creates options for single-threaded execution (for debugging).
	/// </summary>
	public static ParallelProcessingOptions Sequential => new()
	{
		MaxDegreeOfParallelism = 1,
		PreserveOrder = true
	};
}

/// <summary>
/// Provides parallel processing capabilities for large-scale data operations.
/// </summary>
public class ParallelProcessor
{
	private readonly ParallelProcessingOptions _options;

	public ParallelProcessor(ParallelProcessingOptions? options = null)
	{
		_options = options ?? ParallelProcessingOptions.Default;
	}

	/// <summary>
	/// Processes items in parallel using the specified action.
	/// </summary>
	public void ProcessInParallel<T>(
		IEnumerable<T> items,
		Action<T> action,
		Action<Exception>? onError = null)
	{
		var parallelOptions = CreateParallelOptions();

		try
		{
			if (_options.PreserveOrder)
			{
				// Use ordered processing
				foreach (var item in items)
				{
					_options.CancellationToken.ThrowIfCancellationRequested();
					try
					{
						action(item);
					}
					catch (Exception ex) when (onError != null)
					{
						onError(ex);
					}
				}
			}
			else
			{
				// Use unordered parallel processing
				System.Threading.Tasks.Parallel.ForEach(
					items,
					parallelOptions,
					item =>
					{
						try
						{
							action(item);
						}
						catch (Exception ex) when (onError != null)
						{
							onError(ex);
						}
					});
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
			throw;
		}
	}

	/// <summary>
	/// Processes items in parallel and returns results.
	/// </summary>
	public IEnumerable<TResult> ProcessInParallel<TInput, TResult>(
		IEnumerable<TInput> items,
		Func<TInput, TResult> transform,
		Action<Exception>? onError = null)
	{
		var results = new ConcurrentBag<TResult>();
		var parallelOptions = CreateParallelOptions();

		try
		{
			System.Threading.Tasks.Parallel.ForEach(
				items,
				parallelOptions,
				item =>
				{
					try
					{
						var result = transform(item);
						results.Add(result);
					}
					catch (Exception ex) when (onError != null)
					{
						onError(ex);
					}
				});
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
			throw;
		}

		return results;
	}

	/// <summary>
	/// Processes items in batches in parallel.
	/// </summary>
	public void ProcessInBatches<T>(
		IEnumerable<T> items,
		int batchSize,
		Action<IEnumerable<T>> batchAction,
		Action<Exception>? onError = null)
	{
		var batches = items
			.Select((item, index) => new { item, index })
			.GroupBy(x => x.index / batchSize)
			.Select(g => g.Select(x => x.item));

		ProcessInParallel(batches, batchAction, onError);
	}

	/// <summary>
	/// Processes items using a producer-consumer pattern with bounded capacity.
	/// </summary>
	public async Task ProcessWithPipelineAsync<TInput, TOutput>(
		IEnumerable<TInput> items,
		Func<TInput, Task<TOutput>> transform,
		Action<TOutput> consume,
		Action<Exception>? onError = null)
	{
		var transformBlock = new TransformBlock<TInput, TOutput>(
			transform,
			new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism ?? Environment.ProcessorCount,
				BoundedCapacity = _options.BufferSize,
				CancellationToken = _options.CancellationToken
			});

		var actionBlock = new ActionBlock<TOutput>(
			consume,
			new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = 1, // Single consumer for ordered writes
				BoundedCapacity = _options.BufferSize,
				CancellationToken = _options.CancellationToken
			});

		transformBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

		try
		{
			foreach (var item in items)
			{
				await transformBlock.SendAsync(item, _options.CancellationToken);
			}

			transformBlock.Complete();
			await actionBlock.Completion;
		}
		catch (Exception ex) when (onError != null)
		{
			onError(ex);
		}
	}

	private ParallelOptions CreateParallelOptions()
	{
		var options = new ParallelOptions
		{
			CancellationToken = _options.CancellationToken
		};

		if (_options.MaxDegreeOfParallelism.HasValue)
		{
			options.MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism.Value;
		}

		return options;
	}
}
