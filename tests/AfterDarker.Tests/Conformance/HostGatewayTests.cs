using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class HostGatewayTests
{
    [TestMethod]
    public void Gateway_StopsAtExpectedAddressAndResumesTwiceWithPascalCleanup()
    {
        var observed = new Tutorial04HostGateway().Execute();
        Assert.HasCount(2, observed.Calls);
        for (int index = 0; index < observed.Calls.Count; index++)
        {
            var call = observed.Calls[index];
            Assert.AreEqual((ushort)0x0010, call.Selector);
            Assert.AreEqual((ushort)0x0200, call.Offset);
            Assert.AreEqual(0x20200L, call.LinearAddress);
            Assert.AreEqual(0x0FF8L, call.StackPointer);
            Assert.AreEqual((ushort)0x0008, call.ReturnCs);
            Assert.AreEqual(index == 0 ? (ushort)0x000B : (ushort)0x0019, call.ReturnIp);
            Assert.AreEqual(index == 0 ? (short)7 : (short)-7, call.Left);
            Assert.AreEqual((short)5, call.Right);
            Assert.AreEqual(index == 0 ? (short)12 : (short)-2, call.ReturnedValue);
        }
        CollectionAssert.AreEqual(new ushort[] { 12, 0xFFFE }, observed.StoredValues.ToArray());
        Assert.AreEqual(0x1000L, observed.FinalSp);
        Assert.AreEqual(0x0008L, observed.FinalCs);
        Assert.AreEqual(0x001CL, observed.FinalIp);
        Assert.AreEqual(0xFFFEL, observed.FinalAx);
        Assert.AreEqual(0x0018L, observed.FinalDs);
        Assert.AreEqual(0x0020L, observed.FinalSs);
        Assert.IsTrue(observed.ProtectedMode);
    }

    [TestMethod]
    public void Gateway_PassesArgumentsToActualHandlerInOrderAndGuestStoresItsResult()
    {
        var arguments = new List<(short Left, short Right)>();
        var observed = new Tutorial04HostGateway().Execute((left, right) =>
        {
            arguments.Add((left, right));
            // Subtraction discriminates argument order; addition alone cannot.
            return (short)(left - right);
        });
        CollectionAssert.AreEqual(new (short, short)[] { (7, 5), (-7, 5) }, arguments);
        CollectionAssert.AreEqual(new ushort[] { 2, 0xFFF4 }, observed.StoredValues.ToArray());
        Assert.AreEqual(0xFFF4L, observed.FinalAx);
        Assert.AreEqual(0x1000L, observed.FinalSp);
        Assert.AreEqual(0x001CL, observed.FinalIp);
    }

    [TestMethod]
    [DataRow(-32768, 0x8000)]
    [DataRow(32767, 0x7FFF)]
    public void Gateway_PreservesAllReturnBitsAcrossTheGuestBoundary(int value, int expectedBits)
    {
        var observed = new Tutorial04HostGateway().Execute((_, _) => (short)value);
        CollectionAssert.AreEqual(new ushort[] { (ushort)expectedBits, (ushort)expectedBits },
            observed.StoredValues.ToArray());
        Assert.AreEqual((long)expectedBits, observed.FinalAx);
    }

    [TestMethod]
    public void HandlerFailure_PropagatesAndNextRunCanComplete()
    {
        var lesson = new Tutorial04HostGateway();
        var expected = new InvalidOperationException("Intentional host service failure.");
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
            lesson.Execute((_, _) => throw expected));
        Assert.AreSame(expected, actual);
        var nextRun = lesson.Execute();
        CollectionAssert.AreEqual(new ushort[] { 12, 0xFFFE }, nextRun.StoredValues.ToArray());
    }
}
