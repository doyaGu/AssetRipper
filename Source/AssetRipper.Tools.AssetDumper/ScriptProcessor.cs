﻿using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using System.Diagnostics;

namespace AssetRipper.Tools.AssetDumper;

internal class ScriptProcessor
{
	private readonly Options _options;
	private readonly FilterManager _filterManager;
	private readonly AstGenerator _astGenerator;

	public ScriptProcessor(Options options, FilterManager filterManager)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_filterManager = filterManager ?? throw new ArgumentNullException(nameof(filterManager));
		_astGenerator = new AstGenerator(_options);
	}

	public void ProcessScripts(GameData gameData)
	{
		if (gameData.AssemblyManager == null || !gameData.AssemblyManager.IsSet)
		{
			if (!_options.Silent)
			{
				Logger.Warning(LogCategory.Export, "No assembly manager available");
			}
			return;
		}

		var totalStopwatch = Stopwatch.StartNew();

		bool needsAssemblies = _options.ExportAssemblies;
		bool needsScripts = _options.ExportScripts || _options.GenerateAst;
		bool needsAst = _options.GenerateAst;

		string scriptsDir = Path.Combine(_options.OutputPath, "Scripts");
		bool hasScripts = _options.IncrementalProcessing && Directory.Exists(scriptsDir) &&
			_filterManager.GetFilteredFiles(new DirectoryInfo(scriptsDir)).Any();

		try
		{
			// Export assemblies if needed
			if (needsAssemblies || (needsScripts && !hasScripts))
			{
				if (!_options.Silent)
				{
					Logger.Info(LogCategory.Export, "Exporting assemblies...");
				}
				var assemblyStopwatch = Stopwatch.StartNew();
				ExportAssemblyDlls(gameData);
				assemblyStopwatch.Stop();

				if (_options.Verbose)
				{
					Logger.Info(LogCategory.Export, $"Assembly export completed in {assemblyStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}

			// Decompile scripts if needed
			if (needsScripts && !hasScripts)
			{
				if (!_options.Silent)
				{
					Logger.Info(LogCategory.Export, "Decompiling scripts...");
				}
				var scriptStopwatch = Stopwatch.StartNew();
				DecompileScripts();
				scriptStopwatch.Stop();

				if (_options.Verbose)
				{
					Logger.Info(LogCategory.Export, $"Script decompilation completed in {scriptStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}
			else if (needsAst && !hasScripts)
			{
				// AST needs scripts but user didn't request them - auto-enable
				if (!_options.Silent)
				{
					Logger.Info(LogCategory.Export, "Decompiling scripts (required for AST)...");
				}
				if (!Directory.Exists(Path.Combine(_options.OutputPath, "Assemblies")))
				{
					ExportAssemblyDlls(gameData);
				}
				DecompileScripts();
			}

			// Generate AST if needed
			if (needsAst)
			{
				if (!_options.Silent)
				{
					Logger.Info(LogCategory.Export, "Generating AST...");
				}
				var astStopwatch = Stopwatch.StartNew();
				_astGenerator.GenerateAstFromScripts(scriptsDir, _options.OutputPath, _filterManager);
				astStopwatch.Stop();

				if (_options.Verbose)
				{
					Logger.Info(LogCategory.Export, $"AST generation completed in {astStopwatch.Elapsed:mm\\:ss\\.fff}");
				}
			}
		}
		finally
		{
			totalStopwatch.Stop();

			if (_options.Verbose)
			{
				Logger.Info(LogCategory.Export, $"Total script processing time: {totalStopwatch.Elapsed:mm\\:ss\\.fff}");
			}
		}
	}

	public void PreviewScriptProcessing(GameData gameData)
	{
		if (gameData.AssemblyManager?.IsSet != true)
		{
			if (!_options.Silent)
			{
				Logger.Warning(LogCategory.Export, "No assembly manager available");
			}
			return;
		}

		var assemblies = _filterManager.GetFilteredAssemblies(gameData.AssemblyManager);
		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Would process {assemblies.Count} assemblies");
		}

		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export, "Assembly breakdown:");
			assemblies.Take(5).ToList().ForEach(a =>
				Logger.Info(LogCategory.Export, $"  - {a.Name}"));
			if (assemblies.Count > 5)
				Logger.Info(LogCategory.Export, $"  ... and {assemblies.Count - 5} more");
		}

		// Preview script files if they exist
		string scriptsDir = Path.Combine(_options.OutputPath, "Scripts");
		if (Directory.Exists(scriptsDir))
		{
			var scriptFiles = _filterManager.GetFilteredFiles(new DirectoryInfo(scriptsDir)).ToList();
			if (!_options.Silent)
			{
				Logger.Info(LogCategory.Export, $"Would process {scriptFiles.Count} script files for AST");
			}

			if (_options.Verbose && scriptFiles.Any())
			{
				Logger.Info(LogCategory.Export, "Sample script files:");
				scriptFiles.Take(3).ToList().ForEach(f =>
					Logger.Info(LogCategory.Export, $"  - {Path.GetRelativePath(scriptsDir, f.FullName)} ({f.Length} bytes)"));

				if (scriptFiles.Count > 3)
				{
					Logger.Info(LogCategory.Export, $"  ... and {scriptFiles.Count - 3} more files");
				}
			}
		}

		if (_options.GenerateAst)
		{
			_astGenerator.PreviewAstGeneration(_options.OutputPath, _filterManager);
		}
	}

	private void ExportAssemblyDlls(GameData gameData)
	{
		string assemblyDir = Path.Combine(_options.OutputPath, "Assemblies");

		if (_options.IncrementalProcessing && Directory.Exists(assemblyDir) &&
			Directory.GetFiles(assemblyDir, "*.dll").Any())
		{
			if (!_options.Silent)
			{
				Logger.Info(LogCategory.Export, "Skipping assembly export (already exists)");
			}
			return;
		}

		Directory.CreateDirectory(assemblyDir);

		var assemblies = _filterManager.GetFilteredAssemblies(gameData.AssemblyManager!);

		if (_options.Verbose)
		{
			Logger.Info(LogCategory.Export, $"Exporting {assemblies.Count} filtered assemblies");
		}

		var stopwatch = Stopwatch.StartNew();
		int exported = 0;

		foreach (var assembly in assemblies)
		{
			try
			{
				var stream = gameData.AssemblyManager!.GetStreamForAssembly(assembly);
				stream.Position = 0;

				string path = Path.Combine(assemblyDir, assembly.Name + ".dll");
				using var fileStream = File.Create(path);
				stream.CopyTo(fileStream);
				exported++;

				if (_options.Verbose)
				{
					Logger.Debug(LogCategory.Export, $"Exported: {assembly.Name}.dll ({stream.Length} bytes)");
				}
			}
			catch (Exception ex)
			{
				if (_options.Verbose)
				{
					Logger.Warning(LogCategory.Export, $"Failed to export {assembly.Name}: {ex.Message}");
				}
			}
		}

		stopwatch.Stop();

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export,
				$"Assembly export completed: {exported}/{assemblies.Count} successful in {stopwatch.Elapsed:mm\\:ss\\.fff}");
		}
	}

	private void DecompileScripts()
	{
		string scriptsDir = Path.Combine(_options.OutputPath, "Scripts");
		string assemblyDir = Path.Combine(_options.OutputPath, "Assemblies");

		if (!Directory.Exists(assemblyDir))
		{
			throw new InvalidOperationException("Assemblies directory not found. Export assemblies first.");
		}

		Directory.CreateDirectory(scriptsDir);

		var assemblyFiles = Directory.GetFiles(assemblyDir, "*.dll")
			.Where(f => _filterManager.ShouldProcessAssembly(Path.GetFileNameWithoutExtension(f)))
			.ToList();

		if (!_options.Silent)
		{
			Logger.Info(LogCategory.Export, $"Decompiling {assemblyFiles.Count} assemblies...");
			if (_options.Verbose)
			{
				Logger.Info(LogCategory.Export, $"Assembly timeout: {Math.Min(600, _options.FileTimeoutSeconds * 20)} seconds per assembly");
			}
		}

		var stopwatch = Stopwatch.StartNew();
		var completed = 0;
		var errors = new List<string>();
		var lockObject = new object();

		// Overall timeout - scale with number of assemblies, minimum 30 minutes
		var overallTimeoutMinutes = Math.Max(30, assemblyFiles.Count * 5);

		try
		{
			var parallelOptions = new ParallelOptions();
			if (_options.ParallelDegree > 0)
			{
				parallelOptions.MaxDegreeOfParallelism = _options.ParallelDegree;
			}

			// Add cancellation token for overall timeout
			var overallCts = new CancellationTokenSource(TimeSpan.FromMinutes(overallTimeoutMinutes));
			parallelOptions.CancellationToken = overallCts.Token;

			Parallel.ForEach(assemblyFiles, parallelOptions, assemblyPath =>
			{
				// Individual assembly timeout - much longer than file timeout since decompilation is expensive
				var assemblyTimeoutSeconds = Math.Min(600, _options.FileTimeoutSeconds * 20); // Max 10 minutes per assembly
				var assemblyTimeout = TimeSpan.FromSeconds(assemblyTimeoutSeconds);

				try
				{
					var result = DecompileAssemblyWithTimeout(assemblyPath, scriptsDir, assemblyTimeout);

					switch (result.Status)
					{
						case DecompilationStatus.Success:
							break;
						case DecompilationStatus.Timeout:
							lock (errors)
							{
								var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
								errors.Add($"{assemblyName}: timeout after {assemblyTimeoutSeconds} seconds");
							}
							break;
						case DecompilationStatus.Error:
							lock (errors)
							{
								var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
								errors.Add($"{assemblyName}: {result.ErrorMessage}");
							}
							break;
					}

					var current = Interlocked.Increment(ref completed);

					// Simple progress reporting
					if (!_options.Silent)
					{
						if (_options.Verbose && current % 2 == 0)
						{
							var percentage = current * 100.0 / assemblyFiles.Count;
							var elapsed = stopwatch.Elapsed;
							Logger.Info(LogCategory.Export,
								$"Decompilation progress: {current}/{assemblyFiles.Count} ({percentage:F1}%) " +
								$"[{elapsed:mm\\:ss} elapsed]");
						}
						else if (!_options.Verbose && current % 5 == 0)
						{
							var percentage = current * 100.0 / assemblyFiles.Count;
							Logger.Info(LogCategory.Export,
								$"Decompilation progress: {current}/{assemblyFiles.Count} ({percentage:F1}%)");
						}
					}

					// Check for cancellation
					parallelOptions.CancellationToken.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException)
				{
					var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
					Logger.Error(LogCategory.Export, $"Processing cancelled on assembly: {assemblyName}");
					lock (errors)
					{
						errors.Add($"{assemblyName}: cancelled");
					}
					throw; // Re-throw to stop parallel processing
				}
				catch (Exception ex)
				{
					var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
					lock (errors)
					{
						errors.Add($"{assemblyName}: {ex.Message}");
					}

					Logger.Warning(LogCategory.Export,
						$"Error processing {assemblyName}: {ex.Message}");

					// Increment completed count even for errors
					lock (lockObject)
					{
						completed++;
					}
				}
			});
		}
		catch (OperationCanceledException)
		{
			Logger.Error(LogCategory.Export, $"Decompilation was cancelled after {overallTimeoutMinutes} minutes");
			throw;
		}
		finally
		{
			stopwatch.Stop();
		}

		if (!_options.Silent)
		{
			var successful = completed - errors.Count;
			Logger.Info(LogCategory.Export,
				$"Decompilation completed in {stopwatch.Elapsed:mm\\:ss\\.fff}: " +
				$"{successful} successful, {errors.Count} errors " +
				$"(total: {completed}/{assemblyFiles.Count})");
		}

		// Show errors if any
		if (errors.Count > 0 && (_options.Verbose || !_options.Silent))
		{
			var errorList = errors.Take(5).ToList();
			Logger.Warning(LogCategory.Export, $"First {errorList.Count} decompilation errors:");
			foreach (var error in errorList)
			{
				Logger.Warning(LogCategory.Export, $"  - {error}");
			}
			if (errors.Count > 5)
			{
				Logger.Warning(LogCategory.Export, $"  ... and {errors.Count - 5} more errors");
			}
		}
	}

	private DecompilationResult DecompileAssemblyWithTimeout(string assemblyPath, string scriptsDir, TimeSpan timeout)
	{
		var task = Task.Run(() => DecompileSingleAssembly(assemblyPath, scriptsDir));

		try
		{
			if (task.Wait(timeout))
			{
				return task.Result;
			}
			else
			{
				// Task timed out
				string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
				Logger.Warning(LogCategory.Export, $"Assembly decompilation timed out after {timeout.TotalSeconds}s: {assemblyName}");

				// Clean up partial output on timeout
				string outputDir = Path.Combine(scriptsDir, assemblyName);
				try
				{
					if (Directory.Exists(outputDir))
					{
						Directory.Delete(outputDir, true);
					}
				}
				catch
				{
					// Ignore cleanup errors
				}

				return DecompilationResult.Timeout();
			}
		}
		catch (AggregateException ex)
		{
			// Unwrap the inner exception from the task
			var innerEx = ex.InnerException ?? ex;
			string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
			Logger.Warning(LogCategory.Export, $"Exception in assembly decompilation: {assemblyName}: {innerEx.Message}");
			return DecompilationResult.Error(innerEx.Message);
		}
	}

	private DecompilationResult DecompileSingleAssembly(string assemblyPath, string scriptsDir)
	{
		string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
		string outputDir = Path.Combine(scriptsDir, assemblyName);

		try
		{
			Directory.CreateDirectory(outputDir);

			var settings = new DecompilerSettings
			{
				AlwaysShowEnumMemberValues = true,
				ShowXmlDocumentation = false, // Avoid PDB issues
				UseSdkStyleProjectFormat = false,
				UseNestedDirectoriesForNamespaces = true
			};

			// Create decompiler and PE file with proper disposal
			using var file = new PEFile(assemblyPath);
			var decompiler = new WholeProjectDecompiler(settings,
				new UniversalAssemblyResolver(assemblyPath, false, null), null, null, null);

			// This is the expensive operation - the timeout mechanism works by cancelling the task
			decompiler.DecompileProject(file, outputDir);

			if (_options.Verbose)
			{
				var csFiles = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length;
				Logger.Debug(LogCategory.Export, $"Decompiled {assemblyName}: {csFiles} C# files");
			}

			return DecompilationResult.Success();
		}
		catch (BadImageFormatException ex)
		{
			return DecompilationResult.Error($"Invalid assembly format: {ex.Message}");
		}
		catch (FileNotFoundException ex)
		{
			return DecompilationResult.Error($"Assembly file not found: {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			return DecompilationResult.Error($"Access denied: {ex.Message}");
		}
		catch (NotSupportedException ex)
		{
			return DecompilationResult.Error($"Decompilation not supported: {ex.Message}");
		}
		catch (Exception ex) when (ex.Message.Contains("PDB") || ex.Message.Contains("debug"))
		{
			// PDB/debug info issues - retry without XML documentation
			try
			{
				var basicSettings = new DecompilerSettings
				{
					AlwaysShowEnumMemberValues = true,
					ShowXmlDocumentation = false,
					UseSdkStyleProjectFormat = false,
					UseNestedDirectoriesForNamespaces = true
				};

				var basicDecompiler = new WholeProjectDecompiler(basicSettings,
					new UniversalAssemblyResolver(assemblyPath, false, null), null, null, null);

				using var retryFile = new PEFile(assemblyPath);
				basicDecompiler.DecompileProject(retryFile, outputDir);

				if (_options.Verbose)
				{
					var csFiles = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length;
					Logger.Debug(LogCategory.Export, $"Decompiled {assemblyName} (retry mode): {csFiles} C# files");
				}

				return DecompilationResult.Success();
			}
			catch (Exception retryEx)
			{
				return DecompilationResult.Error($"Failed to decompile (retry also failed): {retryEx.Message}");
			}
		}
		catch (Exception ex)
		{
			// Clean up partial output on failure
			try
			{
				if (Directory.Exists(outputDir) && !Directory.EnumerateFileSystemEntries(outputDir).Any())
				{
					Directory.Delete(outputDir);
				}
			}
			catch
			{
				// Ignore cleanup errors
			}

			return DecompilationResult.Error($"Failed to decompile: {ex.Message}");
		}
	}

	private enum DecompilationStatus
	{
		Success,
		Timeout,
		Error
	}

	private class DecompilationResult
	{
		public DecompilationStatus Status { get; set; }
		public string ErrorMessage { get; set; } = "";

		public static DecompilationResult Success() => new() { Status = DecompilationStatus.Success };
		public static DecompilationResult Timeout() => new() { Status = DecompilationStatus.Timeout };
		public static DecompilationResult Error(string message) => new() { Status = DecompilationStatus.Error, ErrorMessage = message };
	}
}
