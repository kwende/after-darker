using AfterDarker.Core.Ne;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterDarker.Tests.Toolchain;

// Compiled only with -p:BuildWin16Fixture=true. The build produces and copies
// the real DLL first, so a missing compiler or fixture fails this opted-in run.
[TestClass]
[TestCategory("Toolchain")]
public sealed class WatcomFixtureTests
{
    private static NeImage ReadFixture() => NeReader.ReadFile(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "hello42.dll"));

    [TestMethod]
    public void WindowsLibraryHasStartupCodeAndAutomaticData()
    {
        NeImage image = ReadFixture();
        Assert.IsTrue(image.Header.IsLibrary);
        Assert.AreEqual((byte)2, image.Header.TargetOs);
        Assert.IsNotNull(image.Header.Startup);
        AssertCodeAddress(image, image.Header.Startup.Value);
        Assert.IsTrue(image.Segments.Single(s =>
            s.Number == image.Header.AutomaticDataSegment).IsData);
    }

    [TestMethod]
    public void PublicExportsAreHelloWorldAndResidentExitProcedure()
    {
        NeImage image = ReadFixture();
        NeEntry? hello = image.FindExport("HELLOWORLD");
        NeEntry? exit = image.FindExport("WEP");
        Assert.IsNotNull(hello);
        Assert.IsNotNull(exit);
        Assert.AreEqual((ushort)1, hello.Ordinal);
        Assert.AreEqual((ushort)2, exit.Ordinal);
        Assert.IsNotNull(hello.Address);
        Assert.IsNotNull(exit.Address);
        AssertCodeAddress(image, hello.Address.Value);
        AssertCodeAddress(image, exit.Address.Value);
        Assert.AreNotEqual(hello.Address, exit.Address);
        Assert.AreNotEqual(image.Header.Startup, hello.Address);
        Assert.AreEqual(2, image.Entries.Count(e => e.Exported));
        Assert.IsTrue(image.Names.Any(n => n.Text == "WEP" && n.Resident));
        Assert.IsNull(image.FindExport("LibMain")); // Reached through header startup.
    }

    [TestMethod]
    public void RuntimeDependenciesRemainTheInspectedMinimalSet()
    {
        NeImage image = ReadFixture();
        CollectionAssert.AreEquivalent(new[]
        {
            new NeImport("KERNEL", 3, null), // GetVersion, startup
            new NeImport("KERNEL", 4, null), // LocalInit, startup
            new NeImport("USER", 1, null),   // MessageBox, runtime error path
        }, image.Imports.ToArray());
        Assert.AreEqual(0, image.Resources.Count);
    }

    private static void AssertCodeAddress(NeImage image, NeAddress address)
    {
        NeSegment segment = image.Segments.Single(s => s.Number == address.SegmentNumber);
        Assert.IsFalse(segment.IsData);
        Assert.IsNotNull(segment.FileOffset);
        Assert.IsTrue(address.Offset < segment.FileBytes);
    }
}
