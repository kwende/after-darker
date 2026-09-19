using System.Buffers.Binary;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class NeInternalRelocationTests
{
    private static NeLoadPlan Prepare(byte[] file, Action<NePatch>? observe = null,
        ushort firstSelector = 8, uint firstBase = 0x10000) => NeLoadPlan.CreateWithImportResolver(file,
            NeLoadPlan.PlaceSegments(NeReader.Read(file), firstSelector, firstBase), _ => new(0x80, 0x1234), observe);

    [TestMethod]
    public void FixedPointerChainAndSelectorUseAssignedDestinations()
    {
        byte[] file = RelocationDemo.Create();
        byte[] original = file.ToArray();
        NeLoadPlan plan = Prepare(file);
        byte[] code = plan.Segments[0].Bytes;
        CollectionAssert.AreEqual(new byte[] { 0x12, 0, 0x10, 0 }, code[4..8]);
        CollectionAssert.AreEqual(code[4..8], code[0x10..0x14]);
        Assert.AreEqual((ushort)0x18, Word(code, 0x18));
        Assert.AreEqual((ushort)0x20, Word(code, 0x20)); // internal entry ordinal 2, not segment 2 + zero
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x12, 0x80, 0 }, code[0x28..0x2C]);
        CollectionAssert.AreEqual(original, file);
        Assert.AreEqual(5, plan.Patches.Count);
    }

    [TestMethod]
    public void ReassigningSelectorsChangesPointersButRebasingLinearMemoryDoesNot()
    {
        byte[] file = RelocationDemo.Create();
        NeLoadPlan original = Prepare(file);
        NeLoadPlan moved = Prepare(file, firstSelector: 0x40, firstBase: 0xA0000);
        Assert.AreEqual(new FarPointer16(0x48, 0x12), moved.Patches[0].Target);
        Assert.AreEqual((ushort)0x48, Word(moved.Segments[0].Bytes, 6));
        Assert.AreEqual((ushort)0x50, Word(moved.Segments[0].Bytes, 0x18));
        Assert.AreEqual((ushort)0x20, Word(moved.Segments[0].Bytes, 0x20));
        NeLoadPlan onlyBasesMoved = Prepare(file, firstBase: 0xA0000);
        CollectionAssert.AreEqual(original.Segments[0].Bytes, onlyBasesMoved.Segments[0].Bytes);
        Assert.AreNotEqual(original.Segments[1].Placement.LinearBase, onlyBasesMoved.Segments[1].Placement.LinearBase);
    }

    [TestMethod]
    public void ObserverReceivesEachActualPatchAndOriginalChainLink()
    {
        var observed = new List<NePatch>();
        NeLoadPlan plan = Prepare(RelocationDemo.Create(), observed.Add);
        CollectionAssert.AreEqual(plan.Patches.ToArray(), observed.ToArray());
        Assert.AreEqual((ushort)0x10, observed[0].NextOffset);
        Assert.AreEqual((ushort)0xFFFF, observed[1].NextOffset);
        Assert.AreEqual(new NeAddress(1, 4), observed[0].Source);
        Assert.AreEqual((ushort)2, observed[0].Relocation!.Target1);
        CollectionAssert.AreEqual(new byte[] { 0x10, 0, 0, 0 }, observed[0].Before);
    }

    [TestMethod]
    [DataRow(2, 2)] // internal selector / two-byte field
    [DataRow(3, 4)] // internal far pointer / four-byte field
    [DataRow(5, 2)] // internal offset / two-byte field
    public void InternalEntryOrdinalResolvesEvenWhenItIsNotExported(int type, int width)
    {
        byte[] file = RelocationDemo.Create();
        file[0x352] = (byte)type;
        NeLoadPlan plan = Prepare(file);
        NePatch patch = plan.Patches.Single(p => p.Source.Offset == 0x20);
        Assert.AreEqual(new FarPointer16(0x10, 0x20), patch.Target);
        Assert.AreEqual(width, patch.After.Length);
        Assert.AreEqual((ushort)(type == 2 ? 0x10 : 0x20), Word(patch.After, 0));
        if (type == 3) Assert.AreEqual((ushort)0x10, Word(patch.After, 2));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(5)]
    public void ImportedSelectorAndOffsetUseTheSameResolver(int type)
    {
        byte[] file = RelocationDemo.Create(); file[0x35A] = (byte)type;
        NeLoadPlan plan = Prepare(file);
        Assert.AreEqual((ushort)(type == 2 ? 0x80 : 0x1234), Word(plan.Segments[0].Bytes, 0x28));
    }

    [TestMethod]
    public void FixedDataPointerCanReferenceZeroFilledAllocation()
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x346, 3); Word(file, 0x348, 0x60);
        NeLoadPlan plan = Prepare(file);
        Assert.AreEqual(new FarPointer16(0x18, 0x60), plan.Patches[0].Target);
        Assert.AreEqual(0x80, plan.Segments[2].Bytes.Length);
        Assert.AreEqual((byte)0, plan.Segments[2].Bytes[0x60]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    [DataRow(0x102)]
    public void InvalidInternalSegmentOrReservedByteIsRejected(int target)
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x346, (ushort)target);
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    public void InternalOffsetOutsideAllocatedTargetIsRejected()
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x348, 0x40); // S2 has exactly 0x40 bytes
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void MissingInternalOrdinalIsRejected(int ordinal)
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x358, (ushort)ordinal);
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    public void ConstantEntryCannotBeUsedAsAnInternalFarAddress()
    {
        byte[] file = RelocationDemo.Create();
        file[0x186] = 0xFE; // replace movable entry bundle with constant bundle
        file[0x18A] = 0; // terminate after its shorter record
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    [DataRow(0xFFFF)] // no initial site
    [DataRow(0x3F)]   // truncated selector field
    public void InvalidFirstPatchSiteIsRejected(int offset)
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x34C, (ushort)offset);
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    public void SelectorChainCanPatchLastCompleteWord()
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x34C, 0x3E); Word(file, 0x33E, 0xFFFF);
        NeLoadPlan plan = Prepare(file);
        Assert.AreEqual((ushort)0x18, Word(plan.Segments[0].Bytes, 0x3E));
    }

    [TestMethod]
    public void SelectorChainCyclesAreRejected()
    {
        byte[] file = RelocationDemo.Create(); Word(file, 0x318, 0x18);
        Assert.Throws<InvalidDataException>(() => Prepare(file));
    }

    [TestMethod]
    public void ResolverFailureLeavesSourceUnchanged()
    {
        byte[] file = RelocationDemo.Create(); byte[] original = file.ToArray();
        Assert.Throws<NotSupportedException>(() => NeLoadPlan.CreateWithImportResolver(file,
            NeLoadPlan.PlaceSegments(NeReader.Read(file)), _ => throw new NotSupportedException("No implementation")));
        CollectionAssert.AreEqual(original, file);
    }

    [TestMethod]
    public void NullImportSelectorIsRejected()
    {
        byte[] file = RelocationDemo.Create();
        Assert.Throws<InvalidDataException>(() => NeLoadPlan.CreateWithImportResolver(file,
            NeLoadPlan.PlaceSegments(NeReader.Read(file)), _ => new(0, 0x100)));
    }

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static void Word(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
