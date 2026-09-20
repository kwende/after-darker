using AfterDarker.Core.Win16;
using AfterDarker.Runtime;
using System.Security.Cryptography;

namespace AfterDarker.Tests.LocalZot;

/// <summary>Opt-in Zot! execution; private input is never copied to test output.</summary>
[TestClass]
[TestCategory("LocalZot")]
public sealed class ZotTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_ZOT")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_ZOT before enabling TestLocalZot."));

    [TestMethod]
    public void RepeatedStrikesAllocateDrawEraseAndFreeBeforeReturning()
    {
        using var session = (AfterDarkSession<ZotState>)SupportedModules.Open(ReadModule(), new(),
            timing: new(new SteppingWin16Clock(0, 2500), AfterDarkSession<ZotState>.CivilTime));
        int visibleImages = 0, erasedImages = 0;
        session.IntermediateFrameReady += (pixels, _) =>
        {
            if (pixels.ContainsAnyExcept((byte)0)) visibleImages++; else erasedImages++;
        };
        session.Initialize(); session.Blank();
        long maximumInstructions = 0, maximumServices = 0;
        var previous = session.GetResult();
        for (int frame = 0; frame < 30; frame++)
        {
            var state = session.DrawFrame().State;
            var current = session.GetResult();
            maximumInstructions = Math.Max(maximumInstructions, current.Instructions - previous.Instructions);
            maximumServices = Math.Max(maximumServices, current.Diagnostics.TotalCalls - previous.Diagnostics.TotalCalls);
            Assert.AreEqual((ushort)0, state.MainHandle);
            Assert.AreEqual((ushort)0, state.ForkHandle);
            Assert.AreEqual(0, current.LocalHeap!.Allocations.Count);
            Assert.AreEqual(current.LocalHeap.FreeBytes, current.LocalHeap.LargestFreeBlock);
            // Zot!'s static data ends at 03E6; four-byte fixed-handle alignment
            // leaves two prefix bytes outside the allocatable free range.
            Assert.AreEqual(2, current.LocalHeap.EnabledBytes - current.LocalHeap.FreeBytes);
            Assert.AreEqual(0, session.LivePenCount);
            Assert.IsTrue(session.CopyPixels().All(component => component == 0));
            previous = current;
        }
        var result = session.GetResult();
        Assert.IsGreaterThan(0L, result.Diagnostics.ImportCalls["GDI!LineTo (#19)"]);
        Assert.AreEqual(60L, result.Diagnostics.ImportCalls["KERNEL!LocalAlloc (#5)"]);
        Assert.AreEqual(60L, result.Diagnostics.ImportCalls["KERNEL!LocalFree (#7)"]);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.IsGreaterThanOrEqualTo(30, visibleImages);
        Assert.IsGreaterThanOrEqualTo(30, erasedImages);
        Assert.AreEqual((long)(visibleImages + erasedImages), session.GetPlaybackResult().IntermediateFrames);
        Console.WriteLine($"Zot!: {result.Instructions} instructions; max per strike {maximumInstructions} instructions / {maximumServices} imports; " +
            $"{visibleImages} visible + {erasedImages} erased checkpoint images.");
        session.Shutdown(); session.Shutdown();
        AssertClean(session.GetPlaybackResult());
    }

    [TestMethod]
    public void ExactDeadlineWaitsThenFixedAllocationsAreUsedAndFreedByTheOriginalStrike()
    {
        var clock = new ControlledClock();
        using var session = (AfterDarkSession<ZotState>)SupportedModules.Open(ReadModule(), new(),
            timing: new(clock, AfterDarkSession<ZotState>.CivilTime), diagnostics: DiagnosticOptions.Full);
        uint deadline = session.Initialize().State.NextStrike;
        session.Blank();
        clock.Now = deadline - 1; session.DrawFrame();
        clock.Now = deadline; session.DrawFrame();
        Assert.IsFalse(session.GetResult().Diagnostics.ImportCalls.ContainsKey("KERNEL!LocalAlloc (#5)"));
        Assert.AreEqual(0L, session.GetPlaybackResult().IntermediateFrames);
        clock.Now = deadline + 1; session.DrawFrame();
        var allocations = session.GetResult().Calls.Where(call => call.Binding.Implementation == Win16Imports.Handler.LocalAlloc).ToArray();
        Assert.AreEqual(2, allocations.Length);
        CollectionAssert.AreEqual(new ushort[] { 0, 800 }, allocations[0].Arguments.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0, 1600 }, allocations[1].Arguments.ToArray());
        var locks = session.GetResult().Calls.Where(call => call.Binding.Implementation == Win16Imports.Handler.LocalLock).ToArray();
        Assert.AreEqual(2, locks.Length);
        for (int block = 0; block < 2; block++)
        {
            Assert.AreEqual(allocations[block].Returned, locks[block].Returned & 0xFFFF); // Fixed handle IS its near offset.
            Assert.AreEqual(0x28u, locks[block].Returned >> 16);
        }
        session.Shutdown(); AssertClean(session.GetPlaybackResult());
    }

    [TestMethod]
    public void IndependentGuestsProduceIdenticalIntermediateImagesNotJustIdenticalBlackFinalFrames()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240), timing: FastTime());
        using var second = SupportedModules.Open(file, new(320, 240), timing: FastTime());
        var firstHashes = new List<string>(); var secondHashes = new List<string>();
        first.IntermediateFrameReady += (pixels, _) => firstHashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
        second.IntermediateFrameReady += (pixels, _) => secondHashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        for (int strike = 0; strike < 3; strike++) { first.DrawFrame(); second.DrawFrame(); }
        Assert.IsGreaterThan(1, firstHashes.Distinct().Count());
        CollectionAssert.AreEqual(firstHashes, secondHashes);
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertClean(first.GetPlaybackResult()); AssertClean(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(1, 1, 0)]
    [DataRow(2048, 2048, 1)]
    [DataRow(321, 239, 2)]
    public void DimensionBoundsAndDifferentSeedsFinishStrikesAndReleaseStorage(int width, int height, int dayOffset)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height), timing: FastTime(dayOffset));
        session.Initialize(); session.Blank();
        for (int strike = 0; strike < 3; strike++) session.DrawFrame();
        session.Shutdown(); AssertClean(session.GetPlaybackResult());
    }

    [TestMethod]
    public async Task CancellingWhileALightningImageIsPresentedStillFinishesTheCallAndRunsClose()
    {
        byte[] file = ReadModule();
        using var stop = new CancellationTokenSource();
        var mailbox = new LatestFrameMailbox(640 * 480 * 3);
        Task<PlaybackResult> running = AfterDarkPlayback.RunAsync(file, new(), mailbox, stop.Token);
        byte[] pixels = new byte[640 * 480 * 3];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        bool sawLightning = false;
        try
        {
            while (!sawLightning)
            {
                if (running.IsCompleted) await running; // Surface a symbolic guest failure immediately.
                sawLightning = mailbox.TryCopyTo(pixels, out _) && pixels.Any(component => component != 0);
                if (!sawLightning) await Task.Delay(10, deadline.Token);
            }
        }
        finally
        {
            stop.Cancel();
            await running.WaitAsync(TimeSpan.FromSeconds(10)); // Also join the worker on a timeout/assertion failure.
        }
        var result = await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(sawLightning);
        Assert.IsGreaterThan(0L, result.IntermediateFrames);
        AssertClean(result);
    }

    [TestMethod]
    public void EmptyPlaybackAndUnknownRevisionKeepTheSameLifecycleBoundary()
    {
        byte[] file = ReadModule();
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown(); session.Shutdown();
        AssertClean(session.GetPlaybackResult());
        Assert.AreEqual(0L, session.GetPlaybackResult().IntermediateFrames);
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static SessionTiming FastTime(int dayOffset = 0) =>
        new(new SteppingWin16Clock(0, 2500), AfterDarkSession<ZotState>.CivilTime.AddDays(dayOffset));

    [TestMethod]
    public void IntermediatePresenterCannotReenterOrDisposeTheUnfinishedGuestCall()
    {
        using var session = SupportedModules.Open(ReadModule(), new(), timing: FastTime());
        int callbacks = 0;
        session.IntermediateFrameReady += (_, _) =>
        {
            callbacks++;
            Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
            Assert.Throws<InvalidOperationException>(() => session.Shutdown());
            Assert.Throws<InvalidOperationException>(() => session.Dispose());
        };
        session.Initialize(); session.Blank(); session.DrawFrame();
        Assert.IsGreaterThan(0, callbacks);
        session.Shutdown(); AssertClean(session.GetPlaybackResult());
    }

    private static void AssertClean(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }

    private sealed class ControlledClock : IWin16Clock
    {
        public uint Now { get; set; }
        public uint GetTickCount() => Now;
    }
}
