using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Throughput.Helpers;
using Throughput.Services;

namespace Throughput.Windows;

/// <summary>
/// Popup shown when a newer release is available. Presents the release notes and
/// downloads + applies the update in place.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private CancellationTokenSource? _cts;
    private string? _downloadedFile;

    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;

        CurrentVersionText.Text = AppInfo.DisplayVersion;
        NewVersionText.Text = $"v{info.Version.Major}.{info.Version.Minor}.{info.Version.Build}";
        ReleaseNameText.Text = string.IsNullOrWhiteSpace(info.ReleaseName) ? "What's new" : info.ReleaseName;

        MarkdownRenderer.Render(
            string.IsNullOrWhiteSpace(info.Notes) ? "A new version of Throughput is available." : info.Notes,
            NotesPanel);

        Closing += (_, _) => _cts?.Cancel();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AcrylicGlass.Enable(this); // frosted-glass backdrop, matching the dashboard
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        // No downloadable asset for this build type - send the user to the release page.
        if (string.IsNullOrEmpty(_info.AssetUrl))
        {
            OpenUrl(_info.HtmlUrl);
            Close();
            return;
        }

        InfoActions.Visibility = Visibility.Collapsed;
        DownloadActions.Visibility = Visibility.Visible;
        DownloadStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
        DownloadStatusText.Text = "Downloading update…";

        _cts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.Stage == UpdateStage.Verifying)
            {
                DownloadProgress.Value = 100;
                DownloadPercentText.Text = "100%";
                DownloadStatusText.Text = "Verifying download…";
                DownloadSizeText.Text = "Checking file integrity";
                DownloadRateText.Text = "";
                return;
            }

            DownloadProgress.Value = p.Percent;
            DownloadPercentText.Text = $"{p.Percent:F0}%";
            DownloadSizeText.Text = p.TotalBytes > 0
                ? $"{FormatSize(p.BytesReceived)} of {FormatSize(p.TotalBytes)}"
                : FormatSize(p.BytesReceived);

            // The first moments cover TLS setup and TCP slow-start, so the rate and
            // ETA are wildly pessimistic there. Wait until the transfer settles.
            if (p.BytesReceived < 512 * 1024)
            {
                DownloadRateText.Text = "Starting…";
            }
            else
            {
                DownloadRateText.Text = p.Remaining is { } eta
                    ? $"{FormatRate(p.BytesPerSecond)}  ·  {FormatEta(eta)}"
                    : FormatRate(p.BytesPerSecond);
            }
        });

        try
        {
            _downloadedFile = await App.Updater.DownloadAsync(_info, progress, _cts.Token);
            ShowReadyToInstall();
        }
        catch (OperationCanceledException)
        {
            // User closed the window mid-download; nothing to do.
        }
        catch (Exception ex)
        {
            DownloadActions.Visibility = Visibility.Collapsed;
            InfoActions.Visibility = Visibility.Visible;
            System.Windows.MessageBox.Show(this,
                $"The update couldn't be downloaded:\n\n{ex.Message}\n\nYou can try again or download it from GitHub.",
                "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Download finished: hand control back to the user rather than restarting the
    /// app under them. If they minimized and went back to work, nudge via the tray.
    /// </summary>
    private void ShowReadyToInstall()
    {
        DownloadActions.Visibility = Visibility.Collapsed;
        InstallActions.Visibility = Visibility.Visible;

        if (WindowState == WindowState.Minimized || !IsActive)
        {
            App.NotifyUpdateReady(_info);
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadedFile)) return;

        InstallButton.IsEnabled = false;
        ReadyText.Text = "Installing… the app will restart.";

        try
        {
            UpdateService.ApplyUpdate(_downloadedFile);
            App.ExitApplication();
        }
        catch (Exception ex)
        {
            InstallButton.IsEnabled = true;
            ReadyText.Text = "Update downloaded and verified";
            System.Windows.MessageBox.Show(this,
                $"The update couldn't be installed:\n\n{ex.Message}",
                "Install failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Minimize_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        WindowState = WindowState.Minimized;
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.SaveSkippedVersion(
            $"{_info.Version.Major}.{_info.Version.Minor}.{_info.Version.Build}");
        Close();
    }

    private void ViewOnGitHub_Click(object sender, RoutedEventArgs e) => OpenUrl(_info.HtmlUrl);

    private static string FormatSize(long bytes) =>
        bytes >= 1L << 20 ? $"{bytes / 1048576.0:F1} MB" : $"{bytes / 1024.0:F0} KB";

    private static string FormatRate(double bytesPerSecond) =>
        bytesPerSecond >= 1048576
            ? $"{bytesPerSecond / 1048576.0:F1} MB/s"
            : $"{bytesPerSecond / 1024.0:F0} KB/s";

    private static string FormatEta(TimeSpan eta) =>
        eta.TotalSeconds < 60
            ? $"{eta.TotalSeconds:F0}s left"
            : $"{eta.TotalMinutes:F0}m {eta.Seconds}s left";

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }
}
