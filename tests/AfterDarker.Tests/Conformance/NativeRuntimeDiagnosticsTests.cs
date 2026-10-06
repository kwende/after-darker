using AfterDarker.Runtime;

namespace AfterDarker.Tests.Conformance;

/// <summary>Exercises the same source-owned diagnostic exposed to installed consumers.</summary>
[TestClass]
public sealed class NativeRuntimeDiagnosticsTests
{
    [TestMethod]
    public void InstalledEngineProbeStopsResumesAndReadsGuestResult() =>
        Assert.AreEqual((ushort)12, NativeRuntimeDiagnostics.VerifyExecution());
}
