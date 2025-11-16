using AssetRipper.Tools.AssetDumper.Helpers;

namespace AssetRipper.Tools.AssetDumper.Tests.Unit.Core;

/// <summary>
/// Unit tests for ExportHelper hash functions and ID generation.
/// Tests FNV-1a hash stability, collision resistance, and ID generation.
/// </summary>
public class ExportHelperHashTests
{
	[Fact]
	public void ComputeStableHash_WithSameInput_ShouldReturnSameHash()
	{
		// Arrange
		string input = "TestString123";

		// Act
		string hash1 = ExportHelper.ComputeStableHash(input);
		string hash2 = ExportHelper.ComputeStableHash(input);

		// Assert
		hash1.Should().Be(hash2);
		hash1.Should().HaveLength(8); // 32-bit hash = 8 hex chars
	}

	[Fact]
	public void ComputeStableHash_WithDifferentInputs_ShouldReturnDifferentHashes()
	{
		// Arrange
		string input1 = "TestString1";
		string input2 = "TestString2";

		// Act
		string hash1 = ExportHelper.ComputeStableHash(input1);
		string hash2 = ExportHelper.ComputeStableHash(input2);

		// Assert
		hash1.Should().NotBe(hash2);
	}

	[Fact]
	public void ComputeStableHash_WithEmptyString_ShouldReturnValidHash()
	{
		// Arrange
		string input = string.Empty;

		// Act
		string hash = ExportHelper.ComputeStableHash(input);

		// Assert
		hash.Should().NotBeNullOrEmpty();
		hash.Should().HaveLength(8);
		hash.Should().MatchRegex("^[0-9A-F]{8}$"); // Uppercase hex
	}

	[Theory]
	[InlineData("")]
	[InlineData("a")]
	[InlineData("Test")]
	[InlineData("LongStringWithManyCharacters123456789")]
	[InlineData("SpecialChars!@#$%^&*()")]
	[InlineData("UnicodeTest测试")]
	public void ComputeStableHash_WithVariousInputs_ShouldReturnValidHash(string input)
	{
		// Act
		string hash = ExportHelper.ComputeStableHash(input);

		// Assert
		hash.Should().NotBeNullOrEmpty();
		hash.Should().HaveLength(8);
		hash.Should().MatchRegex("^[0-9A-F]{8}$");
	}

	[Fact]
	public void ComputeStableHash_Stability_AcrossMultipleRuns()
	{
		// Arrange
		string input = "StabilityTestString";
		HashSet<string> hashes = new HashSet<string>();

		// Act - Run 1000 times
		for (int i = 0; i < 1000; i++)
		{
			string hash = ExportHelper.ComputeStableHash(input);
			hashes.Add(hash);
		}

		// Assert - All hashes should be identical
		hashes.Should().HaveCount(1);
	}

	[Fact]
	public void ComputeStableHash_CollisionTest_With10000RandomStrings()
	{
		// Arrange
		Random random = new Random(42); // Fixed seed for reproducibility
		HashSet<string> hashes = new HashSet<string>();
		int stringCount = 10000;

		// Act
		for (int i = 0; i < stringCount; i++)
		{
			string input = GenerateRandomString(random, 20);
			string hash = ExportHelper.ComputeStableHash(input);
			hashes.Add(hash);
		}

		// Assert
		// With 32-bit hash and 10k strings, collision probability is ~1.16%
		// We expect at least 99% unique hashes
		double uniqueRatio = (double)hashes.Count / stringCount;
		uniqueRatio.Should().BeGreaterThan(0.99, 
			because: "FNV-1a 32-bit should have <1% collision rate for 10k strings");
	}

	[Fact]
	public void ComputeStableHash_CollisionTest_WithSimilarStrings()
	{
		// Arrange - Test with very similar strings (differ by one character)
		List<string> inputs = new List<string>
		{
			"Assets/Scene1.unity",
			"Assets/Scene2.unity",
			"Assets/Scene3.unity",
			"Assets/Scene4.unity",
			"Assets/Scene5.unity",
		};

		// Act
		HashSet<string> hashes = new HashSet<string>();
		foreach (string input in inputs)
		{
			string hash = ExportHelper.ComputeStableHash(input);
			hashes.Add(hash);
		}

		// Assert - All should be unique despite similarity
		hashes.Should().HaveCount(inputs.Count);
	}

	[Theory]
	[InlineData("TestString", "TESTSTRING")] // Case sensitivity
	[InlineData("TestString", "TestString ")] // Trailing space
	[InlineData("Test String", "TestString")] // Space difference
	public void ComputeStableHash_WithMinimalDifferences_ShouldProduceDifferentHashes(
		string input1, string input2)
	{
		// Act
		string hash1 = ExportHelper.ComputeStableHash(input1);
		string hash2 = ExportHelper.ComputeStableHash(input2);

		// Assert
		hash1.Should().NotBe(hash2, 
			because: "even minimal differences should produce different hashes");
	}

	[Fact]
	public void ComputeStableHash_FNV1aAlgorithm_KnownTestVector()
	{
		// Arrange - FNV-1a test vectors
		// FNV-1a 32-bit for empty string should be 0x811C9DC5 (initial offset)
		// After processing empty string: 0x811C9DC5
		
		// Known FNV-1a 32-bit hash for "a" is 0xE40C292C
		string input = "a";

		// Act
		string hash = ExportHelper.ComputeStableHash(input);

		// Assert
		// FNV-1a for "a": (0x811C9DC5 ^ 'a') * 0x01000193 = 0xE40C292C
		hash.Should().Be("E40C292C", 
			because: "FNV-1a 32-bit hash of 'a' should match known test vector");
	}

	[Fact]
	public void ComputeStableHash_PerformanceTest_10000Hashes()
	{
		// Arrange
		List<string> inputs = Enumerable.Range(0, 10000)
			.Select(i => $"Collection_{i}_Path/To/File.asset")
			.ToList();

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		foreach (string input in inputs)
		{
			ExportHelper.ComputeStableHash(input);
		}
		stopwatch.Stop();

		// Assert
		stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
			because: "10k hash operations should complete in <100ms");
	}

	[Fact]
	public void ComputeStableHash_ThreadSafety_ParallelExecution()
	{
		// Arrange
		string input = "ThreadSafetyTest";
		int iterations = 1000;
		
		// Act
		var hashes = new System.Collections.Concurrent.ConcurrentBag<string>();
		Parallel.For(0, iterations, i =>
		{
			string hash = ExportHelper.ComputeStableHash(input);
			hashes.Add(hash);
		});

		// Assert
		hashes.Should().HaveCount(iterations);
		hashes.Distinct().Should().HaveCount(1, 
			because: "all parallel executions should produce the same hash");
	}

	// Helper method
	private static string GenerateRandomString(Random random, int length)
	{
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/._-";
		char[] result = new char[length];
		for (int i = 0; i < length; i++)
		{
			result[i] = chars[random.Next(chars.Length)];
		}
		return new string(result);
	}
}
