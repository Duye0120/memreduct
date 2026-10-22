using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private async void Update_Click(object sender, RoutedEventArgs args) => await CheckUpdateAsync(false);

    private async Task CheckUpdateAsync(bool installRequested)
    {
        if (_checking || _cleaning || _restartPromptOpen) return;
        _restartPromptOpen = true;
        UpdateButton.IsEnabled = false;
        RefreshOptions();
        try
        {
            UpdateStatus.Text = UiText.Get("UpdateChecking");
            var release = await UpdateService.CheckAsync();
            if (release is null) { UpdateStatus.Text = UiText.Get("UpdateNone"); return; }
            UpdateStatus.Text = UiText.Get("UpdateAvailable", release.Version);
            if (!Installation.IsInstalled)
            {
                UpdateStatus.Text += " " + UiText.Get("UpdatePortable");
                return;
            }
            if (!installRequested)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.ActualTheme,
                    Title = UiText.Get("UpdateAvailable", release.Version), Content = UiText.Get("UpdateConfirm"),
                    PrimaryButtonText = UiText.Get("UpdateInstall"), CloseButtonText = UiText.Get("Later"),
                    DefaultButton = ContentDialogButton.Close, CornerRadius = new(10 / Shell.XamlRoot.RasterizationScale)
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            if (!MemoryEngine.IsAdministrator) { RelaunchElevated("--update"); return; }
            UpdateStatus.Text = UiText.Get("UpdateDownloading");
            var installer = await UpdateService.DownloadAsync(release);
            if (!SaveSettings()) return;
            UpdateService.StartInstaller(installer);
            ExitWindow();
        }
        catch (Exception error) { UpdateStatus.Text = UiText.Get("UpdateFailed"); ReportIssue(UpdateStatus.Text, error.Message); }
        finally
        {
            _restartPromptOpen = false;
            if (!_exitRequested) { UpdateButton.IsEnabled = true; RefreshOptions(); }
        }
    }
}
