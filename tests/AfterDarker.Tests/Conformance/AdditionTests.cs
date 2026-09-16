using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class AdditionTests
{
    [TestMethod]
    [DataRow(7, 5, 12)]
    [DataRow(0, 0, 0)]
    [DataRow(0x1234, 0x0102, 0x1336)]
    [DataRow(0xFFFF, 1, 0)]
    [DataRow(0x7FFF, 1, 0x8000)]
    [DataRow(0xFFFF, 0xFFFF, 0xFFFE)]
    public void Add_Executes16BitOperandsAndReachesCompletion(int left, int right, int expected)
    {
        var observed = new Tutorial01Addition().Execute((ushort)left, (ushort)right);
        Assert.AreEqual((long)expected, observed.Ax, "AX must reflect guest 16-bit arithmetic.");
        Assert.AreEqual(0x1006L, observed.Ip, "A bounded early stop is not completion.");
    }
}
