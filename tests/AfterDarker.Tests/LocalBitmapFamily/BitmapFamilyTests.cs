using AfterDarker.Core.Rendering;
using AfterDarker.Runtime;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.LocalBitmapFamily;

[TestClass]
[TestCategory("LocalBitmapFamily")]
public sealed class BitmapFamilyTests
{
    private static byte[] ReadModule(string name) => File.ReadAllBytes(Path.Combine(
        Environment.GetEnvironmentVariable("AFTER_DARKER_MODULE_DIRECTORY") ??
        throw new InvalidOperationException("Set AFTER_DARKER_MODULE_DIRECTORY to your private AD directory."), name + ".ad"));

    [TestMethod]
    [DataRow("Can of Worms")]
    [DataRow("GeoBounce")]
    [DataRow("Nocturnes")]
    [DataRow("Punch Out")]
    public void OriginalModuleDrawsAndReleasesItsResources(string name)
    {
        using var trace = new StringWriter();
        using var session = SupportedModules.Open(ReadModule(name), new(320, 240), output: trace);
        try
        {
            session.Initialize(); session.Blank();
            byte[] before = new byte[session.PixelByteCount], after = new byte[session.PixelByteCount];
            session.CopyPixelsTo(before);
            if (name == "Can of Worms") Assert.IsTrue(before.All(value => value == 255));
            for (int draw = 1; draw <= 600; draw++)
            {
                session.DrawFrame();
                if (Environment.GetEnvironmentVariable("AFTER_DARKER_BITMAP_CAPTURE") is string folder && draw % 100 == 0)
                {
                    string destination = Path.Combine(folder, name); Directory.CreateDirectory(destination);
                    session.CopyPixelsTo(after);
                    using var output = File.Create(Path.Combine(destination, $"frame-{draw:D4}.png"));
                    PngWriter.Write(output, 320, 240, after);
                }
            }
            session.CopyPixelsTo(after); Assert.IsFalse(before.SequenceEqual(after), "Guest must change the initial image.");
            var during = session.GetPlaybackResult();
            Assert.AreEqual(name == "Punch Out" ? 3 : 1, during.PeakBitmaps);
            Assert.AreEqual(name == "Punch Out" ? 3 : 1, during.PeakMemoryDcs);
            Assert.AreEqual(name == "Punch Out" ? 2 : 0, during.PeakRegions);
            if (name == "Nocturnes") Assert.AreEqual(1L, during.ImportCalls["USER!LoadBitmap (#175)"]);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {name, during.Instructions, during.ImportCalls,
                during.PeakBitmaps, during.PeakMemoryDcs, during.LiveBitmaps, during.LiveMemoryDcs, during.BitmapBytes}));
            session.Shutdown(); AssertClean(session.GetPlaybackResult());
        }
        catch { Console.WriteLine(trace.ToString()[Math.Max(0, trace.ToString().Length - 6000)..]); throw; }
    }

    [TestMethod]
    [DataRow("Can of Worms")] [DataRow("GeoBounce")] [DataRow("Nocturnes")] [DataRow("Punch Out")]
    public void MinimumOddAndMaximumDimensionsAndDifferentSeedsShutDownCleanly(string name)
    {
        foreach (var size in new[] {(Width:128,Height:128,Draws:100), (Width:321,Height:239,Draws:100),
            (Width:name == "Punch Out" ? 2028 : 2048,Height:128,Draws:3)})
        {
            var timing = SessionTiming.Deterministic() with {CivilTime=MondrianSession.CivilTime.AddSeconds(13)};
            using var session = SupportedModules.Open(ReadModule(name),new((short)size.Width,(short)size.Height),timing:timing);
            session.Initialize();session.Blank();
            for(int draw=0;draw<size.Draws;draw++) session.DrawFrame();
            session.Shutdown();AssertClean(session.GetPlaybackResult());
        }
    }

    [TestMethod]
    [DataRow("Can of Worms")] [DataRow("GeoBounce")] [DataRow("Nocturnes")] [DataRow("Punch Out")]
    public void LiveClockAtWpfDimensionsMakesProgressWithBoundedOwnership(string name)
    {
        var time=new ManualTimeProvider();
        using var session=SupportedModules.Open(ReadModule(name),new(640,480),timing:SessionTiming.Live(time));
        session.Initialize();session.Blank();
        byte[] before=new byte[session.PixelByteCount],after=new byte[session.PixelByteCount];session.CopyPixelsTo(before);
        for(int draw=0;draw<300;draw++) {time.Advance(TimeSpan.FromMilliseconds(16));session.DrawFrame();}
        session.CopyPixelsTo(after);Assert.IsFalse(before.SequenceEqual(after));
        session.Shutdown();AssertClean(session.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow("Can of Worms")] [DataRow("GeoBounce")] [DataRow("Nocturnes")] [DataRow("Punch Out")]
    public void IndependentSessionsDoNotShareHandlesPixelsOrUnrelatedSpeedSettings(string name)
    {
        byte[] file=ReadModule(name);
        using var first=SupportedModules.Open(file,new(160,128,Speed:0));
        using var second=SupportedModules.Open(file,new(160,128,Speed:100));
        first.Initialize();second.Initialize();first.Blank();second.Blank();
        byte[] firstPixels=new byte[first.PixelByteCount],secondPixels=new byte[second.PixelByteCount];
        for(int draw=0;draw<60;draw++)
        {
            first.DrawFrame();second.DrawFrame();first.CopyPixelsTo(firstPixels);second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels,secondPixels);
        }
        first.Shutdown();second.DrawFrame();second.Shutdown();
        AssertClean(first.GetPlaybackResult());AssertClean(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow("Can of Worms")] [DataRow("GeoBounce")] [DataRow("Nocturnes")] [DataRow("Punch Out")]
    public void UnsafeDimensionsAndChangedArtifactFailBeforeExecution(string name)
    {
        byte[] file=ReadModule(name);
        Assert.Throws<ArgumentOutOfRangeException>(()=>SupportedModules.Open(file,new(127,240)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>SupportedModules.Open(file,new(320,127)));
        if(name=="Punch Out") Assert.Throws<ArgumentOutOfRangeException>(()=>SupportedModules.Open(file,new(2029,240)));
        file[^1]^=1; Assert.Throws<NotSupportedException>(()=>SupportedModules.Open(file,new()));
    }

    private static void AssertClean(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LivePens); Assert.AreEqual(0, result.LiveBrushes);
        Assert.AreEqual(0, result.LiveRegions);
        Assert.AreEqual(0, result.LiveBitmaps); Assert.AreEqual(0, result.LiveMemoryDcs); Assert.AreEqual(0, result.BitmapBytes);
        Assert.AreEqual(0, result.OutstandingLocks); Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name); Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
    }
}
