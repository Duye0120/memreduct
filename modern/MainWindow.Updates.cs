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
            UpdateButton.IsEnabled = false;
            return;
        }
        await CheckUpdateAsync();
    }

    private async Task CheckUpdateAsync()
    {
        if (_checking || _cleaning || _restartPromptOpen || _updateBusy) return;
        _updateBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateProgress.Visibility = Visibility.Collapsed;
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
            try { Installation.RequireProtectedInstall(); }
            catch (UnauthorizedAccessException)
            {
                UpdateStatus.Text += " " + UiText.Get("UpdateUnprotected");
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
            _updateCancellation = new CancellationTokenSource();
            UpdateProgress.Value = 0;
            UpdateProgress.Visibility = Visibility.Visible;
            UpdateButton.Content = UiText.Get("UpdateCancelDownload");
            UpdateButton.IsEnabled = true;
            var progress = new Progress<long>(received =>
            {
                if (_exitRequested || _updateCancellation is null || _updateCancellation.IsCancellationRequested) return;
                UpdateProgress.Value = received * 100.0 / release.Size;
                UpdateStatus.Text = UiText.Get("UpdateDownloadProgress", UpdateProgress.Value,
                    MemorySnapshot.Bytes((ulong)received), MemorySnapshot.Bytes((ulong)release.Size));
            });
            _downloadedInstaller = await UpdateService.DownloadAsync(release, progress, _updateCancellation.Token);
            if (_exitRequested) return;
            UpdateProgress.Value = 100;
            UpdateButton.Content = UiText.Get("UpdateInstall");
            UpdateStatus.Text = UiText.Get("UpdateReady");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { UpdateStatus.Text = UiText.Get("UpdateCanceled"); }
        catch (OperationCanceledException) when (_updateCancellation?.IsCancellationRequested == true)
        {
            if (_exitRequested) return;
            UpdateStatus.Text = UiText.Get("UpdateDownloadCanceled");
            UpdateButton.Content = UiText.Get("UpdateButton.Content");
        }
        catch (Exception error)
        {
            if (_exitRequested) return;
            _downloadedInstaller = null;
            UpdateButton.Content = UiText.Get("UpdateButton.Content");
            UpdateStatus.Text = UiText.Get("UpdateFailed");
            ReportIssue(UpdateStatus.Text, error.Message);
        }
        finally
        {
            _updateCancellation?.Dispose();
            _updateCancellation = null;
            _updateBusy = false;
            _restartPromptOpen = false;
            if (!_exitRequested) { UpdateProgress.Visibility = Visibility.Collapsed; UpdateButton.IsEnabled = true; RefreshOptions(); }
        }
    }
}
