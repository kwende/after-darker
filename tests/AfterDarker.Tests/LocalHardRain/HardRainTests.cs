using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalHardRain;

/// <summary>Opt-in execution of the owner's original Hard Rain artifact, read in place.</summary>
[TestClass]
[TestCategory("LocalHardRain")]
public sealed class HardRainTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_HARD_RAIN")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_HARD_RAIN before enabling TestLocalHardRain."));

    [TestMethod]
    public void OriginalDropsGrowSwitchPenWidthEraseAndRegenerateWithBoundedState()
    {
        using var session = (AfterDarkSession<HardRainState>)SupportedModules.Open(ReadModule(), new());
        session.Initialize();
        HardRainState previous = session.GetResult().Phases[^1].State;
        Assert.AreEqual((ushort)5, previous.DropCount);
        Assert.AreEqual((ushort)20, previous.DropSize);
        Assert.AreEqual((ushort)0, session.Blank().StoredAx);
        int regenerated = 0;
        var observedWidths = new HashSet<ushort>();
        for (int draw = 1; draw <= 1000; draw++)
        {
            HardRainState current = session.DrawFrame().State;
            Assert.AreEqual((ushort)(draw % 5), current.NextDrop);
            for (int dropIndex = 0; dropIndex < current.Drops.Count; dropIndex++)
            {
                var before = previous.Drops[dropIndex];
                var after = current.Drops[dropIndex];
                if (dropIndex != previous.NextDrop) Assert.AreEqual(before, after);
                else if (before.Radius + 3 > before.MaximumRadius)
                {
                    regenerated++;
                    Assert.AreEqual(4u, after.Radius);
                    Assert.IsTrue(after.MaximumRadius is >= 7 and <= 26);
                    Assert.IsTrue(after.CenterX >= 0 && after.CenterX < 640 && after.CenterY >= 0 && after.CenterY < 480);
                }
                else Assert.AreEqual(before with { Radius = before.Radius + 3 }, after);
            }
            var penCall = session.GetResult().Calls.Last(call => call.Binding.Implementation == AfterDarker.Core.Win16.Win16Imports.Handler.CreatePen);
            var drawnDrop = previous.Drops[previous.NextDrop];
            Assert.AreEqual(drawnDrop.Radius <= drawnDrop.MaximumRadius / 2 ? (ushort)2 : (ushort)1, penCall.Arguments[1]);
            Assert.AreEqual(drawnDrop.Color, (uint)penCall.Arguments[2] << 16 | penCall.Arguments[3]);
            observedWidths.Add(penCall.Arguments[1]);
            Assert.AreEqual(0, session.LivePenCount);
            previous = current;
        }
        Assert.IsGreaterThan(100, regenerated);
        Assert.IsTrue(observedWidths.SetEquals([1, 2]));
        var result = session.GetPlaybackResult();
        Assert.AreEqual(1000L, result.ImportCalls["GDI!CreatePen (#61)"]);
        Assert.AreEqual(1000L, result.ImportCalls["GDI!DeleteObject (#69)"]);
        Assert.AreEqual(1000L + regenerated, result.ImportCalls["GDI!Ellipse (#24)"]);
        Assert.IsLessThanOrEqualTo(256, result.RetainedCalls);
        Assert.AreEqual(1, result.PeakPens);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.IsTrue(session.CopyPixels().Any(component => component != 0));
        Console.WriteLine($"Hard Rain: {result.Instructions} instructions, {regenerated} regenerations, {result.ImportCalls["GDI!Ellipse (#24)"]} ellipses.");
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void IndependentGuestsMatchAndIgnoreTheUnrelatedSpeedOption()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int draw = 0; draw < 200; draw++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            Assert.IsTrue(firstPixels.AsSpan().SequenceEqual(secondPixels), $"Images differ at draw {draw}.");
        }
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(321, 239)]
    [DataRow(2048, 2048)]
    public void SupportedDimensionsClipRingsAndFinishCleanup(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 100; draw++) session.DrawFrame();
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void ShutdownWithoutDrawingAndArtifactRejectionRemainSafe()
    {
        byte[] file = ReadModule();
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static void AssertCleanShutdown(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }
}
