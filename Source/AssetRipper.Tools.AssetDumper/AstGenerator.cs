using AssetRipper.Import.Logging;
using AssetRipper.Tools.AssetDumper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AssetRipper.Tools.AssetDumper;

internal class AstGenerator
{
	private readonly Options _options;
	private readonly JsonSerializerSettings _jsonSettings;

	public AstGenerator(Options options)
	{
		_options = options;
		_jsonSettings = new JsonSerializerSettings
		{
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			ContractResolver = new SyntaxNodePropertiesResolver(),
			Formatting = _options.CompactJson ? Formatting.None : Formatting.Indented,
			NullValueHandling = _options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
			MaxDepth = 64
		};
	}

	public void GenerateAstFromScripts(string scriptsDir, string outputPath, FilterManager filterManager)
	{
		if (!Directory.Exists(scriptsDir))
		{
			Logger.Warning(LogCategory.Export, $"Scripts directory not found: {scriptsDir}");
			return;
		}

		string astDir = Path.Combine(outputPath, _options.AstOutputFolder);

		// Quick incremental check
		if (_options.IncrementalProcessing && Directory.Exists(astDir))
		{
			var existingAst = Directory.EnumerateFiles(astDir, "*.json", SearchOption.AllDirectories);
			if (existingAst.Any())
			{
				if (!_options.Silent)
				{
					Logger.Info(LogCategory.Export, "Skipping AST generation (already exists)");
				}
				return;
			}
		}

		Directory.CreateDirectory(astDir);

		// Use FilterManager for consistent filtering
		var scriptFiles = filterManager.GetFilteredFiles(new DirectoryInfo(scriptsDir)).ToList();

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Starting AST generation for {scriptFiles.Count} files...");
		}

		if (_options.Verbose && scriptFiles.Count > 0)
		{
			Logger.Info(LogCategory.Export, $"First few files to process:");
			scriptFiles.Take(5).ToList().ForEach(f =>
				Logger.Info(LogCategory.Export, $"  - {Path.GetRelativePath(scriptsDir, f.FullName)} ({f.Length} bytes)"));
			if (scriptFiles.Count > 5)
			{
				Logger.Info(LogCategory.Export, $"  ... and {scriptFiles.Count - 5} more files");
			}

			// Log some of the largest files as they're more likely to cause issues
			var largestFiles = scriptFiles.OrderByDescending(f => f.Length).Take(3).ToList();
			if (largestFiles.Any() && largestFiles.First().Length > 100000) // > 100KB
			{
				Logger.Info(LogCategory.Export, "Largest files (potential timeout risks):");
				largestFiles.ForEach(f =>
					Logger.Info(LogCategory.Export, $"  - {Path.GetRelativePath(scriptsDir, f.FullName)} ({f.Length:N0} bytes)"));
			}
		}

		// Start timing
		var stopwatch = Stopwatch.StartNew();
		var processed = new ConcurrentBag<string>();
		var errors = new ConcurrentBag<string>();
		var skipped = new ConcurrentBag<string>();
		var totalCompleted = 0;
		var lockObject = new object();

		int timeout = _options.FileTimeoutSeconds;

