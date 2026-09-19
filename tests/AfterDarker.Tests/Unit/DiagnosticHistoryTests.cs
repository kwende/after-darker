using AfterDarker.Runtime;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticHistoryTests
{
    [TestMethod]
    public void RecentHistoryKeepsNewestEntriesInOrderWhileTotalKeepsGrowing()
    {
        var history = new DiagnosticHistory<int>(new(3));
        for (int i = 0; i < 10; i++) history.Add(i);
        Assert.AreEqual(3, history.Count);
        Assert.AreEqual(10L, history.TotalCount);
        CollectionAssert.AreEqual(new[] { 7, 8, 9 }, history.Snapshot().ToArray());
    }

    [TestMethod]
    public void SnapshotRemainsUnchangedAfterRingWraps()
    {
        var history = new DiagnosticHistory<string>(new(2));
        history.Add("first"); history.Add("second");
        var snapshot = history.Snapshot();
        history.Add("third"); history.Add("fourth");
        CollectionAssert.AreEqual(new[] { "first", "second" }, snapshot.ToArray());
        CollectionAssert.AreEqual(new[] { "third", "fourth" }, history.Snapshot().ToArray());
    }

    [TestMethod]
    public void ZeroCapacityRetainsOnlyCountersAndFullIsExplicitlyUnbounded()
    {
        var none = new DiagnosticHistory<int>(new(0));
        var full = new DiagnosticHistory<int>(DiagnosticOptions.Full);
        for (int i = 0; i < 500; i++) { none.Add(i); full.Add(i); }
        Assert.AreEqual(500L, none.TotalCount);
        Assert.AreEqual(0, none.Count);
        Assert.AreEqual(0, none.Snapshot().Count);
        Assert.AreEqual(500, full.Count);
        CollectionAssert.AreEqual(Enumerable.Range(0, 500).ToArray(), full.Snapshot().ToArray());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(100001)]
    public void InvalidRetentionCapacityFailsBeforeStorageAllocation(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticHistory<int>(new(capacity)));

    [TestMethod]
    public void RingStorageDoesNotAllocateAsEventsArrive()
    {
        var history = new DiagnosticHistory<int>(new(3));
        for (int i = 0; i < 100; i++) history.Add(i); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) history.Add(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated, "Adding values should only overwrite preallocated slots.");
    }
}
