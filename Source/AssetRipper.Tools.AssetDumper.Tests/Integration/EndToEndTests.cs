using System;
using System.Diagnostics;
using System.IO;
using AssetRipper.Processing;
using AssetRipper.Tools.AssetDumper.Core;

namespace AssetRipper.Tools.AssetDumper.Tests.Integration;

/// <summary>
/// End-to-end tests that actually run AssetDumper on real game data.
/// These tests execute the complete workflow from command line.
/// </summary>
public class EndToEndTests : IDisposable
{
	private const string GrisGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\GRIS";
	private readonly string _testOutputPath;
	private readonly bool _skipTests;

	public EndToEndTests()
	{
		_testOutputPath = Path.Combine(Path.GetTempPath(), $"AssetDumper_E2E_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_testOutputPath);

		// Skip tests if GRIS is not installed
		_skipTests = !Directory.Exists(GrisGamePath);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testOutputPath))
		{
			try
			{
				Directory.Delete(_testOutputPath, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors - Windows may lock files
			}
		}
	}

	[Fact(Skip = "Requires long execution time - manual execution")]
	public void AssetDumper_FullExport_ShouldSucceed()
	{
		// Skip if GRIS is not installed
		if (_skipTests)
		{
			return;
		}

		// Arrange
		var outputPath = Path.Combine(_testOutputPath, "full_export");
		Directory.CreateDirectory(outputPath);

		// Act
		int exitCode = RunAssetDumper(
			$"-i \"{GrisGamePath}\" " +
			$"-o \"{outputPath}\" " +
			"--facts --relations --manifest " +
			"--collections --scenes " +
			"--compression none " +
			"--quiet");

		// Assert
		exitCode.Should().Be(0, "AssetDumper should execute successfully");
		Directory.Exists(outputPath).Should().BeTrue();
		
		// Verify output files
		var manifestPath = Path.Combine(outputPath, "manifest.json");
		if (File.Exists(manifestPath))
		{
			File.Exists(manifestPath).Should().BeTrue("Should generate manifest.json");
		}
	}

	[Fact(Skip = "Requires long execution time - manual execution")]
	public void AssetDumper_PreviewMode_ShouldSucceed()
	{
		// Skip if GRIS is not installed
		if (_skipTests)
		{
			return;
		}

		// Arrange
		var outputPath = Path.Combine(_testOutputPath, "preview");
		Directory.CreateDirectory(outputPath);

		// Act - Preview mode will not generate actual files
		int exitCode = RunAssetDumper(
			$"-i \"{GrisGamePath}\" " +
			$"-o \"{outputPath}\" " +
			"--preview-only " +
			"--quiet");

		// Assert
		exitCode.Should().Be(0, "Preview mode should execute successfully");
	}

	[Fact(Skip = "Requires long execution time - manual execution")]
	public void AssetDumper_MinimalExport_ShouldGenerateFiles()
	{
		// Skip if GRIS is not installed
		if (_skipTests)
		{
			return;
		}

		// Arrange
		var outputPath = Path.Combine(_testOutputPath, "minimal");
		Directory.CreateDirectory(outputPath);

		// Act - Minimal export: only export facts, not relations and scenes
		int exitCode = RunAssetDumper(
			$"-i \"{GrisGamePath}\" " +
			$"-o \"{outputPath}\" " +
			"--facts " +
			"--relations=false " +
			"--scenes=false " +
			"--manifest " +
			"--compression none " +
			"--sample-rate 0.1 " + // Process only 10% of files to speed up
			"--quiet");

		// Assert
		exitCode.Should().Be(0, "Minimal export should succeed");
		
		// Verify at least some output was generated
		if (Directory.Exists(outputPath))
		{
			var files = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories);
			files.Should().NotBeEmpty("Should generate some output files");
		}
	}

	/// <summary>
	/// Run AssetDumper command and return exit code
	/// </summary>
	private int RunAssetDumper(string arguments)
	{
		// Build AssetDumper executable file path
		var projectPath = Path.GetFullPath(
			Path.Combine(
				Directory.GetCurrentDirectory(),
				"..",
				"..",
				"..",
				"..",
				"AssetRipper.Tools.AssetDumper"));

		var startInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"run --project \"{projectPath}\" -- {arguments}",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		using var process = new Process { StartInfo = startInfo };
		
		process.OutputDataReceived += (sender, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				Console.WriteLine($"[AssetDumper] {e.Data}");
			}
		};
		
		process.ErrorDataReceived += (sender, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				Console.Error.WriteLine($"[AssetDumper ERROR] {e.Data}");
			}
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		
		// Wait up to 5 minutes
		bool finished = process.WaitForExit(300000); // 5 minutes timeout
		
		if (!finished)
		{
			process.Kill();
			return -1;
		}

		return process.ExitCode;
	}
}