		try
		{
			// Configure parallel processing
			var parallelOptions = new ParallelOptions();
			if (_options.ParallelDegree > 0)
			{
				parallelOptions.MaxDegreeOfParallelism = _options.ParallelDegree;
			}

			// Add cancellation token for timeout
			var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeout));
			parallelOptions.CancellationToken = cts.Token;

			// Use Parallel.ForEach instead of AsParallel().ForAll() for better control
			Parallel.ForEach(scriptFiles, parallelOptions, fileInfo =>
			{
				string relativePath = Path.GetRelativePath(scriptsDir, fileInfo.FullName);

				try
				{
					// Add timeout for individual file processing
					var result = ProcessSingleFileWithTimeout(fileInfo.FullName, scriptsDir, astDir, TimeSpan.FromSeconds(timeout));

					switch (result.Status)
					{
						case ProcessingStatus.Success:
							processed.Add(fileInfo.FullName);
							break;
						case ProcessingStatus.Skipped:
							skipped.Add($"{relativePath}: {result.Reason}");
							break;
						case ProcessingStatus.Error:
							errors.Add($"{relativePath}: {result.Reason}");
							break;
					}

					// Thread-safe progress tracking
					int currentCompleted;
					lock (lockObject)
					{
						currentCompleted = ++totalCompleted;
					}

					// More frequent progress updates near the end
					int progressInterval = _options.Verbose ? 50 : 200;

					// Progress logging - use total completed, not just successful
					if (currentCompleted % progressInterval == 0)
					{
						if (_options.Verbose || (!_options.Silent && (currentCompleted % 200 == 0)))
						{
							var elapsed = stopwatch.Elapsed;
							var estimatedTotal = (elapsed.TotalSeconds / currentCompleted) * scriptFiles.Count;
							var remainingSeconds = Math.Max(0, estimatedTotal - elapsed.TotalSeconds);

							Logger.Info(LogCategory.Export,
								$"AST progress: {currentCompleted}/{scriptFiles.Count} " +
								$"({currentCompleted * 100.0 / scriptFiles.Count:F1}%) " +
								$"[{elapsed:mm\\:ss} elapsed]");
						}
					}

					// Check for cancellation
					parallelOptions.CancellationToken.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException)
				{
					Logger.Error(LogCategory.Export, $"Processing timed out on file: {relativePath}");
					errors.Add($"{relativePath}: timeout");
					throw; // Re-throw to stop parallel processing
				}
				catch (Exception ex)
				{
					errors.Add($"{relativePath}: {ex.Message}");

					// Always log which file caused the error
					Logger.Warning(LogCategory.Export,
						$"Error processing {relativePath}: {ex.Message}");

					// Increment completed count even for errors
					lock (lockObject)
					{
						totalCompleted++;
					}
				}
			});
		}
		catch (OperationCanceledException)
		{
			Logger.Error(LogCategory.Export, "AST generation timed out or was cancelled");
			throw;
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Critical error during parallel AST processing: {ex.Message}");
			throw;
		}
		finally
		{
			stopwatch.Stop();
		}

		// Final reporting
		var processedCount = processed.Count;
		var skippedCount = skipped.Count;
		var errorCount = errors.Count;
		var actualTotal = processedCount + skippedCount + errorCount;

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export,
				$"AST generation completed in {stopwatch.Elapsed:mm\\:ss\\.fff}: " +
				$"{processedCount} successful, {skippedCount} skipped, {errorCount} errors " +
				$"(total: {actualTotal}/{scriptFiles.Count})");
		}

		// Detailed verbose reporting
		if (_options.Verbose)
		{
			if (processedCount > 0)
			{
				var avgTimePerFile = stopwatch.Elapsed.TotalMilliseconds / actualTotal;
				Logger.Info(LogCategory.Export, $"Average processing time: {avgTimePerFile:F2}ms per file");
			}

			if (skippedCount > 0)
			{
				Logger.Info(LogCategory.Export, $"Skipped files breakdown:");
				foreach (var skip in skipped.Take(10))
				{
					Logger.Info(LogCategory.Export, $"  - {skip}");
				}
				if (skippedCount > 10)
				{
					Logger.Info(LogCategory.Export, $"  ... and {skippedCount - 10} more skipped");
				}
			}

			if (errorCount > 0)
			{
				Logger.Warning(LogCategory.Export, $"Error files breakdown:");
				foreach (var error in errors.Take(10))
				{
					Logger.Warning(LogCategory.Export, $"  - {error}");
				}
				if (errorCount > 10)
				{
					Logger.Warning(LogCategory.Export, $"  ... and {errorCount - 10} more errors");
				}
			}

			// Warn if some files were not processed
			if (actualTotal < scriptFiles.Count)
			{
				Logger.Warning(LogCategory.Export,
					$"Warning: {scriptFiles.Count - actualTotal} files were not processed (possibly due to timeout or errors)");
			}
		}
		else if (errorCount > 0 && !_options.Silent)
		{
			Logger.Warning(LogCategory.Export, $"AST generation had {errorCount} errors (use --verbose for details)");
		}
	}

	public void PreviewAstGeneration(string outputPath, FilterManager filterManager)
	{
		string scriptsDir = Path.Combine(outputPath, "Scripts");
		if (!Directory.Exists(scriptsDir))
		{
			if (!_options.Silent)
			{
				Logger.Info(LogCategory.Export, "No scripts directory found for AST preview");
			}
			return;
		}

		var allFiles = Directory.EnumerateFiles(scriptsDir, "*.cs", SearchOption.AllDirectories).Count();
		var filteredFiles = filterManager.GetFilteredFiles(new DirectoryInfo(scriptsDir)).ToList();

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"AST preview: {filteredFiles.Count}/{allFiles} files would be processed");
		}

		if (_options.Verbose && filteredFiles.Any())
		{
			// Show filtering details
			var skipped = allFiles - filteredFiles.Count;
			if (skipped > 0)
			{
				Logger.Info(LogCategory.Export, $"Filtering would skip {skipped} files");
			}

			// Show file size stats
			var sizes = filteredFiles.Take(1000).Select(f => f.Length).ToList();
			if (sizes.Any())
			{
				Logger.Info(LogCategory.Export,
					$"File sizes: {sizes.Min()}-{sizes.Max()} bytes (avg: {sizes.Average():F0})");
			}

			// Show estimated processing time
			var estimatedSeconds = filteredFiles.Count * 0.1; // Rough estimate
			Logger.Info(LogCategory.Export,
				$"Estimated AST generation time: ~{TimeSpan.FromSeconds(estimatedSeconds):mm\\:ss}");
		}
	}

	public static bool TryGenerateAst(string filePath, string code, out string json)
	{
		json = "";
		try
		{
			var tree = CSharpSyntaxTree.ParseText(code);
			var hasErrors = tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);

			var wrapper = new AstGenWrapper(filePath, tree);
			var settings = new JsonSerializerSettings
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
				ContractResolver = new SyntaxNodePropertiesResolver(),
				Formatting = Formatting.Indented,
				NullValueHandling = NullValueHandling.Ignore,
				MaxDepth = 64
			};

			json = JsonConvert.SerializeObject(wrapper, settings);
			return !hasErrors;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private ProcessingResult ProcessSingleFileWithTimeout(string filePath, string scriptsDir, string astDir, TimeSpan timeout)
	{
		var task = Task.Run(() => ProcessSingleFile(filePath, scriptsDir, astDir));

		try
		{
			if (task.Wait(timeout))
			{
				return task.Result;
			}
			else
			{
				// Task timed out
				string relativePath = Path.GetRelativePath(scriptsDir, filePath);
				Logger.Warning(LogCategory.Export, $"File processing timed out after {timeout.TotalSeconds}s: {relativePath}");
				return ProcessingResult.Error($"timeout after {timeout.TotalSeconds}s");
			}
		}
		catch (AggregateException ex)
		{
			// Unwrap the inner exception from the task
			var innerEx = ex.InnerException ?? ex;
			string relativePath = Path.GetRelativePath(scriptsDir, filePath);
			Logger.Warning(LogCategory.Export, $"Exception in file processing: {relativePath}: {innerEx.Message}");
			return ProcessingResult.Error(innerEx.Message);
		}
	}

	private ProcessingResult ProcessSingleFile(string filePath, string scriptsDir, string astDir)
	{
		try
		{
			// Check incremental
			string outputPath = GetOutputPath(filePath, scriptsDir, astDir);
			if (_options.IncrementalProcessing && File.Exists(outputPath) &&
				File.GetLastWriteTime(outputPath) >= File.GetLastWriteTime(filePath))
			{
				return ProcessingResult.Skipped("already up to date");
			}

			// Safe file reading with encoding detection and fallback
			string? code = ReadFileWithEncodingFallback(filePath);
			if (code == null)
				return ProcessingResult.Error("failed to read file");

			// Content-based filters (FileFilterManager handles file-level filters)
			if (_options.MinimumLines > 0 && code.Split('\n').Length < _options.MinimumLines)
				return ProcessingResult.Skipped($"too few lines (<{_options.MinimumLines})");

			if (_options.MaxFileSizeBytes > 0 && Encoding.UTF8.GetByteCount(code) > _options.MaxFileSizeBytes)
				return ProcessingResult.Skipped($"too large (>{_options.MaxFileSizeBytes} bytes)");

			if (TryGenerateAstForFile(filePath, code, out string json))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
				File.WriteAllText(outputPath, json, Encoding.UTF8);
				return ProcessingResult.Success();
			}
			else
			{
				return ProcessingResult.Error("AST generation failed");
			}
		}
		catch (Exception ex)
		{
			return ProcessingResult.Error(ex.Message);
		}
	}

	private static string? ReadFileWithEncodingFallback(string filePath)
	{
		// Try UTF-8 first (most common for C# files)
		try
		{
			return File.ReadAllText(filePath, Encoding.UTF8);
		}
		catch (DecoderFallbackException)
		{
			// UTF-8 failed, try with default encoding
		}
		catch (Exception)
		{
			return null;
		}

		// Try with default system encoding
		try
		{
			return File.ReadAllText(filePath, Encoding.Default);
		}
		catch (Exception)
		{
			// Final fallback - read as bytes and use UTF-8 with replacement
		}

		// Final fallback - read bytes and decode with replacement
		try
		{
			var bytes = File.ReadAllBytes(filePath);
			var encoding = new UTF8Encoding(false, false); // No BOM, no exception on invalid bytes
			return encoding.GetString(bytes);
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to read file {filePath}: {ex.Message}");
			return null;
		}
	}

	private bool TryGenerateAstForFile(string filePath, string code, out string json)
	{
		json = "";
		try
		{
			var tree = CSharpSyntaxTree.ParseText(code);
			var hasErrors = tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);

			var wrapper = new AstGenWrapper(filePath, tree);
			json = JsonConvert.SerializeObject(wrapper, _jsonSettings);
			return !hasErrors;
		}
		catch
		{
			return false;
		}
	}

	private string GetOutputPath(string filePath, string scriptsDir, string astDir)
	{
		string relativePath = Path.GetRelativePath(scriptsDir, filePath);
		string jsonPath = Path.ChangeExtension(relativePath, ".json");
		return Path.Combine(astDir, jsonPath);
	}

	private enum ProcessingStatus
	{
		Success,
		Skipped,
		Error
	}

	private class ProcessingResult
	{
		public ProcessingStatus Status { get; set; }
		public string Reason { get; set; } = "";

		public static ProcessingResult Success() => new() { Status = ProcessingStatus.Success };
		public static ProcessingResult Skipped(string reason) => new() { Status = ProcessingStatus.Skipped, Reason = reason };
		public static ProcessingResult Error(string reason) => new() { Status = ProcessingStatus.Error, Reason = reason };
	}
}
