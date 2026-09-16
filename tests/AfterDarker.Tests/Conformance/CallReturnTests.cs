using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class CallReturnTests
{
    [TestMethod]
    public void NearCall_PushesReturnIpAndRetRestoresStack()
    {
        var observed = new Tutorial02StackCall().Execute();
        Assert.AreEqual(0x8FFEL, observed.CalleeSp);
        Assert.AreEqual((ushort)0x1006, observed.SavedReturnIp);
        Assert.AreEqual(0x9000L, observed.FinalSp);
        Assert.AreEqual(12L, observed.Ax);
        Assert.AreEqual(0x100EL, observed.FinalIp);
        Assert.AreEqual(0L, observed.FinalCs);
        Assert.AreEqual(0L, observed.FinalSs);
    }

    [TestMethod]
    public void FarCall_CrossesSelectorsAndRetfRestoresCallerBeforeGuestStore()
    {
        var observed = new Tutorial03FarCall().Execute();
        Assert.IsTrue(observed.ProtectedMode);
        Assert.AreEqual(0x0010L, observed.CalleeCs);
        Assert.AreEqual(0x0FFCL, observed.CalleeSp);
        Assert.AreEqual((ushort)0x0008, observed.SavedIp);
        Assert.AreEqual((ushort)0x0008, observed.SavedCs);
        Assert.AreEqual(0x1000L, observed.FinalSp);
        Assert.AreEqual(0x0008L, observed.FinalCs);
        Assert.AreEqual(0x000BL, observed.FinalIp);
        Assert.AreEqual(0x0018L, observed.FinalDs);
        Assert.AreEqual(0x0020L, observed.FinalSs);
        Assert.AreEqual(12L, observed.Ax);
        Assert.AreEqual((ushort)12, observed.StoredValue, "The guest must execute its MOV after RETF.");
    }
}
