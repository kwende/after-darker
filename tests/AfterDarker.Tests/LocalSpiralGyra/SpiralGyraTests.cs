using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalSpiralGyra;

[TestClass]
[TestCategory("LocalSpiralGyra")]
public sealed class SpiralGyraTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_SPIRAL_GYRA")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_SPIRAL_GYRA before enabling TestLocalSpiralGyra."));

    [TestMethod]
    [DataRow(0)]
    [DataRow(25)]
    [DataRow(50)]
    [DataRow(75)]
    [DataRow(100)]
    public void ExposedSpeedsProduceColorAndCleanShutdown(int speed)
    {
        using var session = SupportedModules.Open(ReadModule(), new(Speed: (ushort)speed));
        session.Initialize(); session.Blank();
        byte[] pixels = new byte[session.PixelByteCount];
        for (int i = 0; i < 30; i++) session.DrawFrame();
        session.CopyPixelsTo(pixels);
        Assert.IsTrue(Enumerable.Range(0, pixels.Length / 3).Any(i => pixels[i * 3] != pixels[i * 3 + 1]));
        session.Shutdown();
        Assert.AreEqual(0, session.GetPlaybackResult().LivePens);
    }

    [TestMethod]
    public void IndependentGuestsProduceIdenticalDeterministicFrames()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new());
        using var second = SupportedModules.Open(file, new());
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] a = new byte[first.PixelByteCount], b = new byte[second.PixelByteCount];
        for (int i = 0; i < 30; i++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(a); second.CopyPixelsTo(b);
            CollectionAssert.AreEqual(a, b);
        }
        first.Shutdown(); second.Shutdown();
        Assert.AreEqual(first.GetPlaybackResult().Instructions, second.GetPlaybackResult().Instructions);
    }

    [TestMethod]
    public void OriginalSpiralInitializesDrawsAndReleasesItsPens()
    {
        using var session = SupportedModules.Open(ReadModule(), new(), diagnostics: DiagnosticOptions.Full);
        session.Initialize(); session.Blank();
        byte[] pixels = new byte[session.PixelByteCount];
        for (int i = 0; i < 100; i++) session.DrawFrame();
        session.CopyPixelsTo(pixels);
        Assert.IsTrue(pixels.Any(b => b != 0));
        var before = session.GetPlaybackResult();
        Assert.AreEqual(2, before.LivePens);
        Assert.IsLessThanOrEqualTo(3, before.PeakPens);
        session.Shutdown();
        var after = session.GetPlaybackResult();
        Assert.AreEqual(0, after.LivePens);
        Assert.AreEqual(0, after.OutstandingLocks);
        Assert.AreEqual("WEP", after.Phases[^1].Name);
        Assert.AreEqual((ushort)1, after.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x50, after.Phases[^1].Registers.Ds);
        Assert.AreEqual((ushort)0x1000, after.Phases[^1].Registers.Sp);
        Console.WriteLine($"Spiral: {after.Instructions} instructions; peak pens {after.PeakPens}; {after.ImportCalls.Count} imports.");
    }
}
