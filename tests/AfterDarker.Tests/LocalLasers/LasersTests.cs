using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalLasers;

/// <summary>Original Lasers execution, opt-in with a private file read in place.</summary>
[TestClass]
[TestCategory("LocalLasers")]
public sealed class LasersTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_LASERS")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_LASERS before enabling TestLocalLasers."));

    [TestMethod]
    public void OriginalRayHistoryGrowsBeyondInitialHeapDrawsRegeneratesAndIsFreedOnClose()
    {
        using var session = (AfterDarkSession<LasersState>)SupportedModules.Open(ReadModule(), new());
        session.Initialize();
        var initialized = session.GetResult();
        var heap = initialized.LocalHeap!;
        var allocation = heap.Allocations.Single();
        var state = initialized.Phases[^1].State;
        Assert.AreEqual(1024, heap.InitialBytes);
        Assert.AreEqual(1836, allocation.RequestedBytes); // Three original 612-byte ray records.
        Assert.IsGreaterThan(heap.InitialBytes, heap.EnabledBytes);
        Assert.AreEqual(allocation.Handle, state.RayHistoryHandle);
        Assert.AreEqual(allocation.Address.Offset, state.RayHistoryOffset);
        Assert.AreNotEqual(allocation.Handle, allocation.Address.Offset);
        Assert.IsTrue(allocation.Moveable);
        Assert.AreEqual(0, heap.OutstandingLocks);
        Assert.AreEqual((ushort)10, session.Blank().StoredAx); // Documented HSV_PAL request in a true-color host.
        for (int frame = 0; frame < 1100; frame++) session.DrawFrame();
        byte[] pixels = session.CopyPixels();
        Assert.IsTrue(pixels.Any(component => component != 0));
        var running = session.GetResult();
        Assert.AreEqual(1, running.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, running.LocalHeap.OutstandingLocks);
        Assert.IsLessThan(1000u, running.Phases[^1].State.DrawCount); // Original periodic reset has executed.
        Assert.IsLessThanOrEqualTo(256, running.Calls.Count);
        Assert.AreEqual(1L, running.Diagnostics.ImportCalls["KERNEL!LocalAlloc (#5)"]);
        Console.WriteLine($"Lasers after 1100 draws: {running.Instructions} instructions, " +
            $"{running.Diagnostics.ImportCalls["GDI!LineTo (#19)"]} lines, heap {heap.InitialBytes}->{heap.EnabledBytes}, " +
            $"handle {allocation.Handle:X4}->{allocation.Address}, pixel hash {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))}.");
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.AreEqual((ushort)0, session.GetResult().Phases[^2].State.RayHistoryHandle);
        Assert.AreEqual(1L, session.GetPlaybackResult().ImportCalls["KERNEL!LocalFree (#7)"]);
    }

    [TestMethod]
    public void FreshGuestsHaveIndependentHeapsAndDeterministicFrames()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int frame = 0; frame < 40; frame++)
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
    [DataRow(141, 141)]
    [DataRow(2048, 2048)]
    [DataRow(321, 239)]
    public void SupportedDimensionsDrawAndReleaseAllocations(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int frame = 0; frame < 10; frame++) session.DrawFrame();
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void EmptyPlaybackStillReleasesHistoryAndRejectsUnsupportedInputs()
    {
        byte[] file = ReadModule();
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(140, 480)));
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
