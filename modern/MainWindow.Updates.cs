using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private bool _updateBusy;
    private string? _downloadedInstaller;
    private CancellationTokenSource? _updateCancellation;

    private async void Update_Click(object sender, RoutedEventArgs args)
    {
        if (_updateCancellation is not null)
        {
            _updateCancellation.Cancel();
            SettingsPage.UpdateButton.IsEnabled = false;
            return;
        }
        await CheckUpdateAsync();
    }

    private async Task CheckUpdateAsync()
    {
        if (_exitRequested || _checking || _cleaning || _restartPromptOpen || _updateBusy) return;
        _updateBusy = true;
        _updateCancellation = new CancellationTokenSource();
        SettingsPage.UpdateButton.IsEnabled = false;
        SettingsPage.UpdateProgress.Visibility = Visibility.Collapsed;
        try
        {
            if (_downloadedInstaller is not null)
            {
                _restartPromptOpen = true;
                RefreshOptions();
                if (!SaveSettings()) return;
                SettingsPage.UpdateStatus.Text = UiText.Get("UpdateInstalling");
                await UpdateService.RequestInstallAsync(_downloadedInstaller);
                if (_exitRequested) return;
                ExitWindow();
                return;
            }
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateChecking");
            var release = await UpdateService.CheckAsync(_updateCancellation.Token);
            if (_exitRequested) return;
            if (release is null) { SettingsPage.UpdateStatus.Text = UiText.Get("UpdateNone"); return; }
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateAvailable", release.Version);
            if (!Installation.IsInstalled)
            {
                SettingsPage.UpdateStatus.Text += " " + UiText.Get("UpdatePortable");
                return;
            }
            try { Installation.RequireProtectedInstall(); }
            catch (UnauthorizedAccessException)
            {
                SettingsPage.UpdateStatus.Text += " " + UiText.Get("UpdateUnprotected");
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
            if (_exitRequested) return;
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateDownloading");
            SettingsPage.UpdateProgress.Value = 0;
            SettingsPage.UpdateProgress.Visibility = Visibility.Visible;
            SettingsPage.UpdateButton.Content = UiText.Get("UpdateCancelDownload");
            SettingsPage.UpdateButton.IsEnabled = true;
            var progress = new Progress<long>(received =>
            {
                if (_exitRequested || _updateCancellation is null || _updateCancellation.IsCancellationRequested) return;
                SettingsPage.UpdateProgress.Value = received * 100.0 / release.Size;
                SettingsPage.UpdateStatus.Text = UiText.Get("UpdateDownloadProgress", SettingsPage.UpdateProgress.Value,
                    MemorySnapshot.Bytes((ulong)received), MemorySnapshot.Bytes((ulong)release.Size));
            });
            _downloadedInstaller = await UpdateService.DownloadAsync(release, progress, _updateCancellation.Token);
            if (_exitRequested) return;
            SettingsPage.UpdateProgress.Value = 100;
            SettingsPage.UpdateButton.Content = UiText.Get("UpdateInstall");
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateReady");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { if (!_exitRequested) SettingsPage.UpdateStatus.Text = UiText.Get("UpdateCanceled"); }
        catch (OperationCanceledException) when (_updateCancellation?.IsCancellationRequested == true)
        {
            if (_exitRequested) return;
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateDownloadCanceled");
            SettingsPage.UpdateButton.Content = UiText.Get("UpdateButton.Content");
        }
        catch (Exception error)
        {
            if (_exitRequested) return;
            _downloadedInstaller = null;
            SettingsPage.UpdateButton.Content = UiText.Get("UpdateButton.Content");
            SettingsPage.UpdateStatus.Text = UiText.Get("UpdateFailed");
            ReportIssue(SettingsPage.UpdateStatus.Text, error.Message);
        }
        finally
        {
            _updateCancellation?.Dispose();
            _updateCancellation = null;
            _updateBusy = false;
            _restartPromptOpen = false;
            if (!_exitRequested) { SettingsPage.UpdateProgress.Visibility = Visibility.Collapsed; SettingsPage.UpdateButton.IsEnabled = true; RefreshOptions(); }
        }
    }
}
