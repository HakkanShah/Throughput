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
        var progress = new Progress<double>(p =>
        {
            DownloadProgress.Value = p;
            DownloadPercentText.Text = $"{p:F0}%";
        });

        try
        {
            string file = await App.Updater.DownloadAsync(_info, progress, _cts.Token);

            DownloadStatusText.Text = "Installing… the app will restart.";
            UpdateService.ApplyUpdate(file);
            App.ExitApplication();
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

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.SaveSkippedVersion(
            $"{_info.Version.Major}.{_info.Version.Minor}.{_info.Version.Build}");
        Close();
    }

    private void ViewOnGitHub_Click(object sender, RoutedEventArgs e) => OpenUrl(_info.HtmlUrl);

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
