using System.Buffers.Binary;
using AfterDarker.Core.Ne;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class NeLoadPlanTests
{
    private static readonly NeSegmentPlacement[] Placements = [new(1, 8, 0x10000), new(2, 16, 0x20000)];
    private static readonly NeImportBinding[] Bindings =
    [
        new(new("kernel", 18, null), "KERNEL!#18", new(32, 0x100), 0, Win16ReturnLayout.WordInAx, "Test"),
        new(new("KERNEL", null, "NamedProc"), "KERNEL!NamedProc", new(32, 0x110), 0, Win16ReturnLayout.WordInAx, "Test"),
    ];

    // Start from the existing original NE metadata fixture. Give its imported
    // pointer sites real terminating links and its shared-data export a prologue.
    private static byte[] FileBytes()
    {
        byte[] bytes = NeFixture.Create();
        foreach (int offset in new[] { 4, 0x10, 0x18 }) Word(bytes, 0x400 + offset, 0xFFFF);
        bytes[0x180 + 2] = 1; // ordinal 1 at :0010 is not a shared-data export in this fixture
        return bytes;
    }

    [TestMethod]
    public void CopiesBytesAndZeroFillsDataPlusHeapWithoutChangingInput()
    {
        byte[] file = FileBytes();
        byte[] original = file.ToArray();
        NeLoadPlan plan = NeLoadPlan.Create(file, Placements, Bindings);
        CollectionAssert.AreEqual(original, file);
        Assert.AreEqual(0x500, plan.Segments[1].Bytes.Length); // 0x100 minimum + 0x400 heap
        Assert.IsTrue(plan.Segments[1].Bytes.All(b => b == 0));
        Assert.AreEqual(new FarPointer16(8, 8), plan.ResolveCode(plan.Image.Header.Startup!.Value));
        Assert.Throws<InvalidDataException>(() => plan.ResolveCode(new(2, 0)));
    }

    [TestMethod]
    public void PatchesOrdinalAndNamedFarPointersInLittleEndianOrder()
    {
        NeLoadPlan plan = NeLoadPlan.Create(FileBytes(), Placements, Bindings);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x20, 0x00 }, plan.Segments[0].Bytes[4..8]);
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x01, 0x20, 0x00 }, plan.Segments[0].Bytes[0x18..0x1C]);
        Assert.AreEqual(3, plan.Patches.Count);
    }

    [TestMethod]
    public void FollowsChainBeforeOverwritingItsLinks()
    {
        byte[] file = FileBytes();
        Word(file, 0x404, 0x28);
        Word(file, 0x428, 0xFFFF);
        NeLoadPlan plan = NeLoadPlan.Create(file, Placements, Bindings);
        Assert.AreEqual(4, plan.Patches.Count);
        CollectionAssert.AreEqual(plan.Segments[0].Bytes[4..8], plan.Segments[0].Bytes[0x28..0x2C]);
    }

    [TestMethod]
    [DataRow(0x28, 4)] // cycle
    [DataRow(0x3E, 0xFFFF)] // four-byte pointer straddles stored code boundary
    [DataRow(0x10, 0xFFFF)] // another relocation also patches this site
    public void RejectsMalformedOrOverlappingChains(int next, int terminal)
    {
        byte[] file = FileBytes();
        Word(file, 0x404, (ushort)next);
        Word(file, 0x400 + next, (ushort)terminal);
        Assert.Throws<InvalidDataException>(() => NeLoadPlan.Create(file, Placements, Bindings));
    }

    [TestMethod]
    [DataRow(0x1E, 0x58)]
    [DataRow(0x8C, 0xD8)]
    public void SharedDataPrologueLoadsAssignedSelector(int first, int second)
    {
        byte[] file = FileBytes();
        file[0x182] = 3; // shared-data export
        Word(file, 0x183, 0x30); // put it away from imported pointer patches
        file[0x430] = (byte)first; file[0x431] = (byte)second; file[0x432] = 0x90;
        NeLoadPlan plan = NeLoadPlan.Create(file, Placements, Bindings);
        CollectionAssert.AreEqual(new byte[] { 0xB8, 0x10, 0 }, plan.Segments[0].Bytes[0x30..0x33]);
    }

    [TestMethod]
    public void UnknownImportFailsBySymbolBeforeExecution()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            NeLoadPlan.Create(FileBytes(), Placements, []));
        StringAssert.Contains(error.Message, "KERNEL!#18");
    }

    [TestMethod]
    [DataRow(3, 5)] // additive imported pointer
    [DataRow(2, 1)] // selector-only relocation
    [DataRow(3, 0)] // internal relocation (separate milestone)
    public void UnsupportedFixupsFailExplicitly(int addressType, int flags)
    {
        byte[] file = FileBytes();
        file[0x442] = (byte)addressType; file[0x443] = (byte)flags;
        Assert.Throws<NotSupportedException>(() => NeLoadPlan.Create(file, Placements, Bindings));
    }

    [TestMethod]
    public void RejectsUnrecognizedSharedDataPrologue()
    {
        byte[] file = FileBytes();
        file[0x182] = 3; Word(file, 0x183, 0x30);
        Assert.Throws<NotSupportedException>(() => NeLoadPlan.Create(file, Placements, Bindings));
    }

    [TestMethod]
    public void RejectsOverlappingPlacements()
    {
        Assert.Throws<ArgumentException>(() => NeLoadPlan.Create(FileBytes(),
            [new(1, 8, 0x10000), new(2, 16, 0x10000)], Bindings));
    }

    [TestMethod]
    [DataRow(0x0800)] // self-loader
    [DataRow(0x0010)] // Win32
    [DataRow(0x2000)] // linked with errors
    public void RejectsUnsupportedLibraryFlags(int flag)
    {
        byte[] file = FileBytes();
        Word(file, 0x8C, (ushort)(0x8001 | flag));
        Assert.Throws<NotSupportedException>(() => NeLoadPlan.Create(file, Placements, Bindings));
    }

    private static void Word(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
