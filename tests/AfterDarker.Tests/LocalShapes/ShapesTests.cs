using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalShapes;

/// <summary>Opt-in original-code execution. Private module bytes and optional PNGs never enter test output.</summary>
[TestClass]
[TestCategory("LocalShapes")]
public sealed class ShapesTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_SHAPES")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_SHAPES before enabling TestLocalShapes."));

    [TestMethod]
    public void OriginalShapesDrawWithRandomColorsAndBalanceEveryTemporaryBrush()
    {
        using var session = (AfterDarkSession<ShapesState>)SupportedModules.Open(ReadModule(), new());
        session.Initialize();
        uint expectedSeed = session.GetResult().Phases[^1].State.RandomSeed;
        Assert.AreEqual(AfterDarkHostContract.RgbPaletteRequest, session.Blank().StoredAx);
        string? captureDirectory = Environment.GetEnvironmentVariable("AFTER_DARKER_SHAPES_CAPTURE");
        if (captureDirectory is not null) Directory.CreateDirectory(captureDirectory);
        var colors = new HashSet<uint>();
        byte[] pixels = new byte[session.PixelByteCount];
        int rectangles = 0, ellipses = 0;
        for (int draw = 1; draw <= 1000; draw++)
        {
            var phase = session.DrawFrame();
            // Independent transcription of the original S3:00A4 generator and
            // S2:00E5..01CA drawing decisions, not a second host animation.
            ushort blue = (ushort)(NextRandom(ref expectedSeed) % 255);
            ushort red = (ushort)(NextRandom(ref expectedSeed) % 255);
            ushort green = (ushort)(NextRandom(ref expectedSeed) % 255);
            short left = (short)(NextRandom(ref expectedSeed) % (640 + 80) - 100);
            short top = (short)(NextRandom(ref expectedSeed) % (480 + 80) - 100);
            short right = (short)(left + NextRandom(ref expectedSeed) % (640 / 2));
            short bottom = (short)(top + NextRandom(ref expectedSeed) % (480 / 2));
            bool expectsEllipse = NextRandom(ref expectedSeed) % 100 < 50;
            Assert.AreEqual(expectedSeed, phase.State.RandomSeed);
            var calls = session.GetResult().Calls;
            var brush = calls.Last(call => call.Binding.Implementation == Win16Imports.Handler.CreateSolidBrush);
            uint color = (uint)brush.Arguments[0] << 16 | brush.Arguments[1];
            Assert.AreEqual(2u, color >> 24);
            Assert.AreEqual(0x02000000u | (uint)blue << 16 | (uint)green << 8 | red, color);
            colors.Add(color);
            var shape = calls.Last(call => call.Binding.Implementation is Win16Imports.Handler.Rectangle or Win16Imports.Handler.Ellipse);
            Assert.AreEqual(expectsEllipse ? Win16Imports.Handler.Ellipse : Win16Imports.Handler.Rectangle, shape.Binding.Implementation);
            CollectionAssert.AreEqual(new ushort[] { AfterDarkHostContract.ReservedHdc,
                unchecked((ushort)left), unchecked((ushort)top), unchecked((ushort)right), unchecked((ushort)bottom) }, shape.Arguments.ToArray());
            if (shape.Binding.Implementation == Win16Imports.Handler.Rectangle) rectangles++; else ellipses++;
            Assert.AreEqual((ushort)1, shape.After.Ax);
            Assert.AreEqual(0, session.LiveBrushCount);
            Assert.AreEqual(0, session.LivePenCount);
            Assert.IsLessThanOrEqualTo(256, calls.Count);
            if (captureDirectory is not null && draw <= 120)
            {
                session.CopyPixelsTo(pixels);
                using var stream = File.Create(Path.Combine(captureDirectory, $"frame-{draw:D4}.png"));
                PngWriter.Write(stream, 640, 480, pixels);
            }
        }
        Assert.IsGreaterThan(100, colors.Count);
        Assert.IsGreaterThan(100, rectangles); Assert.IsGreaterThan(100, ellipses);
        var result = session.GetPlaybackResult();
        Assert.AreEqual(1000L, result.ImportCalls["GDI!CreateSolidBrush (#66)"]);
        Assert.AreEqual(1000L, result.ImportCalls["GDI!DeleteObject (#69)"]);
        Assert.AreEqual((long)rectangles, result.ImportCalls["GDI!Rectangle (#27)"]);
        Assert.AreEqual((long)ellipses, result.ImportCalls["GDI!Ellipse (#24)"]);
        Assert.AreEqual(1, result.PeakBrushes);
        Console.WriteLine($"Shapes: {result.Instructions} instructions, {rectangles} rectangles, {ellipses} ellipses, {colors.Count} colors.");
        Assert.IsTrue(session.CopyPixels().Any(component => component != 0));
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void IndependentGuestsProduceTheSamePixelsAndIgnoreTheUnrelatedSpeedOption()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int draw = 0; draw < 120; draw++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            Assert.IsTrue(firstPixels.AsSpan().SequenceEqual(secondPixels), $"Images differ at draw {draw}.");
        }
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(5, 5)]
    [DataRow(321, 239)]
    [DataRow(2048, 2048)]
    public void SupportedDimensionsFinishDrawingAndCleanup(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 100; draw++) session.DrawFrame();
        session.Shutdown(); AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void UnsafeSmallDimensionsAndModifiedArtifactsAreRejectedBeforeExecution()
    {
        byte[] file = ReadModule();
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportedModules.Open(file, new(4, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportedModules.Open(file, new(1, 480)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportedModules.Open(file, new(640, 1)));
        using var session = SupportedModules.Open(file, new());
        session.Initialize(); session.Blank(); session.Shutdown(); AssertCleanShutdown(session.GetPlaybackResult());
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static int NextRandom(ref uint seed)
    {
        seed = unchecked(seed * 0x343FDu + 0x269EC3u);
        return (int)((seed >> 16) & 0x7FFF);
    }

    private static void AssertCleanShutdown(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LiveBrushes); Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name); Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }
}
