using AfterDarker.Runtime;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class LatestFrameMailboxTests
{
    [TestMethod]
    public void SlowConsumerGetsLatestFrameAndOwnsItsCopy()
    {
        var mailbox = new LatestFrameMailbox(3);
        byte[] source = [1, 2, 3], destination = new byte[3];
        Assert.IsFalse(mailbox.TryCopyTo(destination, out _));
        mailbox.Publish(source, new(1, 1));
        source[0] = 4;
        mailbox.Publish(source, new(2, 2) { IsIntermediate = true });
        source[0] = 9;
        Assert.IsTrue(mailbox.TryCopyTo(destination, out var frame));
        CollectionAssert.AreEqual(new byte[] { 4, 2, 3 }, destination);
        Assert.AreEqual(new FrameInfo(2, 2) { IsIntermediate = true }, frame);
        mailbox.Publish(source, new(3, 3));
        Assert.AreEqual((byte)4, destination[0]);
        Assert.IsTrue(mailbox.TryCopyTo(destination, out frame));
        Assert.IsFalse(frame!.IsIntermediate); // Completed images cannot inherit the flash marker.
        Assert.IsFalse(mailbox.TryCopyTo(destination, out _));
        Assert.Throws<ArgumentException>(() => mailbox.Publish(new byte[2], new(0, 0)));
        Assert.Throws<ArgumentException>(() => mailbox.TryCopyTo(new byte[2], out _));
    }

    [TestMethod]
    public async Task ConcurrentProducerNeverTearsPixelsFromTheirMetadata()
    {
        var mailbox = new LatestFrameMailbox(1024);
        var producer = Task.Run(() =>
        {
            byte[] source = new byte[1024];
            for (int i = 1; i <= 10_000; i++)
            {
                Array.Fill(source, (byte)(i % 256));
                mailbox.Publish(source, new(i, i) { IsIntermediate = i % 2 == 0 });
            }
        });
        byte[] destination = new byte[1024];
        long last = 0;
        do
        {
            if (mailbox.TryCopyTo(destination, out var frame))
            {
                Assert.IsGreaterThan(last, frame!.DrawCalls);
                Assert.IsTrue(destination.All(b => b == frame.DrawCalls % 256));
                Assert.AreEqual(frame.DrawCalls % 2 == 0, frame.IsIntermediate);
                last = frame.DrawCalls;
            }
            await Task.Yield();
        } while (!producer.IsCompleted || last != 10_000);
        await producer;
    }
}
