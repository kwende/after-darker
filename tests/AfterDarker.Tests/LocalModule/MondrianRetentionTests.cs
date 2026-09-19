using System.Security.Cryptography;
using AfterDarker.Runtime;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianRetentionTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_MONDRIAN before enabling TestLocalMondrian."));

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(8)]
    public void RetentionDoesNotChangeGuestStatePixelsOrLifetimeCounts(int capacity)
    {
        byte[] file = ReadModule();
        using var recent = new MondrianSession(file, new(Speed: 100), diagnostics: new(capacity));
        using var full = new MondrianSession(file, new(Speed: 100), diagnostics: DiagnosticOptions.Full);
        recent.Initialize(); recent.Blank(); full.Initialize(); full.Blank();
        var earlier = recent.GetResult();
        long earlierCount = earlier.Diagnostics.TotalCalls;
        for (int i = 0; i < 30; i++) Assert.AreEqual(full.DrawFrame(), recent.DrawFrame());
        CollectionAssert.AreEqual(full.CopyPixels(), recent.CopyPixels());
        var expected = full.GetResult();
        var actual = recent.GetResult();
        Assert.AreEqual(expected.Instructions, actual.Instructions);
        Assert.AreEqual(expected.Calls.Count, (int)actual.Diagnostics.TotalCalls);
        Assert.AreEqual(expected.Phases.Count, (int)actual.Diagnostics.TotalPhases);
        Assert.AreEqual(3L, actual.Diagnostics.TotalInterrupts); // validation cannot depend on retained length
        Assert.AreEqual(Math.Min(capacity, expected.Calls.Count), actual.Calls.Count);
        foreach (var (left, right) in expected.Calls.TakeLast(capacity).Zip(actual.Calls))
        {
            Assert.AreEqual((left.Phase, left.Binding, left.Returned, left.Before, left.After),
                (right.Phase, right.Binding, right.Returned, right.Before, right.After));
            CollectionAssert.AreEqual(left.Arguments.ToArray(), right.Arguments.ToArray());
        }
        CollectionAssert.AreEqual(expected.Phases.TakeLast(capacity).ToArray(), actual.Phases.ToArray());
        CollectionAssert.AreEqual(expected.Interrupts.TakeLast(capacity).ToArray(), actual.Interrupts.ToArray());
        CollectionAssert.AreEquivalent(expected.Diagnostics.ImportCalls.ToArray(), actual.Diagnostics.ImportCalls.ToArray());
        Assert.AreEqual(earlierCount, earlier.Diagnostics.TotalCalls);
        Assert.AreEqual(earlierCount, earlier.Diagnostics.ImportCalls.Values.Sum(), "Earlier totals must not share a mutable dictionary.");
    }

    [TestMethod]
    public void FiveThousandDrawsKeepDefaultHistoryBoundedAndUseOneHostPixelBuffer()
    {
        using var session = new MondrianSession(ReadModule(), new(64, 48, Speed: 100));
        session.Initialize(); session.Blank();
        byte[] buffer = new byte[session.PixelByteCount];
        for (int i = 0; i < 5000; i++)
        {
            session.DrawFrame();
            session.CopyPixelsTo(buffer);
        }
        var result = session.GetResult();
        Assert.AreEqual(256, result.Calls.Count);
        Assert.AreEqual(256, result.Phases.Count);
        Assert.AreEqual(3, result.Interrupts.Count);
        Assert.AreEqual(5004L, result.Diagnostics.TotalPhases);
        Assert.IsTrue(result.Diagnostics.TotalCalls > 30_000);
        Assert.AreEqual(result.Diagnostics.TotalCalls, result.Diagnostics.ImportCalls.Values.Sum());
        Assert.AreEqual(9, result.Diagnostics.ImportCalls.Count);
        Assert.AreEqual(0, result.OutstandingLocks);
        CollectionAssert.AreEqual(session.CopyPixels(), buffer);
    }

    [TestMethod]
    public void SessionCopyReusesDestinationWithoutAllocationsAndRejectsInvalidBuffers()
    {
        using var session = new MondrianSession(ReadModule(), new(Speed: 100));
        session.Initialize(); session.Blank(); session.DrawFrame();
        byte[] buffer = new byte[session.PixelByteCount];
        for (int i = 0; i < 10; i++) session.CopyPixelsTo(buffer);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) session.CopyPixelsTo(buffer);
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        CollectionAssert.AreEqual(session.CopyPixels(), buffer);
        Assert.Throws<ArgumentException>(() => session.CopyPixelsTo(new byte[1]));
        Assert.AreEqual(MondrianSession.SessionState.Ready, session.State);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.CopyPixelsTo(buffer));
    }

    [TestMethod]
    public void CaptureSinkCanExplicitlyRetainACopyOfBorrowedPixels()
    {
        var retained = new List<byte[]>();
        var result = Tutorial09MondrianFrames.Capture(ReadModule(), 3, save: (frame, pixels) =>
        {
            Assert.AreEqual(frame.RgbSha256, Convert.ToHexString(SHA256.HashData(pixels)));
            retained.Add(pixels.ToArray());
        });
        for (int i = 0; i < retained.Count; i++)
            Assert.AreEqual(result.Frames[i].RgbSha256, Convert.ToHexString(SHA256.HashData(retained[i])));
        Assert.IsNull(result.Execution.Diagnostics.HistoryCapacity, "The bounded educational capture explicitly requests full history.");
    }
}
