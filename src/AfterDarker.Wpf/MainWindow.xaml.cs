using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AfterDarker.Core.Ne;
using AfterDarker.Runtime;
using Microsoft.Win32;

namespace AfterDarker.Wpf;

public partial class MainWindow : Window
{
    private const int PixelWidth = 640, PixelHeight = 480, Stride = PixelWidth * 3;
    private readonly WriteableBitmap bitmap = new(PixelWidth, PixelHeight, 96, 96, PixelFormats.Rgb24, null);
    private readonly byte[] display = new byte[Stride * PixelHeight];
    private readonly DispatcherTimer presenter = new() { Interval = TimeSpan.FromSeconds(1.0 / 60) };
    private LatestFrameMailbox? mailbox;
    private CancellationTokenSource? stop;
    private Task<PlaybackResult>? running;
    private PlaybackResult? lastResult;
    private Exception? runError;
    private readonly TaskCompletionSource windowClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool closeAllowed, closing, smokeFinishing, loading;
    private readonly string? smokeDirectory;
    private readonly string? smokeSwitchPath;
    private int presented;
    private FrameInfo? latest;
    private string? selectedModuleName;

    public MainWindow(string[] args)
    {
        InitializeComponent();
        Screen.Source = bitmap;
        foreach (ushort speed in new ushort[] { 0, 25, 50, 75, 100 })
            Speed.Items.Add(new ComboBoxItem { Content = $"Speed {speed}", Tag = speed });
        Speed.SelectedIndex = 4;
        if (args.Length is 3 or 4 && args[0] == "--smoke")
        {
            ModulePath.Text = args[1];
            smokeDirectory = Path.GetFullPath(args[2]);
            smokeSwitchPath = args.Length == 4 ? args[3] : null;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
        else ModulePath.Text = args.Length == 1 ? args[0] : FindLocalModule();
        presenter.Tick += Present;
        Closing += OnClosing;
        Closed += (_, _) => { presenter.Stop(); windowClosed.TrySetResult(); };
        Loaded += async (_, _) =>
        {
            if (smokeDirectory is not null) _ = WatchSmokeTimeoutAsync();
            if (File.Exists(ModulePath.Text) || smokeDirectory is not null) await StartAsync();
        };
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "After Dark module (*.ad)|*.ad|All files (*.*)|*.*", Title = "Load After Dark module" };
        if (dialog.ShowDialog(this) != true) return;
        try { await LoadModuleAsync(dialog.FileName); }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Unable to load AD file", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private async void Run_Click(object sender, RoutedEventArgs e) => await StartAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await StopAsync();

    private async Task StartAsync(byte[]? prepared = null)
    {
        if (running is not null) return;
        stop = new CancellationTokenSource();
        mailbox = new(display.Length);
        presented = 0; latest = null; lastResult = null; runError = null;
        Array.Clear(display);
        bitmap.WritePixels(new Int32Rect(0, 0, PixelWidth, PixelHeight), display, Stride, 0, 0);
        SetBusy(true);
        Status.Text = "Loading AD module…";
        string path = ModulePath.Text.Trim().Trim('"');
        ushort speed = (ushort)((ComboBoxItem)Speed.SelectedItem).Tag;
        running = LoadAndRunAsync(path, new(PixelWidth, PixelHeight, speed), mailbox, stop.Token, prepared);
        presenter.Start();
        try
        {
            lastResult = await running;
            Status.Text = $"Stopped. {latest?.ChangedFrames ?? 0:N0} image changes; CLOSE / WEP returned; native guest released.";
        }
        catch (OperationCanceledException) { Status.Text = "Stopped."; }
        catch (Exception error)
        {
            runError = error;
            Status.Text = $"Unable to run: {error.Message}";
            if (smokeDirectory is not null) await FinishSmokeAsync(error);
        }
        finally
        {
            presenter.Stop();
            running = null;
            stop.Dispose(); stop = null;
            if (!closing) SetBusy(false);
        }
    }

    private async Task<PlaybackResult> LoadAndRunAsync(string path, PlaybackOptions options,
        LatestFrameMailbox frames, CancellationToken cancellationToken, byte[]? prepared)
    {
        byte[] file = prepared ?? await ReadSupportedFileAsync(path, cancellationToken);
        string name = SupportedModules.Identify(file);
        selectedModuleName = name;
        Speed.ToolTip = name switch
        {
            "Rainstorm" => "Rainstorm uses fixed strength, lightning, drop count and wind settings; it has no speed control.",
            "Fade Away" => "Fade Away uses its Radar effect on a white starting image; it has no speed control.",
            "Lasers" => "Lasers uses three rays, a fixed trail width and fixed color-change speed in this version.",
            "Magic" => "Magic uses a 100-line trail, horizontal mirroring, and fixed line/color speeds in this version.",
            "String Theory" => "String Theory uses three groups of 100 strings, color speed 96, and Clear Screen First.",
            "Zot!" => "Zot! uses Few forks and Stormy frequency. Brief lightning images are presented during its drawing calls.",
            _ => "Original module speed; applies on Run"
        };
        Title = $"After Darker — {name}";
        ModuleTitle.Text = name == "Fade Away" ? "FADE AWAY · RADAR" : name.ToUpperInvariant();
        return await AfterDarkPlayback.RunAsync(file, options, frames, cancellationToken);
    }

    private static async Task<byte[]> ReadSupportedFileAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        SupportedModules.Identify(bytes); // reject unsupported versions BEFORE creating a guest
        return bytes;
    }

