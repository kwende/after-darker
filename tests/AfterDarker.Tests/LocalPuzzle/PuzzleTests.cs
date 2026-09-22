using AfterDarker.Core.Win16;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalPuzzle;

[TestClass]
[TestCategory("LocalPuzzle")]
public sealed class PuzzleTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_PUZZLE")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_PUZZLE to the analyzed AD30 Puzzle module."));

    [TestMethod]
    [DataRow(128, 128, 0)]
    [DataRow(321, 239, 13)]
    [DataRow(640, 480, 27)]
    public void OriginalPuzzleScrollsAndReturnsOutputThroughGuestMemoryWithCleanShutdown(int width, int height, int seedSeconds)
    {
        byte[] file = ReadModule();
        Assert.AreEqual("Puzzle", SupportedModules.Identify(file));
        using var session = (AfterDarkSession<BitmapModuleState>)SupportedModules.Open(file,
            new((short)width, (short)height),
            timing: SessionTiming.Deterministic() with { CivilTime = MondrianSession.CivilTime.AddSeconds(seedSeconds) },
            diagnostics: DiagnosticOptions.Full);
        session.Initialize(); session.Blank();
        byte[] initial = session.CopyPixels();
        for (int draw = 0; draw < 600; draw++)
        {
            var phase = session.DrawFrame();
            Assert.AreEqual((ushort)4096, phase.Registers.Sp);
            Assert.AreEqual(session.HostData, phase.Registers.Ds);
        }
        Assert.IsFalse(initial.AsSpan().SequenceEqual(session.CopyPixels()));
        var calls = session.GetResult().Calls;
        var scrolls = calls.Where(call => call.Binding.Implementation == Win16Imports.Handler.ScrollDC).ToArray();
        Assert.IsGreaterThan(0, scrolls.Length);
        Assert.IsTrue(scrolls.All(call => call.Returned == 1));
        Assert.IsTrue(scrolls.All(call => call.Arguments[7] == 0)); // No HRGN; exposed RECT is used instead.
        Assert.IsTrue(scrolls.All(call => call.Arguments[8] == 0x28 && call.Arguments[9] == 0x386));
        Assert.IsTrue(scrolls.Any(call => unchecked((short)call.Arguments[1]) < 0 || unchecked((short)call.Arguments[2]) < 0));
        Assert.IsTrue(scrolls.Any(call => unchecked((short)call.Arguments[1]) > 0 || unchecked((short)call.Arguments[2]) > 0));
        if (width == 321) Assert.IsTrue(calls.Any(call => call.Binding.Implementation == Win16Imports.Handler.CopyRect));
        session.Shutdown();
        var result = session.GetPlaybackResult();
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LiveBitmaps + result.LiveMemoryDcs + result.LivePens + result.LiveBrushes + result.LiveRegions);
    }

    [TestMethod]
    public void MaximumSurfaceCompletesItsBoundedLifecycle()
    {
        using var session = SupportedModules.Open(ReadModule(), new(2048, 2048));
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 10; draw++) session.DrawFrame();
        session.Shutdown();
        Assert.AreEqual(0, session.GetPlaybackResult().OutstandingLocks);
    }
}
