using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class MondrianHostContractTests
{
    [TestMethod]
    public void HostRecordsContainGuestHandleAndSignedDimensionsAtObservedOffsets()
    {
        var records = MondrianInitialization.CreateRecords(new(320, 200, 25, false));
        Assert.AreEqual((ushort)200, Word(records.System, 0x14));
        Assert.AreEqual((ushort)0x102, Word(records.System, 0x20));
        Assert.AreEqual((ushort)0x42, Word(records.System, 0x2C));
        Assert.AreEqual((ushort)0x48, Word(records.System, 0x34));
        Assert.AreEqual((ushort)320, Word(records.Module, 2));
        Assert.AreEqual((ushort)200, Word(records.Module, 4));
        Assert.AreEqual((ushort)25, Word(records.Module, 6));
        Assert.AreEqual((ushort)0, Word(records.Module, 8));
    }

    [TestMethod]
    [DataRow(0, 480, 50)]
    [DataRow(-1, 480, 50)]
    [DataRow(640, 0, 50)]
    [DataRow(640, 480, 51)]
    public void InvalidOptionsFailBeforeGuestCanDivideByDimensions(int width, int height, int speed) =>
        Assert.Throws<ArgumentException>(() => MondrianInitialization.CreateRecords(new((short)width, (short)height, (ushort)speed)));

    [TestMethod]
    public void GlobalBlockLockNestingReturnsStablePointerAndRemainingCount()
    {
        var blocks = new GuestGlobalBlocks(new RecordMemory());
        blocks.Register(0x123, new(0x48, 0x100), 16);
        Assert.AreEqual(new FarPointer16(0x48, 0x100), blocks.Lock(0x123));
        Assert.AreEqual(new FarPointer16(0x48, 0x100), blocks.Lock(0x123));
        Assert.AreEqual((ushort)1, blocks.Unlock(0x123));
        Assert.AreEqual((ushort)0, blocks.Unlock(0x123));
        Assert.AreEqual(0, blocks.OutstandingLocks);
        Assert.Throws<InvalidOperationException>(() => blocks.Unlock(0x123));
        Assert.Throws<InvalidOperationException>(() => blocks.Lock(0));
        Assert.Throws<InvalidOperationException>(() => blocks.Lock(0xDEAD));
        Assert.Throws<InvalidOperationException>(() => blocks.Register(0x124, new(0x48, 4090), 16));
    }

    [TestMethod]
    public void DateAndTimeResponsesPackLeapDayWeekdayAndHundredths()
    {
        var time = new DateTime(2000, 2, 29, 23, 59, 58, 999);
        var date = DosClock.Respond(0x2A, 0x2AFF, time);
        Assert.AreEqual((ushort)0x2A02, date.Ax); // Tuesday
        Assert.AreEqual((ushort)2000, date.Cx);
        Assert.AreEqual((ushort)0x021D, date.Dx);
        var clock = DosClock.Respond(0x2C, 0x2CFF, time);
        Assert.AreEqual((ushort)0x2C00, clock.Ax);
        Assert.AreEqual((ushort)0x173B, clock.Cx);
        Assert.AreEqual((ushort)0x3A63, clock.Dx); // seconds 58, hundredths 99 (truncate milliseconds)
        Assert.Throws<ArgumentOutOfRangeException>(() => DosClock.Respond(0x2A, 0, new DateTime(1979, 1, 1)));
    }

    [TestMethod]
    public void UnrecognizedModuleIsRejectedBeforeCreatingCpu() =>
        Assert.Throws<NotSupportedException>(() => Tutorial08MondrianInitialize.Execute([0x4D, 0x5A]));

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private sealed class RecordMemory : IGuestMemory16
    {
        private readonly byte[] bytes = new byte[4096];
        public byte[] Read(FarPointer16 address, int count)
        {
            if (address.Selector != 0x48 || count < 0 || address.Offset > bytes.Length - count)
                throw new InvalidOperationException("Unmapped record bytes.");
            return bytes.AsSpan(address.Offset, count).ToArray();
        }
        public void Write(FarPointer16 address, byte[] value)
        {
            _ = Read(address, value.Length);
            value.CopyTo(bytes, address.Offset);
        }
    }
}
