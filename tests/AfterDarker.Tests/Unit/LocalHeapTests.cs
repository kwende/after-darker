using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class LocalHeapTests
{
    private const ushort Selector = 0x28, Start = 0x100;

    [TestMethod]
    public void FixedOffsetsAndMoveableIdentitiesHaveDifferentLockSemantics()
    {
        var heap = Create(64);
        ushort fixedHandle = heap.Allocate(LocalMemoryFlags.Fixed, 7);
        ushort moveableHandle = heap.Allocate(LocalMemoryFlags.Moveable, 9);
        Assert.AreEqual(Start, fixedHandle);
        Assert.AreEqual(fixedHandle, heap.Lock(fixedHandle));
        Assert.AreEqual((ushort)(Start + 8), heap.Lock(moveableHandle));
        Assert.AreNotEqual(moveableHandle, heap.Lock(moveableHandle));
        Assert.AreEqual(2, heap.Snapshot().OutstandingLocks);
        Assert.AreEqual((ushort)1, heap.Unlock(moveableHandle));
        Assert.AreEqual((ushort)0, heap.Unlock(moveableHandle));
        Assert.AreEqual((ushort)0, heap.Unlock(moveableHandle));
        Assert.AreEqual((ushort)0, heap.Unlock(fixedHandle));
        Assert.AreEqual(44, heap.Snapshot().FreeBytes); // Aligned 8 + 12 payload extents.
    }

    [TestMethod]
    public void FreePreservesBackingBytesAndZeroInitClearsReusedPayloadOnly()
    {
        var memory = new Memory();
        var heap = Create(64, memory: memory);
        ushort handle = heap.Allocate(LocalMemoryFlags.Moveable, 7);
        ushort offset = heap.Lock(handle);
        memory.Write(new(Selector, offset), [1, 2, 3, 4, 5, 6, 7, 0xAA]);
        heap.Unlock(handle); heap.Free(handle);
        Assert.AreEqual((byte)7, memory.Read(new(Selector, offset), 7)[^1]);
        ushort fixedHandle = heap.Allocate(LocalMemoryFlags.Fixed, 7);
        Assert.AreEqual(offset, fixedHandle);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, memory.Read(new(Selector, fixedHandle), 7));
        heap.Free(fixedHandle);
        ushort zeroed = heap.Allocate(LocalMemoryFlags.ZeroInit, 7);
        CollectionAssert.AreEqual(new byte[7], memory.Read(new(Selector, zeroed), 7));
        Assert.AreEqual((byte)0xAA, memory.Read(new(Selector, (ushort)(zeroed + 7)), 1)[0]);
    }

    [TestMethod]
    public void FragmentationCausesFailureAndAdjacentFreesCoalesceForLargerAllocation()
    {
        var heap = Create(48);
        ushort first = heap.Allocate(0, 16), middle = heap.Allocate(0, 16), last = heap.Allocate(0, 16);
        heap.Free(first); heap.Free(last);
        Assert.AreEqual(32, heap.Snapshot().FreeBytes);
        Assert.AreEqual(16, heap.Snapshot().LargestFreeBlock);
        Assert.AreEqual((ushort)0, heap.Allocate(0, 24));
        Assert.AreEqual(middle, heap.Lock(middle));
        heap.Free(middle);
        Assert.AreEqual(48, heap.Snapshot().LargestFreeBlock);
        Assert.AreEqual(Start, heap.Allocate(0, 48));
        Assert.AreEqual((ushort)0, heap.Allocate(0, 1));
    }

    [TestMethod]
    public void GrowthUsesOnlyMappedCapacityAndDoesNotMoveAnExistingLockedBlock()
    {
        var heap = Create(16, 48);
        ushort handle = heap.Allocate(LocalMemoryFlags.Moveable, 12);
        ushort address = heap.Lock(handle);
        var beforeGrowth = heap.Snapshot();
        Assert.AreEqual(16, beforeGrowth.EnabledBytes);
        Assert.AreNotEqual((ushort)0, heap.Allocate(0, 32));
        Assert.AreEqual(48, heap.Snapshot().EnabledBytes);
        Assert.AreEqual(address, heap.Lock(handle));
        Assert.AreEqual((ushort)0, heap.Allocate(0, 8));
        Assert.AreEqual(16, beforeGrowth.EnabledBytes); // Detached snapshot did not mutate.
        Assert.AreEqual(1, beforeGrowth.OutstandingLocks);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(65532)]
    public void NullAndUnalignedStartsNeverPublishNullOrWrappedFixedPointers(int start)
    {
        var memory = new Memory();
        var heap = new Win16LocalHeap(memory, new(new(Selector, (ushort)start), 65536 - start));
        ushort handle = heap.Allocate(0, 4);
        Assert.AreNotEqual((ushort)0, handle);
        Assert.AreEqual(0, handle % 4);
        Assert.IsGreaterThanOrEqualTo(start, (int)handle);
        Assert.IsLessThanOrEqualTo(65536, handle + 4);
    }

    [TestMethod]
    public void LastByteAllocationUsesIntArithmeticBeforeCheckedWordConversion()
    {
        var heap = new Win16LocalHeap(new Memory(), new(new(Selector, 65532), 4));
        Assert.AreEqual((ushort)65532, heap.Allocate(0, 4));
        Assert.AreEqual((ushort)0, heap.Allocate(0, 65535));
        Assert.AreEqual((ushort)0, heap.Allocate(0, 1));
        heap.Free(65532);
        Assert.AreEqual((ushort)65532, heap.Allocate(0, 1));
    }

    [TestMethod]
    public void ZeroSizeFixedReturnsNullAndMoveableReturnsADiscardedIdentity()
    {
        var heap = Create(32);
        Assert.AreEqual((ushort)0, heap.Allocate(0, 0));
        ushort handle = heap.Allocate(LocalMemoryFlags.Moveable, 0);
        Assert.AreNotEqual((ushort)0, handle);
        Assert.AreEqual((ushort)0, heap.Lock(handle));
        Assert.AreEqual((ushort)0, heap.Unlock(handle));
        Assert.AreEqual(32, heap.Snapshot().FreeBytes);
        Assert.AreEqual((ushort)0, heap.Free(handle));
        Assert.AreEqual(0, heap.Snapshot().Allocations.Count);
    }

    [TestMethod]
    public void LockCountSaturatesLikeTheWin16ReferenceAndLockedLocalBlocksCanBeFreed()
    {
        var heap = Create(32);
        ushort handle = heap.Allocate(LocalMemoryFlags.Moveable, 8);
        for (int count = 0; count < 300; count++) heap.Lock(handle);
        Assert.AreEqual(254, heap.Snapshot().OutstandingLocks);
        Assert.AreEqual((ushort)253, heap.Unlock(handle));
        Assert.AreEqual((ushort)0, heap.Free(handle));
        Assert.AreEqual(0, heap.Snapshot().OutstandingLocks);
        Assert.AreEqual(32, heap.Snapshot().FreeBytes);
    }

    [TestMethod]
    public void InvalidFlagsHandlesAndWrongDataSelectorFailWithoutPublishingAnAddress()
    {
        var heap = Create(32);
        Assert.Throws<NotSupportedException>(() => heap.Allocate((LocalMemoryFlags)0x100, 4));
        Assert.Throws<InvalidOperationException>(() => heap.Lock(42));
        Assert.Throws<InvalidOperationException>(() => heap.Unlock(42));
        Assert.Throws<InvalidOperationException>(() => heap.Free(42));
        Assert.Throws<NotSupportedException>(() => heap.RequireOwner(0x48));
        heap.RequireOwner(Selector);
        ushort handle = heap.Allocate(0, 4);
        heap.Free(handle);
        Assert.Throws<InvalidOperationException>(() => heap.Free(handle));
        Assert.AreEqual(0, heap.Snapshot().Allocations.Count);
        Assert.AreEqual(32, heap.Snapshot().FreeBytes);
        Assert.AreEqual((ushort)0, heap.Lock(0));
        Assert.AreEqual((ushort)0, heap.Unlock(0));
        Assert.AreEqual((ushort)0, heap.Free(0));
    }

    [TestMethod]
    public void InvalidCapacityAndUnmappedBackingAreRejectedBeforeAllocation()
    {
        var memory = new Memory();
        Assert.Throws<ArgumentException>(() => new Win16LocalHeap(memory, new(new(Selector, Start), 32), 16));
        Assert.Throws<ArgumentException>(() => new Win16LocalHeap(memory, new(new(Selector, Start), 32), 65536));
        Assert.Throws<InvalidOperationException>(() => new Win16LocalHeap(memory, new(new(0x48, Start), 32)));
        Assert.Throws<ArgumentException>(() => new Win16LocalHeap(memory, new(new(Selector, Start), 0)));
    }

    [TestMethod]
    public void ApiRequiresSuccessfulLocalInitAndSeparatesSimultaneousGuestOwnership()
    {
        var first = new Win16Api(new Win16ApiState(new Memory(), new(new(Selector, Start), 32)));
        var second = new Win16Api(new Win16ApiState(new Memory(), new(new(Selector, Start), 32), localInitSucceeds: false));
        Assert.Throws<InvalidOperationException>(() => first.LocalAlloc(Selector, 0, 4));
        Assert.IsTrue(first.LocalInit(Selector, 0, 32));
        Assert.IsFalse(second.LocalInit(Selector, 0, 32));
        Assert.Throws<InvalidOperationException>(() => second.LocalAlloc(Selector, 0, 4));
        Assert.Throws<NotSupportedException>(() => first.LocalAlloc(0x48, 0, 4));
        ushort handle = first.LocalAlloc(Selector, LocalMemoryFlags.Moveable, 8);
        Assert.Throws<NotSupportedException>(() => first.LocalLock(0x48, handle));
        Assert.Throws<NotSupportedException>(() => first.LocalUnlock(0x48, handle));
        Assert.Throws<NotSupportedException>(() => first.LocalFree(0x48, handle));
        Assert.AreEqual(new FarPointer16(Selector, Start), first.LocalLock(Selector, handle));
        first.LocalFree(Selector, handle);
        Assert.AreEqual(0, first.State.LocalHeap!.Snapshot().Allocations.Count);
    }

    [TestMethod]
    public void RepeatedReuseKeepsMetadataBoundedAndDoesNotOverlapLivePayloads()
    {
        var heap = Create(64);
        ushort survivor = heap.Allocate(LocalMemoryFlags.Moveable, 16);
        ushort survivorOffset = heap.Lock(survivor);
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            ushort handle = heap.Allocate(LocalMemoryFlags.Moveable, 48);
            Assert.AreNotEqual(survivor, handle);
            Assert.AreEqual((ushort)(survivorOffset + 16), heap.Lock(handle));
            heap.Free(handle);
        }
        Assert.AreEqual(1, heap.Snapshot().Allocations.Count);
        Assert.AreEqual(48, heap.Snapshot().FreeBytes);
        Assert.AreEqual(survivorOffset, heap.Lock(survivor));
    }

    private static Win16LocalHeap Create(int bytes, int? capacity = null, Memory? memory = null)
        => new(memory ?? new Memory(), new(new(Selector, Start), bytes), capacity);

    private sealed class Memory : IGuestMemory16
    {
        private readonly byte[] bytes = new byte[65536];
        public byte[] Read(FarPointer16 address, int count)
        {
            if (address.Selector != Selector || count < 0 || address.Offset + (long)count > bytes.Length)
                throw new InvalidOperationException("Unmapped test guest memory.");
            return bytes.AsSpan(address.Offset, count).ToArray();
        }
        public void Write(FarPointer16 address, byte[] value)
        {
            _ = Read(address, value.Length);
            value.CopyTo(bytes, address.Offset);
        }
    }
}