    private async Task LoadModuleAsync(string path)
    {
        if (loading || closing) return;
        loading = true; LoadMenu.IsEnabled = false;
        SetBusy(running is not null);
        try
        {
            // Validation precedes stopping: a mistaken selection leaves the
            // existing screensaver alive. The new guest is created only after
            // the old worker has run CLOSE/WEP and released its native engine.
            byte[] bytes = await ReadSupportedFileAsync(path, CancellationToken.None);
            await StopAsync();
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (closing || windowClosed.Task.IsCompleted) return;
            ModulePath.Text = path;
            _ = StartAsync(bytes);
        }
        finally
        {
            loading = false; LoadMenu.IsEnabled = !closing;
            if (!closing) SetBusy(running is not null);
        }
    }

    private async Task StopAsync()
    {
        stop?.Cancel();
        var task = running;
        if (task is null) return;
        StopButton.IsEnabled = false;
        Status.Text = "Stopping…";
        try
        {
            lastResult = await task;
            Status.Text = "Stopped. CLOSE / WEP returned; native guest released.";
        }
        catch { /* StartAsync displays the error. Closing must still release the window. */ }
    }

    private void Present(object? sender, EventArgs e)
    {
        // Only this dispatcher thread touches the WriteableBitmap. The worker
        // copies into a one-frame mailbox; no per-frame dispatcher work is queued.
        if (mailbox is null || !mailbox.TryCopyTo(display, out var frame)) return;
        bitmap.WritePixels(new Int32Rect(0, 0, PixelWidth, PixelHeight), display, Stride, 0, 0);
        latest = frame;
        if (presented < int.MaxValue) presented++;
        Status.Text = $"Running · 640 × 480 · {frame!.ChangedFrames:N0} image changes · {frame.DrawCalls:N0} guest calls";
        // Transient effects legitimately alternate with black. Capture an actual
        // visible effect after the presentation threshold, not its erased image.
        if (smokeDirectory is not null && presented >= 30 && frame.ChangedFrames > 0 && display.Any(component => component != 0))
            _ = FinishSmokeAsync(null);
    }

