using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace MemReduct.Fluent;

public sealed partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly bool _checking;
    private readonly DispatcherQueueTimer? _timer;
    private readonly TrayIcon? _tray;
    private bool _ready, _cleaning, _exitRequested, _sampleFailed;
    private bool _statsStacked;
    private double _cornerScale;
    private DateTimeOffset _lastAttempt;
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    internal bool CanStayInTray => _tray?.IsVisible == true;

    public MainWindow(AppSettings settings, bool checking)
    {
        _settings = settings;
        _checking = checking;
        _lastAttempt = settings.LastCleanUtc is { } last && last <= DateTimeOffset.UtcNow ? last : DateTimeOffset.UtcNow;
        InitializeComponent();
        Shell.Language = UiText.LanguageTag;
        IssueBanner.Closed += (_, _) => IssueBanner.Visibility = Visibility.Collapsed;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(handle) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(700 * scale), (int)(560 * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(560 * scale);
            presenter.PreferredMinimumHeight = (int)(440 * scale);
        }
        LoadSettings();
        if (!checking)
        {
            Shell.SizeChanged += (_, _) => ReflowMetrics(Shell.ActualWidth, _uiSettings.TextScaleFactor);
            _uiSettings.TextScaleFactorChanged += (_, _) => DispatcherQueue.TryEnqueue(() => ReflowMetrics(Shell.ActualWidth, _uiSettings.TextScaleFactor));
            try
            {
                _tray = new TrayIcon(handle, ShowWindow, ExitWindow);
                if (!_tray.IsVisible) ReportIssue(UiText.Get("TrayUnavailable"), UiText.Get("ExitOnClose"));
            }
            catch (Exception error) { ReportIssue(UiText.Get("TrayUnavailable"), error.Message + " " + UiText.Get("ExitOnClose")); }
            try { StartupSwitch.IsOn = StartupRegistration.IsEnabled(); }
            catch (Exception error) { ReportIssue(UiText.Get("StartupReadFailed"), error.Message); StartupSwitch.IsEnabled = false; }
            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, _) => Sample();
            _timer.Start();
        }
        _ready = true;
        RefreshOptions();
        if (!checking) Sample();
        AppWindow.Closing += (_, args) =>
        {
            if (!_exitRequested && !_checking && _settings.CloseToTray && _tray?.IsVisible == true)
            { args.Cancel = true; AppWindow.Hide(); }
        };
        Closed += (_, _) => { _timer?.Stop(); _tray?.Dispose(); ((App)Application.Current).Finish(); };
        Shell.Loaded += async (_, _) =>
        {
            foreach (var button in new[] { OverviewTab, SettingsTab, SidebarOverviewTab, SidebarSettingsTab })
            {
                var shadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
                shadow.Receivers.Add(button == OverviewTab || button == SettingsTab ? TopShadowReceiver : SidebarShadowReceiver);
                button.Shadow = shadow;
            }
            UpdateCornerRadii();
            Shell.XamlRoot.Changed -= XamlRoot_Changed;
            Shell.XamlRoot.Changed += XamlRoot_Changed;
            if (App.Arguments.Contains("--clean") && !checking) await CleanAsync(false);
        };
    }

    private void LoadSettings()
    {
        ThemeChoice.SelectedIndex = _settings.Theme == "Light" ? 1 : _settings.Theme == "Dark" ? 2 : 0;
        LanguageChoice.SelectedIndex = _settings.Language == "zh-CN" ? 1 : _settings.Language == "en-US" ? 2 : 0;
        PinSwitch.IsOn = _settings.AlwaysOnTop;
        TraySwitch.IsOn = _settings.CloseToTray;
        ConfirmSwitch.IsOn = _settings.ConfirmCleanup;
        WorkingSetCheck.IsChecked = _settings.CleanWorkingSets;
        SystemCacheCheck.IsChecked = _settings.CleanSystemCache;
        StandbyCheck.IsChecked = _settings.CleanLowPriorityStandby;
        ThresholdSwitch.IsOn = _settings.AutoThreshold;
        IntervalSwitch.IsOn = _settings.AutoInterval;
        ThresholdNumber.Value = _settings.Threshold;
        IntervalNumber.Value = _settings.IntervalMinutes;
        ResultText.Text = _settings.LastCleanUtc is { } last
            ? UiText.Get("LastClean", last.ToLocalTime()) : UiText.Get("NeverCleaned");
        ApplyTheme();
        SelectPage(false);
    }

    private bool SaveSettings()
    {
        if (_checking) return true;
        try { SettingsStore.Save(_settings); return true; }
        catch (Exception error) { ReportIssue(UiText.Get("SaveFailed"), error.Message); return false; }
    }

    private void ApplyTheme()
    {
        Shell.RequestedTheme = _settings.Theme switch
        { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        AppWindow.TitleBar.PreferredTheme = _settings.Theme switch
        { "Light" => TitleBarTheme.Light, "Dark" => TitleBarTheme.Dark, _ => TitleBarTheme.UseDefaultAppMode };
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateCornerRadii();

    private void UpdateCornerRadii()
    {
        var scale = Shell.XamlRoot.RasterizationScale;
        if (_cornerScale == scale) return;
        // The requested radii are screen pixels, while WinUI measures CornerRadius in DIPs.
        var cardRadius = new CornerRadius(12 / scale);
        var controlRadius = new CornerRadius(10 / scale);
        foreach (var card in new[] { MetricsCard, CleanCard, AppearanceCard, CleanupSettingsCard, AutoSettingsCard, AboutCard })
            card.CornerRadius = cardRadius;
        foreach (var control in new Control[] { OverviewTab, SettingsTab, SidebarOverviewTab, SidebarSettingsTab, PaneToggleButton,
            CleanButton, ElevateButton, ThemeChoice, LanguageChoice,
            ThresholdNumber, IntervalNumber, SourceLink, ExitButton })
            control.CornerRadius = controlRadius;
        _cornerScale = scale;
    }

    private void RefreshOptions()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = _settings.AlwaysOnTop;
        ThresholdNumber.IsEnabled = _settings.AutoThreshold;
        IntervalNumber.IsEnabled = _settings.AutoInterval;
        CleanButton.IsEnabled = !_cleaning && _settings.Areas != CleanAreas.None;
        if (!_cleaning) CleanButton.Content = UiText.Get(MemoryEngine.IsAdministrator ? "CleanMemory" : "AuthorizeClean");
        CleanDescription.Text = UiText.Get(MemoryEngine.IsAdministrator ? "CleanDescription" : "AuthorizeDescription");
        AdminBanner.IsOpen = (_settings.AutoThreshold || _settings.AutoInterval) && !MemoryEngine.IsAdministrator;
        AdminBanner.Visibility = AdminBanner.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        var modes = new List<string>();
        if (_settings.AutoThreshold) modes.Add(UiText.Get("ThresholdMode", _settings.Threshold));
        if (_settings.AutoInterval) modes.Add(UiText.Get("IntervalMode", _settings.IntervalMinutes));
        AutoStatusText.Text = modes.Count == 0 ? UiText.Get("AutoOff") :
            UiText.Get("AutoModes", string.Join(" / ", modes)) + (AdminBanner.IsOpen ? UiText.Get("WaitingPermission") : "");
        if (_settings.Areas == CleanAreas.None) AutoStatusText.Text = UiText.Get("SelectArea");
    }

    internal void ReflowMetrics(double width, double textScale)
    {
        var sidebar = _settings.SidebarExpanded && textScale <= 1.25;
        NavigationColumn.Width = new GridLength(sidebar ? (double)Application.Current.Resources["SidebarWidth"] : 0);
        NavigationPane.Visibility = sidebar ? Visibility.Visible : Visibility.Collapsed;
        NavigationPanel.Visibility = sidebar ? Visibility.Collapsed : Visibility.Visible;
        HeaderBreadcrumb.Visibility = sidebar ? Visibility.Visible : Visibility.Collapsed;
        PaneToggleButton.IsEnabled = textScale <= 1.25;
        ThresholdNumber.Width = IntervalNumber.Width = 80 * textScale;
        LanguageChoice.MinWidth = ThemeChoice.MinWidth = 144 * textScale;
        var pageInset = (Thickness)Application.Current.Resources["PageInset"];
        var inset = (double)Application.Current.Resources["Space16"];
        var stacked = width - pageInset.Left - pageInset.Right - NavigationColumn.Width.Value < 3 * 140 * textScale;
        if (stacked == _statsStacked) return;
        _statsStacked = stacked;
        MetricsGrid.ColumnDefinitions.Clear(); MetricsGrid.RowDefinitions.Clear();
        for (var i = 0; i < (stacked ? 1 : 3); i++) MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < (stacked ? 3 : 1); i++) MetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cells = new FrameworkElement[] { PhysicalCell, CommitCell, CacheCell };
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], stacked ? 0 : i); Grid.SetRow(cells[i], stacked ? i : 0); }
        CommitCell.BorderThickness = stacked ? new Thickness(0, 1, 0, 1) : new Thickness(1, 0, 1, 0);
        CommitCell.Margin = stacked ? new Thickness(inset, 0, inset, 0) : new Thickness(0, inset, 0, inset);
        CommitStack.Padding = stacked ? new Thickness(0, inset, 0, inset) : new Thickness(inset, 0, inset, 0);
    }

    private void Sample()
    {
        try
        {
            var snapshot = MemoryEngine.Read();
            if (AppWindow.IsVisible) UpdateStats(snapshot);
            _tray?.Update(snapshot.PhysicalPercent);
            if (_sampleFailed) { _sampleFailed = false; if (IssueBanner.Title == UiText.Get("SampleFailed")) IssueBanner.IsOpen = false; }
            if (!_cleaning && MemoryEngine.IsAdministrator && _settings.IsAutoDue(snapshot.PhysicalPercent, DateTimeOffset.UtcNow, _lastAttempt))
                _ = CleanAsync(false);
        }
        catch (Exception error)
        {
            if (!_sampleFailed) ReportIssue(UiText.Get("SampleFailed"), error.Message);
            _sampleFailed = true;
        }
    }

    internal void UpdateStats(MemorySnapshot snapshot)
    {
        SetText(PhysicalValue, $"{snapshot.PhysicalPercent:0.0}%");
        SetText(PhysicalFree, UiText.Get("AvailableMemory", MemorySnapshot.Bytes(snapshot.PhysicalAvailable)));
        SetText(PhysicalTotal, UiText.Get("TotalMemory", MemorySnapshot.Bytes(snapshot.PhysicalTotal)));
        SetText(CommitValue, $"{snapshot.CommitPercent:0.0}%");
        SetText(CommitUsed, UiText.Get("CommittedMemory", MemorySnapshot.Bytes(snapshot.CommitUsed)));
        SetText(CommitTotal, UiText.Get("CommitLimit", MemorySnapshot.Bytes(snapshot.CommitLimit)));
        SetText(CacheValue, snapshot.CacheCurrent is { } current ? MemorySnapshot.Bytes(current) : "—");
        SetText(CachePeak, snapshot.CachePeak is { } peak ? UiText.Get("PeakMemory", MemorySnapshot.Bytes(peak)) : UiText.Get("PeakUnavailable"));
        SetProgress(PhysicalBar, snapshot.PhysicalPercent);
        SetProgress(CommitBar, snapshot.CommitPercent);
        SetProgress(CacheBar, snapshot.CacheCurrent is { } used && snapshot.CachePeak is { } total ? MemorySnapshot.Percent(used, total) : 0);
    }

    internal static void SetText(TextBlock block, string text) { if (block.Text != text) block.Text = text; }
    private static void SetProgress(ProgressBar bar, double value)
    { value = Math.Round(value, 1); if (bar.Value != value) bar.Value = value; }

    private void ReportIssue(string title, string message)
    { IssueBanner.Title = title; IssueBanner.Message = message; IssueBanner.Visibility = Visibility.Visible; IssueBanner.IsOpen = true; }

    private void OverviewTab_Click(object sender, RoutedEventArgs args) => SelectPage(false);
    private void SettingsTab_Click(object sender, RoutedEventArgs args) => SelectPage(true);
    private void PaneToggle_Click(object sender, RoutedEventArgs args)
    {
        _settings.SidebarExpanded = !_settings.SidebarExpanded;
        ReflowMetrics(Shell.ActualWidth, _uiSettings.TextScaleFactor);
        SaveSettings();
    }
    internal void SelectPage(bool settings)
    {
        foreach (var button in new[] { OverviewTab, SettingsTab, SidebarOverviewTab, SidebarSettingsTab })
        {
            var selected = settings == (button == SettingsTab || button == SidebarSettingsTab);
            button.IsChecked = selected;
            button.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            button.Translation = new System.Numerics.Vector3(0, 0, selected ? 4 : 0);
        }
        PageCaption.Text = UiText.Get(settings ? "SettingsTab.Content" : "OverviewTab.Content");
        OverviewPage.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        SettingsPage.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        BodyScroll.ChangeView(null, 0, null, true);
    }

    private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready) return;
        _settings.Theme = (ThemeChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        ApplyTheme(); SaveSettings();
    }

    private void LanguageChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready) return;
        var previous = _settings.Language;
        _settings.Language = (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        if (SaveSettings()) return;
        _settings.Language = previous;
        _ready = false;
        LanguageChoice.SelectedIndex = previous == "zh-CN" ? 1 : previous == "en-US" ? 2 : 0;
        _ready = true;
    }

    private void Preference_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready) return;
        _settings.AlwaysOnTop = PinSwitch.IsOn;
        _settings.CloseToTray = TraySwitch.IsOn;
        _settings.ConfirmCleanup = ConfirmSwitch.IsOn;
        _settings.AutoThreshold = ThresholdSwitch.IsOn;
        _settings.AutoInterval = IntervalSwitch.IsOn;
        RefreshOptions(); SaveSettings();
    }

    private void CleanupOption_Click(object sender, RoutedEventArgs args)
    {
        _settings.CleanWorkingSets = WorkingSetCheck.IsChecked == true;
        _settings.CleanSystemCache = SystemCacheCheck.IsChecked == true;
        _settings.CleanLowPriorityStandby = StandbyCheck.IsChecked == true;
        RefreshOptions(); SaveSettings();
    }

    private void AutoNumber_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_ready || !double.IsFinite(args.NewValue)) return;
        if (sender == ThresholdNumber) _settings.Threshold = (int)Math.Round(args.NewValue);
        else _settings.IntervalMinutes = (int)Math.Round(args.NewValue);
        _settings.Normalize();
        var committed = sender == ThresholdNumber ? _settings.Threshold : _settings.IntervalMinutes;
        if (sender.Value != committed) { _ready = false; sender.Value = committed; _ready = true; }
        RefreshOptions(); SaveSettings();
    }

    private void AutoNumber_LostFocus(object sender, RoutedEventArgs args)
    {
        if (sender is NumberBox box && !double.IsFinite(box.Value))
            box.Value = box == ThresholdNumber ? _settings.Threshold : _settings.IntervalMinutes;
    }

    private void StartupSwitch_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _checking) return;
        var desired = StartupSwitch.IsOn;
        try { StartupRegistration.SetEnabled(desired); }
        catch (Exception error)
        {
            _ready = false; StartupSwitch.IsOn = !desired; _ready = true;
            ReportIssue(UiText.Get("StartupFailed"), error.Message);
        }
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs args) => await CleanAsync(true);
    private async Task CleanAsync(bool manual)
    {
        if (_cleaning || _settings.Areas == CleanAreas.None) return;
        _cleaning = true; CleanButton.IsEnabled = false;
        try
        {
            if (manual && _settings.ConfirmCleanup)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.ActualTheme, Title = UiText.Get("CleanMemory"),
                    CornerRadius = new CornerRadius(12 / Shell.XamlRoot.RasterizationScale),
                    Content = UiText.Get("CleanConfirm"),
                    PrimaryButtonText = UiText.Get("CleanAction"), CloseButtonText = UiText.Get("CancelAction"), DefaultButton = ContentDialogButton.Close
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            if (!MemoryEngine.IsAdministrator) { RelaunchElevated("--clean"); return; }
            _lastAttempt = DateTimeOffset.UtcNow;
            CleanButton.Content = UiText.Get("Cleaning");
            var areas = _settings.Areas;
            var result = await Task.Run(() => MemoryEngine.Clean(areas));
            _settings.LastCleanUtc = DateTimeOffset.UtcNow;
            SaveSettings();
            ResultText.Text = UiText.Get("FreedStatus", DateTime.Now, MemorySnapshot.Bytes(result.FreedBytes));
            if (result.Errors.Count != 0) ReportIssue(UiText.Get("PartialCleanup"), string.Join("\n", result.Errors));
            Sample();
        }
        catch (Exception error) { _lastAttempt = DateTimeOffset.UtcNow; ReportIssue(UiText.Get("CleanupFailed"), error.Message); }
        finally { _cleaning = false; RefreshOptions(); }
    }

    private void Elevate_Click(object sender, RoutedEventArgs args) => RelaunchElevated("");
    private void RelaunchElevated(string intent)
    {
        try
        {
            if (!SaveSettings()) return;
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add("--elevated");
            if (intent.Length != 0) start.ArgumentList.Add(intent);
            using var process = Process.Start(start);
            if (process is not null) ExitWindow();
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { ResultText.Text = UiText.Get("ElevationCanceled"); }
        catch (Exception error) { ReportIssue(UiText.Get("ElevationFailed"), error.Message); }
    }

    private void ShowWindow()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized) presenter.Restore();
        Activate(); Sample();
    }
    private void Exit_Click(object sender, RoutedEventArgs args) => ExitWindow();
    internal void ExitWindow() { _exitRequested = true; Close(); }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
}
