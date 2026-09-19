using AfterDarker.Runtime;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianTimingTests
{
    [TestMethod]
    public void OriginalDrawingProgressesWithTimeAdvancingBetweenHostCalls()
    {
        var time = new ManualTimeProvider();
        byte[] file = File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")!);
        using var session = new MondrianSession(file, new(Speed: 100), timing: SessionTiming.Live(time));
        Assert.AreEqual(0u, session.Initialize().State.Tick);
        session.Blank();
        for (int i = 0; i < 100; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(16));
            session.DrawFrame();
        }
        Assert.IsTrue(session.CopyPixels().Any(b => b != 0));
        Assert.AreEqual(0, session.GetResult().OutstandingLocks);
    }
}
