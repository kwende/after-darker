using System.Security.Cryptography;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalStringTheory;

/// <summary>Opt-in original String Theory execution; the private file is read in place.</summary>
[TestClass]
[TestCategory("LocalStringTheory")]
public sealed class StringTheoryTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_STRING_THEORY")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_STRING_THEORY before enabling TestLocalStringTheory."));

    [TestMethod]
    public void ThreeGroupsOwnOneHistoryAllocationThroughRingMotionAndColorWraps()
    {
        using var session = (AfterDarkSession<StringTheoryState>)SupportedModules.Open(ReadModule(), new());
        var initial = session.Initialize().State;
        var heap = session.GetResult().LocalHeap!;
        var allocation = heap.Allocations.Single();
        Assert.AreEqual(4560, allocation.RequestedBytes);
        Assert.IsTrue(allocation.Moveable);
        Assert.AreEqual(initial.HistoryHandle, allocation.Handle);
        Assert.AreEqual(initial.HistoryOffset, allocation.Address.Offset);
        Assert.IsGreaterThan(heap.InitialBytes, heap.EnabledBytes);
        Assert.AreEqual(3, initial.GroupColors.Count);
        Assert.AreEqual((ushort)10, session.Blank().StoredAx);
        ushort[] colors = initial.GroupColors.ToArray();
        for (int frame = 1; frame <= 1500; frame++)
        {
            var state = session.DrawFrame().State;
            Assert.AreEqual((ushort)(frame % 100), state.HistoryIndex);
            Assert.AreEqual((ushort)(frame % 701), state.MotionCount);
            if (frame % 21 == 0)
                for (int group = 0; group < colors.Length; group++) colors[group] = colors[group] == 21 ? (ushort)1 : (ushort)(colors[group] + 1);
            CollectionAssert.AreEqual(colors, state.GroupColors.ToArray());
        }
        byte[] pixels = session.CopyPixels();
        Assert.IsTrue(pixels.Any(component => component != 0));
        var result = session.GetResult();
        Assert.AreEqual(9000L, result.Diagnostics.ImportCalls["GDI!LineTo (#19)"]);
        Assert.AreEqual(1L, result.Diagnostics.ImportCalls["KERNEL!LocalAlloc (#5)"]);
        Assert.AreEqual(0, result.LocalHeap!.OutstandingLocks);
        Assert.AreEqual(0, session.LivePenCount);
        Console.WriteLine($"String Theory: {result.Instructions} instructions, RGB {Convert.ToHexString(SHA256.HashData(pixels))}.");
        session.Shutdown(); session.Shutdown();
        Assert.AreEqual(0, session.GetPlaybackResult().LocalHeap!.Allocations.Count);
        Assert.AreEqual((ushort)0, session.GetResult().Phases[^2].State.HistoryHandle);
    }

    [TestMethod]
    [DataRow(3, 3)]
    [DataRow(2048, 2048)]
    [DataRow(321, 239)]
    public void DimensionBoundsDrawAndCleanUp(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int frame = 0; frame < 101; frame++) session.DrawFrame();
        session.Shutdown();
        Assert.AreEqual(0, session.GetPlaybackResult().LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, session.GetPlaybackResult().LivePens);
    }

    [TestMethod]
    public void IndependentGuestsMatchAcrossHistoryWrapAndRetainSeparateHeaps()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int frame = 0; frame < 130; frame++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels, secondPixels);
        }
        first.Shutdown();
        Assert.AreEqual(1, second.GetPlaybackResult().LocalHeap!.Allocations.Count);
        second.DrawFrame(); second.Shutdown();
        Assert.AreEqual(0, second.GetPlaybackResult().LocalHeap!.Allocations.Count);
    }

    [TestMethod]
    public void EmptyPlaybackCleansUpAndInvalidDimensionsOrVersionsAreRejected()
    {
        byte[] file = ReadModule();
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown(); session.Shutdown();
        var result = session.GetPlaybackResult();
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(2, 480)));
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(640, 1)));
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }
}
