using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AfterDarker.Core.AfterDark;
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
    private Task<MondrianSession.Result>? running;
    private MondrianSession.Result? lastResult;
    private bool closeAllowed, closing, smokeFinishing;
    private readonly string? smokeDirectory;
    private int presented;
    private FrameInfo? latest;

    public MainWindow(string[] args)
    {
        InitializeComponent();
        Screen.Source = bitmap;
        foreach (ushort speed in new ushort[] { 0, 25, 50, 75, 100 })
            Speed.Items.Add(new ComboBoxItem { Content = $"Speed {speed}", Tag = speed });
        Speed.SelectedIndex = 4;
        if (args is ["--smoke", var module, var destination])
        {
            ModulePath.Text = module;
            smokeDirectory = Path.GetFullPath(destination);
        }
        else ModulePath.Text = args.Length == 1 ? args[0] : FindLocalModule();
        presenter.Tick += Present;
        Closing += OnClosing;
        Closed += (_, _) => presenter.Stop();
        Loaded += async (_, _) =>
        {
            if (smokeDirectory is not null) _ = WatchSmokeTimeoutAsync();
            if (File.Exists(ModulePath.Text) || smokeDirectory is not null) await StartAsync();
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "After Dark module (*.ad)|*.ad|All files (*.*)|*.*", Title = "Select your local Mondrian module" };
        if (dialog.ShowDialog(this) == true) ModulePath.Text = dialog.FileName;
    }
    private async void Run_Click(object sender, RoutedEventArgs e) => await StartAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await StopAsync();

    private async Task StartAsync()
    {
        if (running is not null) return;
        stop = new CancellationTokenSource();
        mailbox = new(display.Length);
        presented = 0; latest = null; lastResult = null;
        Array.Clear(display);
        bitmap.WritePixels(new Int32Rect(0, 0, PixelWidth, PixelHeight), display, Stride, 0, 0);
        SetBusy(true);
        Status.Text = "Loading original Mondrian…";
        string path = ModulePath.Text.Trim().Trim('"');
        ushort speed = (ushort)((ComboBoxItem)Speed.SelectedItem).Tag;
        running = LoadAndRunAsync(path, new(PixelWidth, PixelHeight, speed), mailbox, stop.Token);
        presenter.Start();
        try
        {
            lastResult = await running;
            Status.Text = $"Stopped. {latest?.ChangedFrames ?? 0:N0} image changes; native guest released.";
        }
        catch (OperationCanceledException) { Status.Text = "Stopped."; }
        catch (Exception error)
        {
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

    private static async Task<MondrianSession.Result> LoadAndRunAsync(string path, MondrianInitialization.Options options,
        LatestFrameMailbox frames, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] file = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(file, cancellationToken);
        return await MondrianPlayback.RunAsync(file, options, frames, cancellationToken);
    }

    private async Task StopAsync()
    {
        stop?.Cancel();
        var task = running;
        if (task is null) return;
        StopButton.IsEnabled = false;
        Status.Text = "Stopping…";
        try { lastResult = await task; }
        catch { /* StartAsync displays the error. Closing must still release the window. */ }
    }

    private void Present(object? sender, EventArgs e)
    {
        // Only this dispatcher thread touches the WriteableBitmap. The worker
        // copies into a one-frame mailbox; no per-frame dispatcher work is queued.
        if (mailbox is null || !mailbox.TryCopyTo(display, out var frame)) return;
        bitmap.WritePixels(new Int32Rect(0, 0, PixelWidth, PixelHeight), display, Stride, 0, 0);
        latest = frame;
        presented++;
        Status.Text = $"Running · 640 × 480 · {frame!.ChangedFrames:N0} image changes · {frame.DrawCalls:N0} guest calls · {frame.Rectangles} rectangles";
        if (smokeDirectory is not null && presented >= 30 && frame.ChangedFrames > 0) _ = FinishSmokeAsync(null);
    }

    private void SetBusy(bool busy)
    {
        ModulePath.IsEnabled = BrowseButton.IsEnabled = RunButton.IsEnabled = Speed.IsEnabled = !busy;
        StopButton.IsEnabled = busy;
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closeAllowed || running is null) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        SetBusy(true); StopButton.IsEnabled = false;
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
        try
        {
            Directory.CreateDirectory(smokeDirectory!);
            if (error is null)
            {
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
            File.WriteAllText(Path.Combine(smokeDirectory!, "report.json"), JsonSerializer.Serialize(new
            {
                Status = error is null ? "passed" : "failed", Error = error?.Message,
                Presented = presented, Frame = latest, RgbSha256 = Convert.ToHexString(SHA256.HashData(display)),
                Instructions = lastResult?.Instructions, OutstandingLocks = lastResult?.OutstandingLocks,
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
    }
}
