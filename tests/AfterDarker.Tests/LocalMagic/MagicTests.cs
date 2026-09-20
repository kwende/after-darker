using System.Security.Cryptography;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalMagic;

/// <summary>Original Magic execution, opt-in with a private module read in place.</summary>
[TestClass]
[TestCategory("LocalMagic")]
public sealed class MagicTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MAGIC")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_MAGIC before enabling TestLocalMagic."));

    [TestMethod]
    public void OriginalLineHistoryWrapsChangesColorAndMotionThenReleasesItsHeap()
    {
        using var session = (AfterDarkSession<MagicState>)SupportedModules.Open(ReadModule(), new());
        session.Initialize();
        var initialized = session.GetResult();
        var heap = initialized.LocalHeap!;
        var allocation = heap.Allocations.Single();
        var state = initialized.Phases[^1].State;
        Assert.AreEqual(1024, heap.InitialBytes);
        Assert.AreEqual(1520, allocation.RequestedBytes); // Guest always reserves 152 ten-byte records.
        Assert.IsGreaterThan(heap.InitialBytes, heap.EnabledBytes);
        Assert.IsTrue(allocation.Moveable);
        Assert.AreEqual(allocation.Handle, state.HistoryHandle);
        Assert.AreEqual(allocation.Address.Offset, state.HistoryOffset);
        Assert.AreNotEqual(allocation.Handle, allocation.Address.Offset);
        Assert.AreEqual((ushort)100, state.HistoryLength);
        Assert.AreEqual((ushort)1, state.Mirror);
        Assert.AreEqual(0, heap.OutstandingLocks);
        Assert.AreEqual((ushort)10, session.Blank().StoredAx);

        for (int frame = 1; frame <= 1700; frame++)
        {
            var drawn = session.DrawFrame();
            Assert.AreEqual((ushort)(frame % 100), drawn.State.HistoryIndex);
            Assert.AreEqual((ushort)(frame % 701), drawn.State.MotionCount);
            // In true color, the original table cycles through indices 1..21.
            Assert.AreEqual((ushort)(1 + frame / 76 % 21), drawn.State.ColorIndex);
        }
        byte[] pixels = session.CopyPixels();
        Assert.IsTrue(pixels.Any(component => component != 0));
        var running = session.GetResult();
        Assert.AreEqual(1, running.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, running.LocalHeap.OutstandingLocks);
        Assert.AreEqual(0, session.LivePenCount);
        Assert.AreEqual(1, session.PeakPenCount);
        Assert.AreEqual(1L, running.Diagnostics.ImportCalls["KERNEL!LocalAlloc (#5)"]);
        Assert.AreEqual(1701L, running.Diagnostics.ImportCalls["KERNEL!LocalLock (#8)"]);
        Assert.AreEqual(1701L, running.Diagnostics.ImportCalls["KERNEL!LocalUnlock (#9)"]);
        Assert.AreEqual(6800L, running.Diagnostics.ImportCalls["GDI!LineTo (#19)"]); // Two erased + two drawn per frame.
        Assert.IsLessThanOrEqualTo(256, running.Calls.Count);
        Assert.IsLessThanOrEqualTo(256, running.Phases.Count);
        Console.WriteLine($"Magic after 1700 draws: {running.Instructions} instructions, " +
            $"6800 lines, heap {heap.InitialBytes}->{heap.EnabledBytes}, " +
            $"handle {allocation.Handle:X4}->{allocation.Address}, pixel hash {Convert.ToHexString(SHA256.HashData(pixels))}.");
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.AreEqual((ushort)0, session.GetResult().Phases[^2].State.HistoryHandle);
        Assert.AreEqual(1L, session.GetPlaybackResult().ImportCalls["KERNEL!LocalFree (#7)"]);
        Assert.IsTrue(session.CopyPixels().All(component => component == 0)); // Original CLOSE clears its surface.
    }

    [TestMethod]
    public void IndependentGuestsProduceIdenticalFramesAcrossHistoryWrapAndSurviveOtherGuestsClosing()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int frame = 0; frame < 160; frame++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels, secondPixels);
        }
        Assert.IsTrue(firstPixels.Any(component => component != 0));
        first.Shutdown();
        Assert.AreEqual(1, second.GetPlaybackResult().LocalHeap!.Allocations.Count);
        second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(3, 3)]
    [DataRow(2048, 2048)]
    [DataRow(321, 239)]
    public void SupportedDimensionsDrawAndReleaseAllocations(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int frame = 0; frame < 101; frame++) session.DrawFrame();
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void EmptyPlaybackReleasesHistoryAndUnsupportedDimensionsOrArtifactsAreRejected()
    {
        byte[] file = ReadModule();
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(2, 480)));
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(640, 1)));
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static void AssertCleanShutdown(PlaybackResult result)
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
}
