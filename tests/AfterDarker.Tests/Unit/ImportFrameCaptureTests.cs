using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.Unit;

/// <summary>Source-only presentation tests: matching, ownership and bounds, without private module data.</summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ImportFrameCaptureTests
{
    private static readonly NeImport DeletePen = new("GDI", 69, null);
    private static readonly FarPointer16 Resume = new(0x10, 0x80);
    private static readonly ImportFrameCheckpoint Point = new(new(2, 0x80), DeletePen, TimeSpan.FromMilliseconds(80));

    [TestMethod]
    public void OnlyTheSpecifiedPhaseImportAndReturnAddressOfferUnmodifiedSurfacePixels()
    {
        var capture = new ImportFrameCapture([Point], _ => Resume, 12);
        var surface = new PixelSurface(2, 2);
        surface.Paint(new(0, 0, 2, 2), invert: true);
        byte[]? observed = null;
        void Present(ReadOnlySpan<byte> pixels, TimeSpan hold)
        {
            observed = pixels.ToArray();
            Assert.AreEqual(TimeSpan.FromMilliseconds(80), hold);
        }
        capture.AfterImport("CLOSE", Resume, DeletePen, surface, Present);
        capture.AfterImport("DRAWFRAME", new(0x10, 0x81), DeletePen, surface, Present);
        capture.AfterImport("DRAWFRAME", Resume, new("GDI", 45, null), surface, Present);
        Assert.IsNull(observed);
        Assert.AreEqual(0L, capture.TotalVisits);
        capture.AfterImport("DRAWFRAME", Resume, DeletePen, surface, Present);
        CollectionAssert.AreEqual(surface.CopyRgb(), observed!);
        surface.Paint(new(0, 0, 2, 2), invert: false);
        Assert.IsTrue(observed!.All(component => component == 255)); // Consumer's detached copy survives reuse.
        Assert.IsTrue(surface.CopyRgb().All(component => component == 0));
    }

    [TestMethod]
    public void PerDrawBudgetResetsAndRemainsEnforcedWithoutASubscriber()
    {
        var capture = new ImportFrameCapture([Point], _ => Resume, 12);
        var surface = new PixelSurface(2, 2);
        for (int visit = 0; visit < 32; visit++) capture.AfterImport("DRAWFRAME", Resume, DeletePen, surface, null);
        Assert.Throws<InvalidOperationException>(() => capture.AfterImport("DRAWFRAME", Resume, DeletePen, surface, null));
        capture.BeginDraw();
        capture.AfterImport("DRAWFRAME", Resume, DeletePen, surface, null);
        Assert.AreEqual(33L, capture.TotalVisits);
    }

    [TestMethod]
    public void OversizeBuffersUnboundedHoldsAndDuplicateLocationsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImportFrameCapture([Point], _ => Resume, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImportFrameCapture([Point], _ => Resume, 2048 * 2048 * 3 + 1));
        Assert.Throws<ArgumentException>(() => new ImportFrameCapture([Point with { MinimumDisplayTime = TimeSpan.FromSeconds(1) }], _ => Resume, 12));
        Assert.Throws<ArgumentException>(() => new ImportFrameCapture([Point, Point], _ => Resume, 12));
    }
}
