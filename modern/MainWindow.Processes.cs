using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private readonly ObservableCollection<ProcessMemoryEntry> _processRows = [];
    private bool _processBusy;

    private async void Processes_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (!_checking && _processRows.Count == 0) await RefreshProcessesAsync();
    }

    private async void RefreshProcesses_Click(object sender, RoutedEventArgs args) => await RefreshProcessesAsync();

    private async Task RefreshProcessesAsync()
    {
        if (_exitRequested || _checking || _processBusy) return;
        _processBusy = true;
        ProcessRefreshButton.IsEnabled = false;
        ProcessStatus.Text = UiText.Get("ProcessesReading");
        try
        {
            var snapshot = await Task.Run(ProcessMemory.Read);
            if (_exitRequested) return;
            _processRows.Clear();
            foreach (var entry in snapshot.Entries) _processRows.Add(entry);
            ProcessStatus.Text = UiText.Get("ProcessesUpdated", DateTime.Now, snapshot.Entries.Count)
                + (snapshot.Unavailable == 0 ? "" : UiText.Get("ProcessesUnavailable", snapshot.Unavailable));
        }
        catch (Exception error)
        {
            if (!_exitRequested) ProcessStatus.Text = UiText.Get("ProcessesFailed") + " " + error.Message;
        }
        finally
        {
            _processBusy = false;
            if (!_exitRequested) ProcessRefreshButton.IsEnabled = true;
        }
    }
}
