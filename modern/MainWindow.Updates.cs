using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private bool _updateBusy;
    private string? _downloadedInstaller;

    private async void Update_Click(object sender, RoutedEventArgs args) => await CheckUpdateAsync();

    private async Task CheckUpdateAsync()
    {
        if (_checking || _cleaning || _restartPromptOpen || _updateBusy) return;
        _updateBusy = true;
        UpdateButton.IsEnabled = false;
        try
        {
            if (_downloadedInstaller is not null)
            {
                _restartPromptOpen = true;
                RefreshOptions();
                if (!SaveSettings()) return;
                UpdateStatus.Text = UiText.Get("UpdateInstalling");
                await UpdateService.RequestInstallAsync(_downloadedInstaller);
                ExitWindow();
                return;
            }
            UpdateStatus.Text = UiText.Get("UpdateChecking");
            var release = await UpdateService.CheckAsync();
            if (release is null) { UpdateStatus.Text = UiText.Get("UpdateNone"); return; }
            UpdateStatus.Text = UiText.Get("UpdateAvailable", release.Version);
            if (!Installation.IsInstalled)
            {
                UpdateStatus.Text += " " + UiText.Get("UpdatePortable");
                return;
            }
            _restartPromptOpen = true;
            RefreshOptions();
            try
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.ActualTheme,
                    Title = UiText.Get("UpdateAvailable", release.Version), Content = UiText.Get("UpdateConfirm"),
                    PrimaryButtonText = UiText.Get("UpdateDownload"), CloseButtonText = UiText.Get("Later"),
                    DefaultButton = ContentDialogButton.Close, CornerRadius = new(10 / Shell.XamlRoot.RasterizationScale)
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            finally { _restartPromptOpen = false; RefreshOptions(); }
            UpdateStatus.Text = UiText.Get("UpdateDownloading");
            _downloadedInstaller = await UpdateService.DownloadAsync(release);
            UpdateButton.Content = UiText.Get("UpdateInstall");
            UpdateStatus.Text = UiText.Get("UpdateReady");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { UpdateStatus.Text = UiText.Get("UpdateCanceled"); }
        catch (Exception error)
        {
            _downloadedInstaller = null;
            UpdateButton.Content = UiText.Get("UpdateButton.Content");
            UpdateStatus.Text = UiText.Get("UpdateFailed");
            ReportIssue(UpdateStatus.Text, error.Message);
        }
        finally
        {
            _updateBusy = false;
            _restartPromptOpen = false;
            if (!_exitRequested) { UpdateButton.IsEnabled = true; RefreshOptions(); }
        }
    }
}
