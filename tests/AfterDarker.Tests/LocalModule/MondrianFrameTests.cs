using AfterDarker.Core.AfterDark;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianFrameTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_MONDRIAN before enabling TestLocalMondrian."));

    [TestMethod]
    public void OriginalThirtyFramesRepeatExactlyAndEveryGuestCallBalancesItsStack()
    {
        byte[] file = ReadModule();
        var first = Tutorial09MondrianFrames.Capture(file);
        var second = Tutorial09MondrianFrames.Capture(file);
        Assert.AreEqual(30, first.Frames.Count);
        Assert.AreEqual(30, first.DrawCalls);
        CollectionAssert.AreEqual(first.Frames.ToArray(), second.Frames.ToArray());
        Assert.AreEqual(0, first.Execution.OutstandingLocks);
        Assert.IsTrue(first.Execution.ProtectedMode);
        Assert.IsTrue(first.Execution.Phases.All(p => p.Registers.Sp == 0x1000 && p.Registers.Ss == 0x40 && p.Registers.Bp == 0));
        Assert.IsTrue(first.Frames.All(f => f.ChangedPixels > 0));
        Assert.AreEqual((ushort)30, first.Frames[^1].GuestState.Rectangles);
        Assert.AreEqual("3259528906BA8AF2F8E56ECC66CE6CC9A4A55D151A7516C4E024037C5E0F91E3", first.Frames[0].RgbSha256);
        Assert.AreEqual(new AfterDarker.Core.Win16.Rectangle16(367, 430, 307, 284), first.Frames[0].LastOperation!.Rectangle);
        Assert.AreEqual(31, first.Execution.Calls.Count(c => c.Binding.Name.Contains("!SetRect ")));
        Assert.AreEqual(30, first.Execution.Calls.Count(c => c.Binding.Name.Contains("!InvertRect ")));
    }

    [TestMethod]
    public void LongerRunExercisesOriginalRectangleRemovalAndKeepsRunningBeyondOldLifetimeBudget()
    {
        var result = Tutorial09MondrianFrames.Capture(ReadModule(), 180);
        Assert.IsTrue(result.Frames.Zip(result.Frames.Skip(1)).Any(pair => pair.Second.GuestState.Rectangles < pair.First.GuestState.Rectangles),
            "The guest must have entered its removal path, not just accumulated rectangles.");
        Assert.IsTrue(result.Execution.Instructions > 50_000);
        Assert.AreEqual(0, result.Execution.OutstandingLocks);
    }

    [TestMethod]
    public void SlowOptionCountsChangedImagesInsteadOfEmptyDrawCalls()
    {
        var result = Tutorial09MondrianFrames.Capture(ReadModule(), 2, new(Speed: 50));
        Assert.AreEqual(2, result.Frames.Count);
        Assert.IsTrue(result.DrawCalls > result.Frames.Count);
    }

    [TestMethod]
    public void DrawBudgetFailsWithoutClaimingRequestedFramesWereCaptured()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Tutorial09MondrianFrames.Capture(ReadModule(), 2, maximumDrawCalls: 1));
        StringAssert.Contains(error.Message, "1/2 changed images");
    }
}
