using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class AfterDarkCallPlanTests
{
    [TestMethod]
    public void Plan_UsesResolvedExportForEveryMessageAndKeepsDllStartupSeparate()
    {
        byte[] bytes = NeFixture.Create();
        NeFixture.Word(bytes, 0x183, 0x30); // A different export offset must flow through the plan.
        AfterDarkCallPlan plan = AfterDarkCallPlan.FromImage(NeReader.Read(bytes))!;
        Assert.AreEqual(new NeAddress(1, 8), plan.LibraryStartup);
        Assert.AreEqual(new NeAddress(1, 0x30), plan.Dispatcher);
        CollectionAssert.AreEqual(new ushort[] { 12, 0, 1, 2, 3 },
            plan.Invocations.Select(i => (ushort)i.Message).ToArray());
        Assert.IsTrue(plan.Invocations.All(i => i.EntryPoint == new NeAddress(1, 0x30)));
        Assert.AreEqual(AfterDarkMessage.DrawFrame, plan.Invocations.Single(i => i.Repeat).Message);
    }

    [TestMethod]
    [DataRow("executable")] [DataRow("data")] [DataRow("constant")] [DataRow("internal")]
    public void InapplicableModuleEntry_DoesNotProduceAnInvocationPlan(string variant)
    {
        byte[] bytes = NeFixture.Create();
        switch (variant)
        {
            case "executable": NeFixture.Word(bytes, 0x8C, 1); break;
            case "data": bytes[0x181] = 2; break;
            case "constant": bytes[0x181] = 0xFE; break;
            case "internal": bytes[0x182] = 0; break;
        }
        Assert.IsNull(AfterDarkCallPlan.FromImage(NeReader.Read(bytes)));
    }
}
