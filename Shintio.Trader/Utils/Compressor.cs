using System.IO.Compression;

namespace Shintio.Trader.Utils;

public static class Compressor
{
	public static byte[] Compress(byte[] data)
	{
		using var ms = new MemoryStream();

		using var compressor = new DeflateStream(ms, CompressionMode.Compress);
		compressor.Write(data, 0, data.Length);

		return ms.ToArray();
	}

	public static byte[] Decompress(byte[] compressedData)
	{
		using var compressedMs = new MemoryStream(compressedData);
		using var decompressedMs = new MemoryStream();

		using var decompressor = new DeflateStream(compressedMs, CompressionMode.Decompress);
		decompressor.CopyTo(decompressedMs);

		return decompressedMs.ToArray();
	}
}