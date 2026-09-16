using AfterDarker.Core.X86;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class SegmentDescriptor16Tests
{
    [TestMethod]
    [DataRow(true, 0x9B)]
    [DataRow(false, 0x93)]
    public void Encode_PreservesBaseAndLimitWith16BitRingZeroAccess(bool executable, int access)
    {
        // Distinct bytes catch swapped/truncated fields; do not decode using the encoder.
        byte[] actual = SegmentDescriptor16.Encode(0x12345678, 0xABCD, executable);
        byte[] expected = [0xCD, 0xAB, 0x78, 0x56, 0x34, (byte)access, 0x00, 0x12];
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Encode_ZeroLimitStillDescribesOneAddressableByte()
    {
        byte[] actual = SegmentDescriptor16.Encode(0, 0, executable: false);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 0x93, 0, 0 }, actual);
    }

    [TestMethod]
    public void Encode_MaximumWordLimitKeepsByteGranularity()
    {
        byte[] actual = SegmentDescriptor16.Encode(0xFFFFFFFF, 0xFFFF, executable: true);
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x9B, 0, 0xFF }, actual);
    }
}
