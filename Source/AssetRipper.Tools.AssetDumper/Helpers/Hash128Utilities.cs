using AssetRipper.SourceGenerated.Subclasses.Hash128;

namespace AssetRipper.Tools.AssetDumper.Helpers;

internal static class Hash128Utilities
{
	public static string ToLowerHex(in Hash128_5 hash)
	{
		uint data0 = hash.Bytes__0 | (uint)hash.Bytes__1 << 8 | (uint)hash.Bytes__2 << 16 | (uint)hash.Bytes__3 << 24;
		uint data1 = hash.Bytes__4 | (uint)hash.Bytes__5 << 8 | (uint)hash.Bytes__6 << 16 | (uint)hash.Bytes__7 << 24;
		uint data2 = hash.Bytes__8 | (uint)hash.Bytes__9 << 8 | (uint)hash.Bytes_10 << 16 | (uint)hash.Bytes_11 << 24;
		uint data3 = hash.Bytes_12 | (uint)hash.Bytes_13 << 8 | (uint)hash.Bytes_14 << 16 | (uint)hash.Bytes_15 << 24;
		return string.Create(32, (data0: data0, data1: data1, data2: data2, data3: data3), static (span, values) =>
		{
			WriteUInt32(span[..8], values.data0);
			WriteUInt32(span.Slice(8, 8), values.data1);
			WriteUInt32(span.Slice(16, 8), values.data2);
			WriteUInt32(span.Slice(24, 8), values.data3);
		});
	}

	private static void WriteUInt32(Span<char> span, uint value)
	{
		_ = value.TryFormat(span, out _, "x8");
	}
}
