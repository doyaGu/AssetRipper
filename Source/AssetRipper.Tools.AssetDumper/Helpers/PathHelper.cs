using System.Text;
using System.Text.RegularExpressions;

namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Shared utilities for path operations across exporters.
/// </summary>
/// <remarks>
/// Consolidates duplicate code from:
/// CollectionExporter, CollectionFactsExporter, SceneExporter
/// </remarks>
public static partial class PathHelper
{
	/// <summary>
	/// Normalizes a path by replacing backslashes with forward slashes.
	/// </summary>
	public static string? NormalizePath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		return path.Replace('\\', '/');
	}

	/// <summary>
	/// Beautifies a scene path for display by applying various transformations.
	/// </summary>
	public static string? BeautifyScenePath(string? rawPath)
	{
		if (string.IsNullOrWhiteSpace(rawPath))
		{
			return null;
		}

		string normalized = rawPath.Replace('\\', '/');
		string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

		if (segments.Length == 0)
		{
			return null;
		}

		List<string> beautified = new List<string>(segments.Length);

		for (int i = 0; i < segments.Length; i++)
		{
			string segment = segments[i];
			bool isLast = i == segments.Length - 1;
			string result = BeautifySegment(segment, isLast);

			if (!string.IsNullOrWhiteSpace(result))
			{
				beautified.Add(result);
			}
		}

		if (beautified.Count == 0)
		{
			return null;
		}

		string joined = string.Join("/", beautified);
		return CollapseSeparators(joined);
	}

	/// <summary>
	/// Beautifies a single path segment.
	/// </summary>
	public static string BeautifySegment(string segment, bool isLastSegment)
	{
		// Remove common prefixes
		segment = RemovePrefix(segment, "level");
		segment = RemovePrefix(segment, "sharedassets");
		segment = RemovePrefix(segment, "resources");

		// Remove numeric-only segments except for last
		if (!isLastSegment && int.TryParse(segment, out _))
		{
			return string.Empty;
		}

		// Remove .unity extension from last segment
		if (isLastSegment && segment.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
		{
			segment = segment[..^6];
		}

		// Remove common bundle suffixes
		segment = RemoveSuffix(segment, ".assets");
		segment = RemoveSuffix(segment, ".bundle");
		segment = RemoveSuffix(segment, ".ab");

		return segment.Trim();
	}

	/// <summary>
	/// Removes a prefix from a string if present (case-insensitive).
	/// </summary>
	public static string RemovePrefix(string input, string prefix)
	{
		if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			string remainder = input[prefix.Length..];
			// Also remove trailing numbers after prefix
			return TrimLeadingDigits(remainder);
		}
		return input;
	}

	/// <summary>
	/// Removes a suffix from a string if present (case-insensitive).
	/// </summary>
	public static string RemoveSuffix(string input, string suffix)
	{
		if (input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
		{
			return input[..^suffix.Length];
		}
		return input;
	}

	/// <summary>
	/// Collapses multiple consecutive separators and trims separators from ends.
	/// </summary>
	public static string CollapseSeparators(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return input;
		}

		StringBuilder sb = new StringBuilder(input.Length);
		bool lastWasSeparator = false;

		foreach (char c in input)
		{
			if (c == '/' || c == '\\')
			{
				if (!lastWasSeparator && sb.Length > 0)
				{
					sb.Append('/');
					lastWasSeparator = true;
				}
			}
			else
			{
				sb.Append(c);
				lastWasSeparator = false;
			}
		}

		// Trim trailing separator
		if (sb.Length > 0 && sb[^1] == '/')
		{
			sb.Length--;
		}

		return sb.ToString();
	}

	/// <summary>
	/// Trims leading digits from a string.
	/// </summary>
	private static string TrimLeadingDigits(string input)
	{
		int index = 0;
		while (index < input.Length && char.IsDigit(input[index]))
		{
			index++;
		}
		return input[index..];
	}

	/// <summary>
	/// Extracts the file name from a path without extension.
	/// </summary>
	public static string? GetFileNameWithoutExtension(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		string fileName = Path.GetFileNameWithoutExtension(path);
		return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
	}

	/// <summary>
	/// Gets the parent directory path.
	/// </summary>
	public static string? GetParentDirectory(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		string? parent = Path.GetDirectoryName(path);
		return string.IsNullOrWhiteSpace(parent) ? null : NormalizePath(parent);
	}
}
