namespace AssetRipper.Assets.Collections;

/// <summary>
/// Captures raw SerializedFile object table metadata for a single asset.
/// When AssetRipper does not expose the object table, the metadata is derived
/// from the available unity object information.
/// </summary>
public readonly struct SerializedObjectMetadata
{
	public SerializedObjectMetadata(
		long byteStart,
		int byteSize,
		int classId,
		int typeId,
		int serializedTypeIndex,
		int scriptTypeIndex,
		bool isStripped)
	{
		ByteStart = byteStart;
		ByteSize = byteSize;
		ClassId = classId;
		TypeId = typeId;
		SerializedTypeIndex = serializedTypeIndex;
		ScriptTypeIndex = scriptTypeIndex;
		IsStripped = isStripped;
	}

	/// <summary>
	/// Offset into the serialized data stream (relative to SerializedFile header data section).
	/// </summary>
	public long ByteStart { get; }

	/// <summary>
	/// Serialized object byte length.
	/// </summary>
	public int ByteSize { get; }

	/// <summary>
	/// Unity ClassID associated with this object.
	/// </summary>
	public int ClassId { get; }

	/// <summary>
	/// Serialized TypeID (matches ClassID when not MonoBehaviour).
	/// </summary>
	public int TypeId { get; }

	/// <summary>
	/// SerializedType index within the file, or -1 when unavailable.
	/// </summary>
	public int SerializedTypeIndex { get; }

	/// <summary>
	/// Script type index when applicable (MonoBehaviour etc.).
	/// </summary>
	public int ScriptTypeIndex { get; }

	/// <summary>
	/// Indicates whether the object was stripped during build.
	/// </summary>
	public bool IsStripped { get; }

	/// <summary>
	/// Creates metadata from an in-memory unity object when raw serialized metadata is unavailable.
	/// </summary>
	public static SerializedObjectMetadata FromAsset(IUnityObjectBase asset)
	{
		if (asset is null)
		{
			throw new ArgumentNullException(nameof(asset));
		}

		int classId = asset.ClassID;

		// Unity uses the class ID except for script-backed behaviours; we default to the class ID
		// because serialized type information is not exposed without modifying AssetRipper itself.
		int typeId = classId;

		// Script indices are not available without reading the object table directly.
		const int UnknownSerializedTypeIndex = -1;
		const int UnknownScriptTypeIndex = -1;

		return new SerializedObjectMetadata(
			byteStart: -1,
			byteSize: -1,
			classId: classId,
			typeId: typeId,
			serializedTypeIndex: UnknownSerializedTypeIndex,
			scriptTypeIndex: UnknownScriptTypeIndex,
			isStripped: false);
	}
}
