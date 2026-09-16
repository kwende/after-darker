using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class FarPascalWordFrameTests
{
    [TestMethod]
    public void Decode_ReadsIpThenCsAndReturnsArgumentsInSourceOrder()
    {
        // Stack order: IP=1234, CS=5678, right=5, left=-7.
        byte[] bytes = [0x34, 0x12, 0x78, 0x56, 0x05, 0, 0xF9, 0xFF];
        var frame = new FarPascalWordFrame(bytes);
        Assert.AreEqual((ushort)0x1234, frame.ReturnIp);
        Assert.AreEqual((ushort)0x5678, frame.ReturnCs);
        Assert.AreEqual(2, frame.ArgumentCount);
        Assert.AreEqual((short)-7, frame.ReadArgument(0));
        Assert.AreEqual((short)5, frame.ReadArgument(1));
        Assert.AreEqual((ushort)0x1000, frame.StackPointerAfterReturn(0x0FF8));
    }

    [TestMethod]
    public void Decode_PreservesSignedWordExtremes()
    {
        var frame = new FarPascalWordFrame([0, 0, 8, 0, 0xFF, 0x7F, 0, 0x80]);
        Assert.AreEqual(short.MinValue, frame.ReadArgument(0));
        Assert.AreEqual(short.MaxValue, frame.ReadArgument(1));
    }

    [TestMethod]
    public void ReturnWithNoArguments_PopsOnlyIpAndCs()
    {
        var frame = new FarPascalWordFrame([0, 0, 8, 0]);
        Assert.AreEqual(0, frame.ArgumentCount);
        Assert.AreEqual((ushort)0x1000, frame.StackPointerAfterReturn(0x0FFC));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(7)]
    public void Decode_RejectsMissingReturnBytesOrPartialWord(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new FarPascalWordFrame(new byte[length]); });
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void ReadArgument_RejectsIndicesOutsideTheFrame(int index)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
        {
            var frame = new FarPascalWordFrame(new byte[8]);
            frame.ReadArgument(index);
        });
    }

    [TestMethod]
    public void Return_RejectsStackWrapInsteadOfSilentlyTruncating()
    {
        var frame = new FarPascalWordFrame(new byte[8]);
        Assert.AreEqual((ushort)0xFFFF, frame.StackPointerAfterReturn(0xFFF7));
        Assert.ThrowsExactly<OverflowException>(() =>
            new FarPascalWordFrame(new byte[8]).StackPointerAfterReturn(0xFFF8));
    }
}
