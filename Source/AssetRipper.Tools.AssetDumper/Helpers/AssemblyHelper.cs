using System.Security.Cryptography;
using System.Text;
using AsmResolver.DotNet;

namespace AssetRipper.Tools.AssetDumper.Helpers;

/// <summary>
/// Shared utilities for assembly operations across exporters.
/// </summary>
/// <remarks>
/// Consolidates duplicate code from:
/// AssemblyExporter, AssemblyDependencyExporter, ScriptTypeMappingExporter, TypeMemberExporter
/// </remarks>
public static class AssemblyHelper
{
	/// <summary>
	/// Gets the assembly name with null safety.
	/// </summary>
	public static string GetName(AssemblyDefinition assembly)
	{
		return assembly.Name ?? "Unknown";
	}

	/// <summary>
	/// Computes a stable GUID for an assembly based on its name.
	/// Uses SHA256 hash of assembly name for consistent, deterministic GUID generation.
	/// </summary>
	public static string ComputeGuid(AssemblyDefinition assembly)
	{
		string name = GetName(assembly);
		return ComputeGuidFromName(name);
	}

	/// <summary>
	/// Computes a stable GUID from an assembly name string.
	/// </summary>
	public static string ComputeGuidFromName(string assemblyName)
	{
		using SHA256 hash = SHA256.Create();
		byte[] hashBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(assemblyName));

		// Convert first 16 bytes to GUID format, then to uppercase hex string (32 chars)
		return new Guid(hashBytes.Take(16).ToArray()).ToString("N").ToUpperInvariant();
	}

	/// <summary>
	/// Computes SHA256 hash of a file.
	/// </summary>
	public static string ComputeFileSha256(string filePath)
	{
		using FileStream stream = File.OpenRead(filePath);
		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(stream);
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	/// <summary>
	/// Counts the number of types in an assembly.
	/// </summary>
	public static int CountTypes(AssemblyDefinition assembly)
	{
		int count = 0;
		foreach (ModuleDefinition module in assembly.Modules)
		{
			count += CountTypesRecursive(module.TopLevelTypes);
		}
		return count;
	}

	/// <summary>
	/// Gets the DLL file name for an assembly.
	/// </summary>
	public static string GetDllFileName(AssemblyDefinition assembly)
	{
		return $"{GetName(assembly)}.dll";
	}

	/// <summary>
	/// Checks if an assembly name matches common system/framework assemblies to filter.
	/// </summary>
	public static bool IsSystemAssembly(string assemblyName)
	{
		return assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
			|| assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
			|| assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
			|| assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Checks if an assembly is a Unity engine assembly.
	/// </summary>
	public static bool IsUnityAssembly(string assemblyName)
	{
		return assemblyName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase)
			|| assemblyName.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase);
	}

	private static int CountTypesRecursive(IEnumerable<TypeDefinition> types)
	{
		int count = 0;
		foreach (TypeDefinition type in types)
		{
			count++;
			if (type.NestedTypes.Count > 0)
			{
				count += CountTypesRecursive(type.NestedTypes);
			}
		}
		return count;
	}
}
