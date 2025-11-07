using System.Globalization;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;

namespace AssetRipper.Tools.AssetDumper.Core;

/// <summary>
/// Assigns dense class keys for facts/assets and collects metadata for facts/types.
/// </summary>
public sealed class TypeDictionaryBuilder
{
	private readonly Dictionary<TypeDictionaryKey, TypeDictionaryEntry> _entries = new();
	private int _nextClassKey = 1;

	public int GetOrAdd(IUnityObjectBase asset, SerializedObjectMetadata metadata)
	{
		TypeDictionaryKey key = new(metadata.ClassId, metadata.TypeId, metadata.ScriptTypeIndex, metadata.IsStripped);

		if (_entries.TryGetValue(key, out TypeDictionaryEntry? existing) && existing is not null)
		{
			return existing.ClassKey;
		}

		string className = asset.ClassName ?? string.Empty;
		if (string.IsNullOrWhiteSpace(className))
		{
			className = $"ClassID_{metadata.ClassId.ToString(CultureInfo.InvariantCulture)}";
		}

		TypeDictionaryEntry entry = new TypeDictionaryEntry(
			classKey: _nextClassKey++,
			classId: metadata.ClassId,
			className: className,
			typeId: metadata.TypeId,
			scriptTypeIndex: metadata.ScriptTypeIndex >= 0 ? metadata.ScriptTypeIndex : (int?)null,
			isStripped: metadata.IsStripped);

		_entries.Add(key, entry);
		return entry.ClassKey;
	}

	public IReadOnlyCollection<TypeDictionaryEntry> Entries => _entries.Values;

	private readonly struct TypeDictionaryKey : IEquatable<TypeDictionaryKey>
	{
		public TypeDictionaryKey(int classId, int typeId, int scriptTypeIndex, bool isStripped)
		{
			ClassId = classId;
			TypeId = typeId;
			ScriptTypeIndex = scriptTypeIndex;
			IsStripped = isStripped;
		}

		public int ClassId { get; }
		public int TypeId { get; }
		public int ScriptTypeIndex { get; }
		public bool IsStripped { get; }

		public bool Equals(TypeDictionaryKey other)
		{
			return ClassId == other.ClassId
				&& TypeId == other.TypeId
				&& ScriptTypeIndex == other.ScriptTypeIndex
				&& IsStripped == other.IsStripped;
		}

		public override bool Equals(object? obj)
		{
			return obj is TypeDictionaryKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(ClassId, TypeId, ScriptTypeIndex, IsStripped);
		}
	}
}

/// <summary>
/// Captures type metadata corresponding to a classKey value.
/// </summary>
public sealed class TypeDictionaryEntry
{
	public TypeDictionaryEntry(int classKey, int classId, string className, int typeId, int? scriptTypeIndex, bool isStripped)
	{
		ClassKey = classKey;
		ClassId = classId;
		ClassName = className;
		TypeId = typeId;
		ScriptTypeIndex = scriptTypeIndex;
		IsStripped = isStripped ? true : null;
	}

	public int ClassKey { get; }

	public int ClassId { get; }

	public string ClassName { get; }

	public int TypeId { get; }

	public int? ScriptTypeIndex { get; }

	public bool? IsStripped { get; }
}
