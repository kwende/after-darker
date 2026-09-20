using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class LocalHeapExecutionTests
{
    [TestMethod]
    public void RealGuestUsesReturnedNearPointerAndEveryHeapImportRestoresItsPascalFrame()
    {
        var result = new Tutorial10LocalHeap().Execute();
        Assert.AreEqual((ushort)0xBEEF, result.GuestStoredValue);
        Assert.AreEqual((ushort)0xBEEF, result.HostObservedValue);
        Assert.AreEqual((ushort)0xBEEF, result.ReusedValue);
        Assert.AreEqual((ushort)0, result.ZeroInitializedValue);
        Assert.AreNotEqual(result.Handle, result.Address.Offset);
        Assert.AreEqual((ushort)0x18, result.Address.Selector);
        Assert.AreEqual(1, result.WhileLocked.OutstandingLocks);
        Assert.AreEqual(0, result.AfterFree.Allocations.Count);
        var allocation = result.Calls[1];
        CollectionAssert.AreEqual(new ushort[] { 0x42, 32 }, allocation.Arguments.ToArray());
        Assert.AreEqual(result.Handle, allocation.After.Ax);
        Assert.AreEqual(result.Handle, allocation.After.Cx);
        Assert.AreEqual(allocation.Before.Dx, allocation.After.Dx);
        var locked = result.Calls[2];
        Assert.AreEqual(result.Address.Offset, locked.After.Ax);
        Assert.AreEqual(result.Address.Selector, locked.After.Dx);
        Assert.AreEqual((ushort)0, result.Calls[3].After.Ax); // Last Unlock's zero is success.
        Assert.AreEqual((ushort)0, result.Calls[4].After.Ax); // Free's zero is success.
        foreach (var call in result.Calls)
        {
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
            Assert.AreEqual((ushort)8, call.After.Cs);
            Assert.AreEqual((ushort)0x18, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es);
            Assert.AreEqual(call.Before.Ss, call.After.Ss);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
        }
        Assert.AreEqual((ushort)0x1000, result.FinalRegisters.Sp);
    }
}
