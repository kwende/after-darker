using System.Buffers.Binary;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class BitmapResourceTests
{
    [TestMethod]
    public void ResourceLessonReturnsTypedBitmapsUsingTheProductionCatalogAndDecoder()
    {
        var fixture = BitmapResourceFixture.ResourceFile();
        var decoded = AfterDarker.Tutorials.Lessons.Tutorial11BitmapResources.DecodeBitmaps(fixture.Image,
            new NeResourceCatalog(fixture.File, fixture.Image));
        Assert.AreEqual((ushort)1, decoded.Single().Resource.Id.Number);
        Assert.AreEqual(3, decoded.Single().Bitmap.Width);
        Assert.IsTrue(decoded.Single().Bitmap.IsMonochrome);
    }
    [TestMethod]
    [DataRow(1, false, false)]
    [DataRow(4, false, false)]
    [DataRow(8, false, false)]
    [DataRow(24, false, false)]
    [DataRow(32, false, false)]
    [DataRow(1, true, false)]
    [DataRow(4, true, false)]
    [DataRow(8, true, false)]
    [DataRow(24, true, false)]
    [DataRow(32, true, false)]
    [DataRow(1, false, true)]
    [DataRow(4, false, true)]
    [DataRow(8, false, true)]
    [DataRow(24, false, true)]
    public void DecodeNormalizesOrientationPaddingAndPackedIndexes(int depth, bool topDown, bool core)
    {
        DecodedBitmap bitmap = DibBitmapDecoder.Decode(BitmapResourceFixture.Dib(depth, topDown, core));
        Assert.AreEqual(3, bitmap.Width); Assert.AreEqual(2, bitmap.Height); Assert.AreEqual(depth, bitmap.SourceBitsPerPixel);
        CollectionAssert.AreEqual(new byte[] { 255, 255, 255, 0, 0, 0, 255, 255, 255, 0, 0, 0, 255, 255, 255, 0, 0, 0 }, bitmap.Rgb);
    }

    [TestMethod]
    public void DirectAndPaletteBgrChannelsBecomeRgbWithoutTreatingFourthByteAsAlpha()
    {
        foreach (int depth in new[] { 8, 24, 32 })
        {
            byte[] dib = BitmapResourceFixture.Dib(depth, topDown: true);
            int firstColor = depth == 8 ? 44 : 40;
            dib[firstColor] = 11; dib[firstColor + 1] = 22; dib[firstColor + 2] = 33;
            CollectionAssert.AreEqual(new byte[] { 33, 22, 11 }, DibBitmapDecoder.Decode(dib).Rgb[..3]);
        }
    }

    [TestMethod]
    public void BadHeadersCompressionAndLengthsFailExplicitly()
    {
        Assert.Throws<NotSupportedException>(() => DibBitmapDecoder.Decode(BitmapResourceFixture.Dib(32, core: true)));
        byte[] valid = BitmapResourceFixture.Dib();
        foreach (int length in new[] { 0, 11, 39, valid.Length - 1 })
            Assert.Throws<InvalidDataException>(() => DibBitmapDecoder.Decode(valid[..length]));
        byte[] compressed = (byte[])valid.Clone(); compressed[16] = 1;
        Assert.Throws<NotSupportedException>(() => DibBitmapDecoder.Decode(compressed));
        byte[] unknown = (byte[])valid.Clone(); unknown[0] = 108;
        Assert.Throws<NotSupportedException>(() => DibBitmapDecoder.Decode(unknown));
        foreach (int dimension in new[] { 0, 2049, int.MinValue })
        {
            byte[] invalid = (byte[])valid.Clone(); BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(8), dimension);
            Assert.Throws<InvalidDataException>(() => DibBitmapDecoder.Decode(invalid));
        }
        byte[] missingColor = (byte[])valid.Clone(); missingColor[32] = 1;
        // Palette now contains only index zero; the first pixel indexes entry one.
        missingColor[44] = 0x80;
        Assert.Throws<InvalidDataException>(() => DibBitmapDecoder.Decode(missingColor));
    }

    [TestMethod]
    public void CatalogResolvesNamesAndAliasesCaseInsensitivelyAndOwnsItsBytes()
    {
        var fixture = BitmapResourceFixture.ResourceFile();
        var catalog = new NeResourceCatalog(fixture.File, fixture.Image);
        byte[] expected = BitmapResourceFixture.Dib(); Array.Fill(fixture.File, (byte)0);
        CollectionAssert.AreEqual(expected, catalog.Find(new(2, null), new(null, "sprite"))!);
        CollectionAssert.AreEqual(expected, catalog.Find(new(2, null), new(1, null))!);
        byte[] detached = catalog.Find(new(2, null), new(1, null))!; detached[0] = 0;
        CollectionAssert.AreEqual(expected, catalog.Find(new(2, null), new(1, null))!);
        Assert.IsNull(catalog.Find(new(2, null), new(null, "missing")));
        Assert.AreEqual((ushort)1, catalog.Aliases.Single().IdNumber);

        var namedImage = fixture.Image with { Resources = new NeResource[] { new(new(null, "Custom"), new(null, "Thing"), 0, 1, 0) } };
        Assert.IsNotNull(new NeResourceCatalog(new byte[1], namedImage).Find(new(null, "CUSTOM"), new(null, "thing")));
    }

    [TestMethod]
    public void MalformedAliasAndOutOfFileDirectoryCannotEscapeTheirPayload()
    {
        var fixture = BitmapResourceFixture.ResourceFile();
        int aliasOffset = fixture.Image.Resources[1].FileOffset;
        fixture.File[aliasOffset] = 255;
        Assert.Throws<InvalidDataException>(() => new NeResourceCatalog(fixture.File, fixture.Image));
        fixture = BitmapResourceFixture.ResourceFile();
        fixture.File[aliasOffset + 13] = 65; // Remove terminating name NUL within its record.
        Assert.Throws<InvalidDataException>(() => new NeResourceCatalog(fixture.File, fixture.Image));
        Assert.Throws<InvalidDataException>(() => new NeResourceCatalog(new byte[1], fixture.Image));
        var duplicates = fixture.Image with { Resources = new[] { fixture.Image.Resources[0], fixture.Image.Resources[0] } };
        Assert.Throws<InvalidDataException>(() => new NeResourceCatalog(fixture.File, duplicates).Find(new(2, null), new(1, null)));
    }
}
