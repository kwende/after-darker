using System.Buffers.Binary;
using System.IO.Compression;
using AfterDarker.Core.Rendering;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class PngWriterTests
{
    [TestMethod]
    public void PngContainsCorrectDimensionsAndLosslessRgbScanlines()
    {
        // Asymmetric colors catch RGB/BGR swaps, row flips, and row-padding bugs.
        byte[] rgb = [255, 0, 0, 0, 255, 0, 0, 0, 255, 1, 2, 3, 40, 50, 60, 253, 254, 255];
        using var png = new MemoryStream();
        PngWriter.Write(png, 3, 2, rgb);
        byte[] bytes = png.ToArray();
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.AreEqual(3, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
        Assert.AreEqual(2, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
        Assert.AreEqual((byte)8, bytes[24]); Assert.AreEqual((byte)2, bytes[25]);
        CollectionAssert.AreEqual("IDAT"u8.ToArray(), bytes[37..41]);
        int compressedLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(33));
        using var encoded = new MemoryStream(bytes, 41, compressedLength);
        using var zlib = new ZLibStream(encoded, CompressionMode.Decompress);
        using var decoded = new MemoryStream(); zlib.CopyTo(decoded);
        CollectionAssert.AreEqual(new byte[] { 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 1, 2, 3, 40, 50, 60, 253, 254, 255 }, decoded.ToArray());
        // Known PNG IEND chunk, including its published CRC32 value.
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }, bytes[^12..]);
    }

    [TestMethod]
    public void InvalidDimensionsOrTruncatedPixelsAreRejectedBeforeWriting()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => PngWriter.Write(stream, 2, 2, new byte[11]));
        Assert.Throws<ArgumentException>(() => PngWriter.Write(stream, -1, 1, []));
        Assert.AreEqual(0L, stream.Length);
    }
}
