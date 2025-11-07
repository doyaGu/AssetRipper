namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Helper methods for constructing export directory layout and manifest-friendly paths.
/// </summary>
internal static class OutputPathHelper
{
	public const string FactsDirectoryName = "facts";
	public const string RelationsDirectoryName = "relations";
	public const string IndexesDirectoryName = "indexes";
	public const string MetricsDirectoryName = "metrics";

	/// <summary>
	/// Returns a normalized relative path (forward slashes) to a shard file under the table directory.
	/// </summary>
	public static string GetShardRelativePath(string shardDirectory, string fileName)
	{
		if (string.IsNullOrWhiteSpace(shardDirectory))
		{
			throw new ArgumentException("Shard directory cannot be null or empty", nameof(shardDirectory));
		}

		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
		}

		string combined = Path.Combine(shardDirectory, fileName);
		return NormalizeRelativePath(combined);
	}

	/// <summary>
	/// Normalizes a relative path to use forward slashes for manifest readability.
	/// </summary>
	public static string NormalizeRelativePath(string relativePath)
	{
		if (string.IsNullOrEmpty(relativePath))
		{
			return relativePath;
		}

		return relativePath.Replace('\\', '/');
	}

	/// <summary>
	/// Combines path segments and returns a normalized relative path with forward slashes.
	/// </summary>
	public static string CombineRelative(params string[] segments)
	{
		string combined = Path.Combine(segments);
		return NormalizeRelativePath(combined);
	}

	/// <summary>
	/// Resolves a normalized relative path to an absolute on-disk path rooted at <paramref name="root"/>.
	/// </summary>
	public static string ResolveAbsolutePath(string root, string relativePath)
	{
		if (string.IsNullOrWhiteSpace(root))
		{
			throw new ArgumentException("Root cannot be null or empty", nameof(root));
		}

		if (string.IsNullOrWhiteSpace(relativePath))
		{
			throw new ArgumentException("Relative path cannot be null or empty", nameof(relativePath));
		}

		string osRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
		return Path.Combine(root, osRelative);
	}

	/// <summary>
	/// Ensures a subdirectory exists under the export root and returns its absolute path.
	/// </summary>
	public static string EnsureSubdirectory(string root, string subdirectory)
	{
		if (string.IsNullOrWhiteSpace(root))
		{
			throw new ArgumentException("Root cannot be null or empty", nameof(root));
		}

		if (string.IsNullOrWhiteSpace(subdirectory))
		{
			throw new ArgumentException("Subdirectory cannot be null or empty", nameof(subdirectory));
		}

		string absolute = Path.Combine(root, subdirectory);
		Directory.CreateDirectory(absolute);
		return absolute;
	}
}
