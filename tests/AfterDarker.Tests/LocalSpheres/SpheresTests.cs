using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalSpheres;

/// <summary>Opt-in execution of the owner's original module; no proprietary inputs enter public test output.</summary>
[TestClass]
[TestCategory("LocalSpheres")]
public sealed class SpheresTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_SPHERES")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_SPHERES before enabling TestLocalSpheres."));

    [TestMethod]
    public void OriginalGuestCompletesOneHundredSpheresClearsAndContinuesWithoutLeakingBrushes()
    {
        using var session = (AfterDarkSession<SpheresState>)SupportedModules.Open(ReadModule(), new());
        session.Initialize();
        Assert.AreEqual(AfterDarkHostContract.PrimaryPaletteRequest, session.Blank().StoredAx);
        Assert.IsTrue(session.CopyPixels().All(component => component == 0));
        var colors = new HashSet<uint>();
        for (int draw = 1; draw <= 3401; draw++)
        {
            var phase = session.DrawFrame();
            // S2:04AD..04D7 advances the band and starts a new sphere after band 33.
            // S2:00C2..0109 clears when that would start sphere 101.
            Assert.AreEqual((ushort)(draw % 34), phase.State.NextBand);
            Assert.AreEqual((ushort)(draw / 34 % 100 + 1), phase.State.SphereNumber);
            Assert.IsTrue(phase.State.Radius is >= 30 and <= 84);
            Assert.IsTrue(phase.State.CenterX < 640 && phase.State.CenterY < 480);
            var brush = session.GetResult().Calls.Last(call => call.Binding.Implementation == Win16Imports.Handler.CreateSolidBrush);
            uint color = (uint)brush.Arguments[0] << 16 | brush.Arguments[1];
            Assert.AreEqual(2u, color >> 24, "The guest requests PALETTERGB, retaining its actual RGB components.");
            colors.Add(color);
            var ellipse = session.GetResult().Calls.Last(call => call.Binding.Implementation == Win16Imports.Handler.Ellipse);
            Assert.AreEqual(AfterDarkHostContract.ReservedHdc, ellipse.Arguments[0]);
            Assert.AreEqual((ushort)1, ellipse.After.Ax);
            Assert.AreEqual(0, session.LiveBrushCount);
            Assert.AreEqual(0, session.LivePenCount);
            if (draw is 34 or 340 or 3399 or 3400 or 3401) Capture(session, draw);
            if (draw == 3400) Assert.IsTrue(session.CopyPixels().All(component => component == 0), "The guest's periodic clear must reach the surface.");
        }
        Assert.IsGreaterThan(100, colors.Count);
        Assert.IsTrue(session.CopyPixels().Any(component => component != 0), "The next sphere must draw after the clear.");
        var result = session.GetPlaybackResult();
        Assert.AreEqual(3401L, result.ImportCalls["GDI!Ellipse (#24)"]);
        Assert.AreEqual(3401L, result.ImportCalls["GDI!CreateSolidBrush (#66)"]);
        Assert.AreEqual(3401L, result.ImportCalls["GDI!DeleteObject (#69)"]);
        Assert.AreEqual(2L, result.ImportCalls["USER!FillRect (#81)"]);
        Assert.AreEqual(1, result.PeakBrushes);
        Console.WriteLine($"Spheres: {result.Instructions} instructions, 3401 bands, {colors.Count} colors; periodic clear verified.");
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session);
    }

    [TestMethod]
    public void IndependentGuestsReproducePixelsAndShutdownDoesNotAffectAnotherGuest()
    {
        byte[] file = ReadModule();
        using var first = (AfterDarkSession<SpheresState>)SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = (AfterDarkSession<SpheresState>)SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        for (int draw = 0; draw < 102; draw++)
        {
            Assert.AreEqual(first.DrawFrame().State, second.DrawFrame().State);
            CollectionAssert.AreEqual(first.CopyPixels(), second.CopyPixels());
        }
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first); AssertCleanShutdown(second);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(321, 239)]
    [DataRow(2048, 2048)]
    public void SupportedDimensionsCompleteTwoSpheresAndCleanup(int width, int height)
    {
        using var session = (AfterDarkSession<SpheresState>)SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 68; draw++) session.DrawFrame();
        session.Shutdown(); AssertCleanShutdown(session);
    }

    [TestMethod]
    public void InvalidDimensionsAndModifiedArtifactsAreRejectedBeforeGuestExecution()
    {
        byte[] file = ReadModule();
        Assert.AreEqual("Spheres", SupportedModules.Identify(file));
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(0, 480)));
        Assert.Throws<ArgumentException>(() => SupportedModules.Open(file, new(640, 2049)));
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static void Capture(AfterDarkSession<SpheresState> session, int draw)
    {
        string? directory = Environment.GetEnvironmentVariable("AFTER_DARKER_SPHERES_CAPTURE");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, $"frame-{draw:D4}.png"));
        PngWriter.Write(stream, 640, 480, session.CopyPixels());
    }

    private static void AssertCleanShutdown(AfterDarkSession<SpheresState> session)
    {
        var result = session.GetPlaybackResult();
        Assert.AreEqual(0, result.LiveBrushes); Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual(AfterDarkSession<SpheresState>.InitialSp, result.Phases[^1].Registers.Sp);
        Assert.AreEqual(session.HostData, result.Phases[^1].Registers.Ds);
    }
}
