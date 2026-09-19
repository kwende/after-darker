using System.Security.Cryptography;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianSessionTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_MONDRIAN before enabling TestLocalMondrian."));

    [TestMethod]
    public void HostCanReturnBetweenCallsWithoutLosingGuestStateOrMutatingEarlierSnapshots()
    {
        using var session = new MondrianSession(ReadModule(), new(Speed: 100));
        Assert.AreEqual(MondrianSession.SessionState.Loaded, session.State);
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
        Assert.Throws<InvalidOperationException>(() => session.Blank());
        Assert.Throws<InvalidOperationException>(() => session.GetResult());
        Assert.AreEqual(MondrianSession.SessionState.Loaded, session.State);

        var initialized = session.Initialize();
        Assert.AreEqual((ushort)0, initialized.State.Rectangles);
        Assert.AreEqual(MondrianSession.SessionState.Initialized, session.State);
        Assert.Throws<InvalidOperationException>(() => session.Initialize());
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame()); // host must BLANK first
        Assert.AreEqual(MondrianSession.SessionState.Initialized, session.State);
        session.Blank();
        Assert.AreEqual(MondrianSession.SessionState.Ready, session.State);

        var beforeDrawing = session.GetResult();
        var first = session.DrawFrame();
        byte[] pixels = session.CopyPixels();
        string firstHash = Convert.ToHexString(SHA256.HashData(pixels));
        Assert.AreEqual("3259528906BA8AF2F8E56ECC66CE6CC9A4A55D151A7516C4E024037C5E0F91E3", firstHash);
        var second = session.DrawFrame();
        Assert.AreEqual((ushort)1, first.State.Rectangles);
        Assert.AreEqual((ushort)2, second.State.Rectangles);
        Assert.AreEqual(firstHash, Convert.ToHexString(SHA256.HashData(pixels)), "Later drawing must not mutate a returned pixel array.");
        Assert.AreNotEqual(firstHash, Convert.ToHexString(SHA256.HashData(session.CopyPixels())));
        Assert.AreEqual(4, beforeDrawing.Phases.Count, "Later calls must not append to an earlier diagnostic result.");
        Assert.AreEqual(6, session.GetResult().Phases.Count);
        Assert.AreEqual(0, session.GetResult().OutstandingLocks);
        session.Dispose();
        Assert.AreEqual(4, beforeDrawing.Phases.Count); // detached observations survive native disposal
    }

    [TestMethod]
    public void InitializationOnlySessionKeepsLesson08Boundary()
    {
        using var session = new MondrianSession(ReadModule(), enableDrawing: false);
        session.Initialize();
        Assert.AreEqual(3, session.GetResult().Phases.Count);
        Assert.AreEqual(11, session.GetResult().Calls.Count);
        Assert.Throws<InvalidOperationException>(() => session.Blank());
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
        Assert.Throws<InvalidOperationException>(() => session.CopyPixels());
        Assert.AreEqual(MondrianSession.SessionState.Initialized, session.State);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void DisposalIsIdempotentAndRejectsAllFurtherNativeAccess(int phase)
    {
        using var session = new MondrianSession(ReadModule());
        if (phase >= 1) session.Initialize();
        if (phase >= 2) session.Blank();
        session.Dispose();
        session.Dispose();
        Assert.AreEqual(MondrianSession.SessionState.Disposed, session.State);
        Assert.Throws<ObjectDisposedException>(() => session.Initialize());
        Assert.Throws<ObjectDisposedException>(() => session.Blank());
        Assert.Throws<ObjectDisposedException>(() => session.DrawFrame());
        Assert.Throws<ObjectDisposedException>(() => session.CopyPixels());
        Assert.Throws<ObjectDisposedException>(() => session.GetResult());
        Assert.Throws<ObjectDisposedException>(() => _ = session.LastDrawingOperation);
    }

    [TestMethod]
    public void InterruptedInitializationFaultsSessionAndCannotBeRetried()
    {
        using var session = new MondrianSession(ReadModule(), instructionLimit: 10);
        Assert.Throws<InvalidOperationException>(() => session.Initialize());
        Assert.AreEqual(MondrianSession.SessionState.Faulted, session.State);
        Assert.Throws<InvalidOperationException>(() => session.Initialize());
        Assert.Throws<InvalidOperationException>(() => session.Blank());
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
        session.Dispose();
        Assert.AreEqual(MondrianSession.SessionState.Disposed, session.State);
    }

    [TestMethod]
    public void FailureDuringDrawFaultsSessionInsteadOfReusingAnInterruptedGuest()
    {
        using var output = new FailingWriter();
        using var session = new MondrianSession(ReadModule(), output: output);
        session.Initialize();
        session.Blank();
        output.Fail = true; // Fail in host dispatch, after the original guest has started its call.
        Assert.Throws<IOException>(() => session.DrawFrame());
        Assert.AreEqual(MondrianSession.SessionState.Faulted, session.State);
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
        Assert.Throws<InvalidOperationException>(() => session.CopyPixels());
        session.Dispose();
        Assert.AreEqual(MondrianSession.SessionState.Disposed, session.State);
    }

    [TestMethod]
    public void TwoLiveSessionsKeepIndependentGuestsAndSurfaces()
    {
        byte[] file = ReadModule();
        using var first = new MondrianSession(file, new(Speed: 100));
        using var second = new MondrianSession(file, new(Speed: 100));
        first.Initialize(); first.Blank(); first.DrawFrame();
        byte[] firstPixels = first.CopyPixels();
        first.DrawFrame();
        second.Initialize(); second.Blank();
        Assert.AreEqual((ushort)1, second.DrawFrame().State.Rectangles);
        CollectionAssert.AreEqual(firstPixels, second.CopyPixels());
        first.Dispose();
        Assert.AreEqual((ushort)2, second.DrawFrame().State.Rectangles);
    }

    private sealed class FailingWriter : StringWriter
    {
        public bool Fail { get; set; }
        public override void WriteLine(string? value)
        {
            if (Fail) throw new IOException("Simulated host output failure.");
            base.WriteLine(value);
        }
    }
}