    private void SetBusy(bool busy)
    {
        ModulePath.IsEnabled = BrowseButton.IsEnabled = RunButton.IsEnabled = !busy && !loading && !closing;
        Speed.IsEnabled = !busy && !loading && !closing && selectedModuleName is not ("Rainstorm" or "Fade Away" or "Lasers" or "Magic" or "String Theory" or "Zot!");
        StopButton.IsEnabled = busy;
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closeAllowed || running is null) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        SetBusy(true); StopButton.IsEnabled = false; LoadMenu.IsEnabled = false;
        await StopAsync();
        closeAllowed = true;
        Close();
    }

    private static string FindLocalModule()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AfterDarker.sln")))
                return Path.Combine(dir.FullName, "ad", "Mondrian.ad");
        return "";
    }

    // Opt-in automated acceptance run uses the actual WPF dispatcher, bitmap,
    // presenter and worker. Outputs are local artifacts, never source assets.
    private async Task WatchSmokeTimeoutAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        if (!smokeFinishing) await FinishSmokeAsync(new TimeoutException("WPF did not present 30 frames within 30 seconds."));
    }
    private async Task FinishSmokeAsync(Exception? error)
    {
        if (smokeFinishing) return;
        smokeFinishing = true;
        presenter.Stop();
        int firstPresented = presented;
        FrameInfo? firstFrame = latest;
        string firstHash = Convert.ToHexString(SHA256.HashData(display));
        string? switchedFrom = null;
        try
        {
            Directory.CreateDirectory(smokeDirectory!);
            if (error is null)
            {
                // Exercise the same load path as the menu while a guest is
                // active. Unsupported content must leave that worker untouched.
                string unsupported = Path.Combine(smokeDirectory!, "unsupported.ad");
                File.WriteAllBytes(unsupported, []);
                var originalWorker = running;
                bool rejected = false;
                try { await LoadModuleAsync(unsupported); }
                catch (NotSupportedException) { rejected = true; }
                if (!rejected || running != originalWorker || running is null || running.IsCompleted)
                    throw new InvalidOperationException("Unsupported selection disturbed the running guest.");
                byte[] readback = new byte[display.Length];
                bitmap.CopyPixels(readback, Stride, 0);
                if (!readback.AsSpan().SequenceEqual(display) || !readback.Any(b => b != 0))
                    throw new InvalidOperationException("WPF readback differs from published pixels or remained blank.");
                SavePng(bitmap, "frame.png");
                UpdateLayout();
                var visual = new RenderTargetBitmap((int)Root.ActualWidth, (int)Root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                // Render the content at the origin: Root's window-relative
                // margin must not crop the right/bottom edges of this artifact.
                var content = new DrawingVisual();
                using (var context = content.RenderOpen())
                {
                    var bounds = new Rect(0, 0, Root.ActualWidth, Root.ActualHeight);
                    context.DrawRectangle(Background, null, bounds);
                    context.DrawRectangle(new VisualBrush(Root), null, bounds);
                }
                visual.Render(content);
                SavePng(visual, "window.png");
            }
            await StopAsync();
            if (error is null)
            {
                VerifyShutdown();
                // Let the original StartAsync finish its UI cleanup before
                // exercising Run again, just as the enabled Run button does.
                await Dispatcher.Yield(DispatcherPriority.Background);
                if (running is not null || !RunButton.IsEnabled)
                    throw new InvalidOperationException("Stop did not return the window to its runnable state.");
                _ = StartAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (presented < 3)
                {
                    if (runError is not null) throw new InvalidOperationException("Restart failed.", runError);
                    await Task.Delay(10, deadline.Token);
                }
                if (running is null) throw new InvalidOperationException("Restart did not retain a running guest.");
                if (smokeSwitchPath is not null)
                {
                    var previousWorker = running;
                    await LoadModuleAsync(smokeSwitchPath);
                    var previousResult = await previousWorker;
                    VerifyShutdown(previousResult);
                    switchedFrom = previousResult.ModuleName;
                    while (presented < 3)
                    {
                        if (runError is not null) throw new InvalidOperationException("Module switch failed.", runError);
                        await Task.Delay(10, deadline.Token);
                    }
                }
                Close(); // exercise the actual Closing handler while playback is active
                await windowClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                VerifyShutdown();
            }
            File.WriteAllText(Path.Combine(smokeDirectory!, "report.json"), JsonSerializer.Serialize(new
            {
                Status = error is null ? "passed" : "failed", Error = error?.Message,
                Presented = firstPresented, Frame = firstFrame, RgbSha256 = firstHash,
                RestartPresented = presented, ClosedWhilePlaying = windowClosed.Task.IsCompletedSuccessfully,
                ShutdownPhases = lastResult?.Phases.TakeLast(2).Select(p => new { p.Name, p.StoredAx, p.Registers.Sp, p.Registers.Ds }),
                Module = lastResult?.ModuleName, SwitchedFrom = switchedFrom, Instructions = lastResult?.Instructions, OutstandingLocks = lastResult?.OutstandingLocks,
                LivePens = lastResult?.LivePens, PeakPens = lastResult?.PeakPens,
                LocalHeap = lastResult?.LocalHeap,
                IntermediateFrames = lastResult?.IntermediateFrames,
                UiThread = Environment.CurrentManagedThreadId
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception failure)
        {
            error = failure;
            try { File.WriteAllText(Path.Combine(smokeDirectory!, "failure.txt"), failure.ToString()); } catch { }
        }
        finally
        {
            await StopAsync();
            closeAllowed = true;
            Application.Current.Shutdown(error is null ? 0 : 1);
        }
        void SavePng(BitmapSource source, string name)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(Path.Combine(smokeDirectory!, name)); encoder.Save(stream);
        }
        void VerifyShutdown(PlaybackResult? completed = null)
        {
            if (runError is not null) throw new InvalidOperationException("Playback failed during shutdown.", runError);
            var result = completed ?? lastResult;
            if (result is null || result.Phases.Count < 2 || result.Phases[^2].Name != "CLOSE" ||
                result.Phases[^1].Name != "WEP" || result.Phases[^1].StoredAx != 1 || result.OutstandingLocks != 0 || result.LivePens != 0 ||
                result.LocalHeap?.Allocations.Count > 0 || result.LocalHeap?.OutstandingLocks > 0)
                throw new InvalidOperationException("Original guest shutdown did not complete with WEP success and balanced locks.");
        }
    }
}
