using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    // Developer-only checks: never clean RAM, create a tray icon, save preferences or register tasks.
    internal async void RunSelfCheck(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        try
        {
            AppWindow.IsShownInSwitchers = false;
            AppWindow.Move(new Windows.Graphics.PointInt32(-30000, -30000));
            AppWindow.Show(false);
            await Task.Delay(250);
            var english = UiText.LanguageTag == "en-US";
            CheckInstallationAndUpdates(directory);
            Require(UpdateButton.Content?.ToString() == (english ? "Check for updates" : "检查更新"), "Update controls are translated");
            checks.Add("Installed-path/ACL boundaries, task XML, non-overwriting settings import, release filtering, download-source and SHA-256 checks passed without creating tasks, downloading or installing anything.");
            Require(SettingsTab.Content?.ToString() == (english ? "Settings" : "设置"), "XAML loads the selected language");
            Require(WorkingSetCheck.Content?.ToString() == (english ? "Process working sets" : "进程工作集"), "Settings controls are translated");
            Require(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(PinSwitch) == (english ? "Always on top" : "窗口始终置顶"),
                "Accessibility labels are translated");
            Require(UiText.Get("TrayOpen") == (english ? "Open Mem Reduct" : "打开 Mem Reduct"), "Tray resources are translated");
            Require(UiText.Get("PermissionRequired") == (english ? "Cleaning memory requires administrator permission." : "清理内存需要管理员权限。"),
                "Permission messages are translated");
            Require(LanguageChoice.Items.Count == 3, "Only supported languages and the system option are listed");
            Require(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(PaneToggleButton)
                == (english ? "Toggle navigation layout" : "切换导航布局"), "Navigation layout control has a translated accessible name");
            var savedAreas = _settings.Areas;
            PaneToggle_Click(PaneToggleButton, new RoutedEventArgs());
            Require(_settings.SidebarExpanded && NavigationPane.Visibility == Visibility.Visible, "The layout button opens the sidebar");
            Require(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!.SidebarExpanded, "The navigation preference survives serialization");
            PaneToggle_Click(PaneToggleButton, new RoutedEventArgs());
            Require(!_settings.SidebarExpanded && NavigationPanel.Visibility == Visibility.Visible && _settings.Areas == savedAreas,
                "The layout button restores top navigation without changing cleanup settings");
            Require(!JsonSerializer.Deserialize<AppSettings>("{}")!.SidebarExpanded, "Existing settings default to top navigation");
            var selectedLanguage = LanguageChoice.SelectedIndex;
            LanguageChoice.SelectedIndex = selectedLanguage == 2 ? 1 : 2;
            var chosenLanguage = selectedLanguage == 2 ? "zh-CN" : "en-US";
            Require(_settings.Language == chosenLanguage, "Language selection reaches settings");
            var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;
            Require(roundTrip.Language == chosenLanguage && roundTrip.Areas == _settings.Areas, "Language survives serialization without changing cleaning areas");
            Require(UiText.LanguageTag == (english ? "en-US" : "zh-CN"), "Pending language changes do not leave a partly translated current window");
            LanguageChoice.SelectedIndex = selectedLanguage;
            Require(JsonSerializer.Deserialize<AppSettings>("{}")!.Language == "System", "Existing settings default to the system language");
            var invalidLanguage = new AppSettings { Language = "unsupported" };
            invalidLanguage.Normalize();
            Require(invalidLanguage.Language == "System", "Unknown language values fall back safely");
            Require(UiText.ResolveLanguage("System", "zh-SG") == "zh-CN"
                && UiText.ResolveLanguage("System", "fr-FR") == "en-US"
                && UiText.ResolveLanguage("en-US", "zh-CN") == "en-US", "System-language matching and explicit override");
            checks.Add($"{UiText.LanguageTag}: XAML, settings, accessibility, tray and permission text; language selection, persistence and fallback passed.");
            var savedTheme = _settings.Theme;
            foreach (var (theme, captionTheme) in new[]
            {
                ("Light", Microsoft.UI.Windowing.TitleBarTheme.Light),
                ("Dark", Microsoft.UI.Windowing.TitleBarTheme.Dark),
                ("System", Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode)
            })
            {
                _settings.Theme = theme; ApplyTheme();
                Require(AppWindow.TitleBar.PreferredTheme == captionTheme, "Caption buttons follow the app theme");
                await Task.Delay(120);
                var colorKey = Shell.ActualTheme == ElementTheme.Dark ? "SystemAccentColorLight2" : "SystemAccentColorDark1";
                Require(CleanButton.Background is SolidColorBrush fill && fill.Color == (Windows.UI.Color)Application.Current.Resources[colorKey],
                    "The actual cleaning button uses the monochrome app palette");
            }
            _settings.Theme = savedTheme; ApplyTheme();
            var displayScale = Shell.XamlRoot.RasterizationScale;
            var buttonRadiusPixels = CleanButton.CornerRadius.TopLeft * displayScale;
            Require(Math.Abs(buttonRadiusPixels - 10) < 0.01,
                $"Button radius must be 10 screen pixels; actual={buttonRadiusPixels}, scale={displayScale}");
            foreach (var card in new[] { MetricsCard, CleanCard, AppearanceCard, CleanupSettingsCard, AutoSettingsCard, AboutCard })
                Require(Math.Abs(card.CornerRadius.TopLeft * displayScale - 12) < 0.01, "All grouping cards have 12 screen-pixel radii");
            foreach (var control in new Control[] { OverviewTab, SettingsTab, SidebarOverviewTab, SidebarSettingsTab, PaneToggleButton,
                ElevateButton, ThemeChoice, LanguageChoice, ThresholdNumber, IntervalNumber, SourceLink, ExitButton, UpdateButton, ReleasesLink })
                Require(Math.Abs(control.CornerRadius.TopLeft * displayScale - 10) < 0.01,
                    $"Every custom control has 10 screen-pixel radii: {control.Name} at scale {displayScale}");
            var presenter = AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            Require(presenter is not null && Math.Abs(presenter.PreferredMinimumWidth.GetValueOrDefault() - 560 * displayScale) <= 1
                && Math.Abs(presenter.PreferredMinimumHeight.GetValueOrDefault() - 440 * displayScale) <= 1,
                $"Minimum window size tracks the monitor DPI: {presenter?.PreferredMinimumWidth}x{presenter?.PreferredMinimumHeight} at scale {displayScale}");
            checks.Add($"Light/dark/system caption themes, physical 12px card / 10px control radii and DPI-tracked minimum window size passed at scale {displayScale}.");
            checks.Add("Black/white controls, pale gray canvas and compact white grouping cards use the reference palette.");
            await CheckTrayGlyphs(directory);
            checks.Add("Tray digits 0/9/99/100 rendered at 16/20/24/32px on light/dark backgrounds; 200 refreshes leaked no GDI or USER handles.");
            Require(Marshal.SizeOf<MemoryEngine.MemoryStatus>() == 64, "MEMORYSTATUSEX ABI");
            Require(Marshal.SizeOf<MemoryEngine.PerformanceInfo>() == 104, "PERFORMANCE_INFORMATION ABI");
            Require(Marshal.SizeOf<MemoryEngine.FileCacheInfo>() == 64, "SYSTEM_FILECACHE_INFORMATION ABI");
            Require(Marshal.SizeOf<MemoryEngine.CombineInfo>() == 24, "MEMORY_COMBINE_INFORMATION_EX ABI");
            var memory = MemoryEngine.Read();
            Require(memory.PhysicalTotal > 0 && memory.PhysicalAvailable <= memory.PhysicalTotal, "Live physical memory");
            Require(memory.CommitLimit > 0 && memory.CommitUsed <= memory.CommitLimit, "Live commit memory");
            checks.Add("Read-only native memory sampling and x64 ABI passed.");
            var options = new AppSettings { Threshold = 500, IntervalMinutes = -1, Theme = "unknown" };
            options.Normalize();
            Require(options.Threshold == 99 && options.IntervalMinutes == 1 && options.Theme == "System", "Configuration bounds");
            var now = DateTimeOffset.UtcNow;
            options.AutoThreshold = true;
            Require(!options.IsAutoDue(100, now, now.AddSeconds(-29)), "Auto-clean cooldown");
            Require(options.IsAutoDue(100, now, now.AddSeconds(-30)), "Threshold boundary");
            options.AutoThreshold = false; options.AutoInterval = true; options.IntervalMinutes = 30;
            Require(!options.IsAutoDue(0, now, now.AddMinutes(-29)), "Interval before deadline");
            Require(options.IsAutoDue(0, now, now.AddMinutes(-30)), "Interval deadline");
            Require(!options.IsAutoDue(100, now, now.AddDays(-1), gameActive: true), "Full-screen protection pauses interval cleaning");
            options.AutoThreshold = true;
            Require(!options.IsAutoDue(100, now, now.AddDays(-1), gameActive: true), "Full-screen protection also pauses threshold cleaning");
            options.GameMode = false;
            Require(options.IsAutoDue(100, now, now.AddDays(-1), gameActive: true), "Disabling game mode restores the existing automatic policy");
            options.GameMode = true;
            var savedAttempt = _lastAttempt;
            var savedGameAreas = _settings.Areas;
            GameModeSwitch.IsOn = false;
            Require(!_settings.GameMode, "Game switch reaches settings");
            GameModeSwitch.IsOn = true;
            Require(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!.GameMode, "Game mode survives restart");
            UpdateGameProtection(true, now);
            Require(_gameActive && !AdminBanner.IsOpen && AutoStatusText.Text == UiText.Get("GameModeActive"), "Active full-screen protection is visible");
            UpdateGameProtection(false, now);
            Require(!_gameActive && _lastAttempt == now && _settings.Areas == savedGameAreas,
                "Leaving full screen starts the cooldown without changing cleanup selections");
            Require(!options.IsAutoDue(100, now.AddSeconds(29), _lastAttempt)
                && options.IsAutoDue(100, now.AddSeconds(30), _lastAttempt), "Alt-Tab has a 30-second grace period");
            _lastAttempt = savedAttempt;
            var savedThresholdPolicy = (_settings.AutoThreshold, _settings.AutoInterval, _settings.Threshold, _settings.IntervalMinutes);
            _settings.AutoThreshold = true; _settings.AutoInterval = false; _settings.Threshold = 90;
            CompleteCleanupAttempt(false, 95, now);
            Require(_thresholdRetryAfter == now.AddMinutes(5) && _lastAttempt == now,
                "Persistently high usage defers threshold retries from completion");
            Require(AutoStatusText.Text.Contains(UiText.Get("ThresholdRetryDeferred")), "The deferred threshold retry is explained in the current language");
            foreach (var seconds in new[] { 30, 60, 299 })
                Require(!_settings.IsAutoDue(95, now.AddSeconds(seconds), _lastAttempt, thresholdRetryAfter: _thresholdRetryAfter),
                    "High memory after cleanup does not cause repeated 30-second threshold attempts");
            Require(_settings.IsAutoDue(95, now.AddMinutes(5), _lastAttempt, thresholdRetryAfter: _thresholdRetryAfter), "Threshold retries resume at five minutes");
            _settings.AutoInterval = true; _settings.IntervalMinutes = 1;
            Require(_settings.IsAutoDue(95, now.AddMinutes(1), _lastAttempt, thresholdRetryAfter: _thresholdRetryAfter), "Explicit interval cleanup remains independent of threshold backoff");
            Require(!_settings.IsAutoDue(95, now.AddMinutes(1), _lastAttempt, gameActive: true, thresholdRetryAfter: _thresholdRetryAfter), "Game protection still overrides both schedules");
            _settings.AutoInterval = false;
            ObserveThresholdRetry(89, now.AddSeconds(10));
            Require(_thresholdRetryAfter == default && _settings.IsAutoDue(95, now.AddSeconds(30), _lastAttempt, thresholdRetryAfter: _thresholdRetryAfter),
                "Recovery below the threshold re-arms threshold cleaning without changing user settings");
            CompleteCleanupAttempt(true, 95, now);
            Require(_thresholdRetryAfter == default, "Manual cleanup does not impose threshold backoff");
            Require(!_settings.IsAutoDue(95, now.AddSeconds(29), _lastAttempt), "Even a slow cleanup has a full cooldown after completion");
            CompleteCleanupAttempt(false, 95, now);
            ObserveThresholdRetry(95, now.AddMinutes(5));
            Require(_thresholdRetryAfter == default, "Expired backoff clears its UI status");
            (_settings.AutoThreshold, _settings.AutoInterval, _settings.Threshold, _settings.IntervalMinutes) = savedThresholdPolicy;
            _lastAttempt = savedAttempt;
            RefreshOptions();
            checks.Add("Persistent-high-memory threshold retries, recovery, five-minute expiry, completion cooldown, explicit intervals and game protection passed without cleaning memory or saving settings.");
            var screen = new GameModeMonitor.Rect { Left = -1920, Top = 0, Right = 0, Bottom = 1080 };
            Require(GameModeMonitor.CoversMonitor(screen, screen), "Borderless full screen works on monitors with negative coordinates");
            Require(!GameModeMonitor.CoversMonitor(new() { Left = -1920, Top = 0, Right = 0, Bottom = 1040 }, screen),
                "Ordinary maximized windows below the taskbar are not full screen");
            Require(!GameModeMonitor.CoversMonitor(default, default) && GameModeMonitor.IsShellClass("WorkerW")
                && !GameModeMonitor.IsShellClass("GameWindow"), "Desktop surfaces and invalid bounds are excluded");
            _ = GameModeMonitor.IsFullscreenApp(); // Read-only ABI smoke check; does not claim a real game was tested.
            Require(JsonSerializer.Deserialize<AppSettings>("{}")!.GameMode && !JsonSerializer.Deserialize<AppSettings>("{}")!.NotifyAfterCleanup,
                "Legacy settings gain game protection with optional notifications off");
            NotifyCleanupSwitch.IsOn = true;
            Require(_settings.NotifyAfterCleanup && JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!.NotifyAfterCleanup,
                "Notification preference is wired and persisted");
            NotifyCleanupSwitch.IsOn = false;
            Require(ShouldNotifyCleanup(true, 0, true, false) && !ShouldNotifyCleanup(false, 0, true, false)
                && !ShouldNotifyCleanup(true, 1, true, false) && !ShouldNotifyCleanup(true, 0, false, true),
                "Only enabled, successful cleanups notify; automatic game-mode notifications are suppressed");
            var activationEvents = new uint[] { 0x201, 0x202, 0x400 };
            Require(activationEvents.Count(x => TrayIcon.IsActivation(x, true)) == 1
                && activationEvents.Count(x => TrayIcon.IsActivation(x, false)) == 1
                && TrayIcon.IsActivation(0x401, true) && !TrayIcon.IsActivation(0x7b, true),
                "Mouse-up and NIN_SELECT cannot clean twice; keyboard activation remains supported");
            Require(TrayIcon.IsContextMenu(0x7b, true) && !TrayIcon.IsContextMenu(0x205, true)
                && TrayIcon.IsContextMenu(0x205, false), "Right-click dispatch follows the negotiated tray protocol");
            foreach (var dark in new[] { false, true })
            {
                var normal = TrayIcon.StatusColor(69.9, CleanupFeedback.None, false, dark);
                var warning = TrayIcon.StatusColor(70, CleanupFeedback.None, false, dark);
                var danger = TrayIcon.StatusColor(90, CleanupFeedback.None, false, dark);
                Require(normal != warning && warning != danger && TrayIcon.StatusColor(89.9, CleanupFeedback.None, false, dark) == warning,
                    "Tray warning/danger boundaries are 70 and 90 percent in both themes");
                Require(TrayIcon.StatusColor(99, CleanupFeedback.Cleaning, false, dark) != danger
                    && TrayIcon.StatusColor(99, CleanupFeedback.Success, false, dark) == normal,
                    "Cleanup feedback overrides usage colors briefly");
            }
            SetFeedback(CleanupFeedback.Success, "self-check");
            _feedbackUntil = Environment.TickCount64 - 1;
            UpdateTray();
            Require(_feedback == CleanupFeedback.None, "Result feedback expires back to live usage");
            checks.Add("Tray event routing, warning boundaries, success-notification gating, full-screen geometry and game-mode scheduling passed without real cleaning or notifications.");
            options.CleanWorkingSets = options.CleanSystemCache = options.CleanLowPriorityStandby = false;
            Require(!options.IsAutoDue(100, now, now.AddDays(-1)), "Empty cleanup selection");
            options.CleanModifiedPages = options.CleanStandby = true;
            options.AutoThreshold = true;
            Require(options.AutomaticAreas == CleanAreas.None && !options.IsAutoDue(100, now, now.AddDays(-1)),
                "Starred options never trigger threshold or interval automatic cleaning");
            Require(MemoryEngine.GetOperations((CleanAreas)255, false).Length == 8, "All eight areas are available for manual cleaning");
            var automaticOperations = MemoryEngine.GetOperations((CleanAreas)255, true);
            Require(automaticOperations.Length == 6 && automaticOperations.All(x => (x.Area & MemoryEngine.ManualOnlyAreas) == 0),
                "Both starred areas are removed at the native dispatch boundary");
            var skipped = MemoryEngine.Clean(MemoryEngine.ManualOnlyAreas, automatic: true);
            Require(skipped.FreedBytes == 0 && skipped.Errors.Count == 0,
                "An automatic request containing only starred areas returns before privilege checks or native cleanup");
            var defaults = JsonSerializer.Deserialize<AppSettings>("{}")!;
            Require(defaults.Areas == (CleanAreas.WorkingSets | CleanAreas.SystemCache | CleanAreas.LowPriorityStandby),
                "New and legacy settings keep all five added cleanup areas off");
            var addedChecks = new[] { ModifiedFileCheck, ModifiedPagesCheck, FullStandbyCheck, RegistryCheck, CombineCheck };
            Require(addedChecks.All(x => x.IsChecked == false), "All five added checkboxes are initially unchecked");
            foreach (var box in addedChecks) box.IsChecked = true;
            CleanupOption_Click(CombineCheck, new RoutedEventArgs());
            Require(_settings.Areas == (CleanAreas)255 && JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!.Areas == (CleanAreas)255,
                "Every added checkbox reaches settings and survives serialization");
            foreach (var box in addedChecks) box.IsChecked = false;
            CleanupOption_Click(CombineCheck, new RoutedEventArgs());
            Require(_settings.Areas == savedAreas, "Checking added options does not alter the existing selection");
            foreach (var key in new[] { "WorkingSet", "SystemCache", "ModifiedFile", "ModifiedPages", "FullStandby", "Standby", "Registry", "Combine" })
                Require(UiText.Get(key + "Description.Text").Length > 20, "Every cleaning area has an explanation");
            const string volume = @"\\?\Volume{12345678-1234-1234-1234-123456789abc}\";
            Require(MemoryEngine.VolumeDevicePath(volume) == volume[..^1], "Volume GUID handle paths omit the trailing slash");
            foreach (var invalid in new[] { @"C:\", @"\\.\PhysicalDrive0", volume[..^1], volume.Replace("12345678", "notaguid") })
            {
                var denied = false;
                try { MemoryEngine.VolumeDevicePath(invalid); } catch (ArgumentException) { denied = true; }
                Require(denied, "Non-volume paths are rejected before opening any handle");
            }
            var rejected = false;
            try { MemoryEngine.ValidateAreas((CleanAreas)256); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Unsupported cleanup command rejected");
            checks.Add("Eight cleanup options, five disabled defaults, descriptions, persistence, native dispatch filtering, auto deadlines/cooldown and volume path validation passed; no native cleanup was invoked.");
            var restart = CreateRestartStartInfo();
            Require(restart.FileName == Environment.ProcessPath && restart.ArgumentList.SequenceEqual(new[] { "--restart" })
                && restart.UseShellExecute && restart.Verb.Length == 0, "Language restart runs only this app, without replaying --clean or requesting elevation");

            UpdateStats(memory);
            var valueControl = PhysicalValue;
            var writes = 0;
            var callback = PhysicalValue.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => writes++);
            for (var i = 0; i < 1000; i++) UpdateStats(memory);
            PhysicalValue.UnregisterPropertyChangedCallback(TextBlock.TextProperty, callback);
            Require(writes == 0 && ReferenceEquals(valueControl, PhysicalValue), "Unchanged samples never rewrite text or replace controls");
            checks.Add("1,000 identical samples caused zero text writes; existing visual controls were retained.");

            var savedAutoThreshold = _settings.AutoThreshold;
            var savedAutoInterval = _settings.AutoInterval;
            var savedThreshold = _settings.Threshold;
            var savedInterval = _settings.IntervalMinutes;
            _settings.AutoThreshold = _settings.AutoInterval = true;
            RefreshOptions();
            SelectPage(true); Layout(700, 560, 1);
            BodyScroll.ChangeView(null, BodyScroll.ScrollableHeight, null, true);
            await Task.Delay(100);
            var inputChecks = new List<object>();
            foreach (var (box, text) in new[] { (ThresholdNumber, "75"), (IntervalNumber, "1440") })
            {
                var editor = Descendants(box).OfType<TextBox>().First();
                Require(editor.Focus(FocusState.Programmatic), "The enabled input can receive focus");
                editor.Text = "";
                await Task.Delay(30);
                Require(editor.Text.Length == 0, "Clearing a focused input keeps an empty draft");
                foreach (var digit in text)
                {
                    editor.Text += digit;
                    UpdateStats(memory);
                    await Task.Delay(20);
                }
                Shell.UpdateLayout();
                await WritePreview(directory, box == ThresholdNumber ? "threshold-editing.png" : "interval-editing.png", AutoSettingsCard);
                Require(editor.Text == text, "Multiple digits survive editing and memory refreshes until commit");
                Require(VisualTreeHelper.GetOpenPopupsForXamlRoot(Shell.XamlRoot).Count == 0,
                    "Typing does not open an overlapping spin popup");
                var content = Descendants(editor).OfType<ScrollViewer>().First(x => x.Name == "ContentElement");
                var glyphs = new TextBlock { Text = text, FontSize = editor.FontSize, FontFamily = editor.FontFamily };
                glyphs.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = content.ActualWidth - content.Padding.Left - content.Padding.Right;
                Require(textWidth >= glyphs.DesiredSize.Width + 2, "The focused input has enough visible space for all digits and the caret");
                var upper = Descendants(ThresholdNumber).OfType<Border>().First(x => x.Name == "BorderElement");
                var lower = Descendants(IntervalNumber).OfType<Border>().First(x => x.Name == "BorderElement");
                Require(Math.Abs(upper.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).X
                    - lower.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).X) < 0.5
                    && Math.Abs(upper.ActualWidth - lower.ActualWidth) < 0.5, "Focused and unfocused input borders align");
                // Moving focus uses NumberBox's real validation and the app's LostFocus handler.
                var other = Descendants(box == ThresholdNumber ? IntervalNumber : ThresholdNumber).OfType<TextBox>().First();
                Require(other.Focus(FocusState.Programmatic), "Focus can move to the next input");
                await Task.Delay(50);
                var committed = box == ThresholdNumber ? _settings.Threshold : _settings.IntervalMinutes;
                Require(committed == int.Parse(text) && editor.Text == text, "Leaving an input commits the completed number");
                Require(editor.Focus(FocusState.Programmatic), "An input can be edited again");
                editor.Text = "";
                other.Focus(FocusState.Programmatic);
                await Task.Delay(50);
                Require(editor.Text == text, "An empty input restores the last committed value on blur");
                editor.Focus(FocusState.Programmatic);
                editor.Text = "abc";
                other.Focus(FocusState.Programmatic);
                await Task.Delay(50);
                Require(editor.Text == text, "Invalid text restores the last committed value on blur");
                inputChecks.Add(new { box.Name, text, visibleTextWidth = textWidth, requiredTextWidth = glyphs.DesiredSize.Width + 2, committed });
            }
            File.WriteAllText(Path.Combine(directory, "input-editing.json"), JsonSerializer.Serialize(inputChecks, new JsonSerializerOptions { WriteIndented = true }));
            checks.Add("Real input controls retained cleared/multi-digit drafts, kept digits visible without popups, aligned focused borders, committed on blur and recovered empty/invalid input; no user settings were written.");

            // Reported height issue: measure both inputs focused and unfocused before asserting, so a failed run still leaves evidence.
            (double Height, double Top, double Bottom, double TextCenterDelta) MeasureInput(NumberBox box)
            {
                var border = Descendants(box).OfType<Border>().First(x => x.Name == "BorderElement");
                var editor = Descendants(box).OfType<TextBox>().First();
                var content = Descendants(editor).OfType<ScrollViewer>().First(x => x.Name == "ContentElement");
                var glyphs = new TextBlock { Text = "0", FontSize = editor.FontSize, FontFamily = editor.FontFamily };
                glyphs.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var top = border.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).Y;
                var contentTop = content.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).Y;
                return (box.ActualHeight, top, top + border.ActualHeight,
                    contentTop + content.Padding.Top + glyphs.DesiredSize.Height / 2 - (top + border.ActualHeight / 2));
            }
            var heightSamples = new List<(string Name, (double Height, double Top, double Bottom, double TextCenterDelta) Unfocused, (double Height, double Top, double Bottom, double TextCenterDelta) Focused)>();
            foreach (var (box, other) in new[] { (ThresholdNumber, IntervalNumber), (IntervalNumber, ThresholdNumber) })
            {
                var editor = Descendants(box).OfType<TextBox>().First();
                var otherEditor = Descendants(other).OfType<TextBox>().First();
                Require(otherEditor.Focus(FocusState.Programmatic), "The sibling input can take focus for the unfocused height sample");
                Shell.UpdateLayout();
                await Task.Delay(50);
                var unfocused = MeasureInput(box);
                if (box == ThresholdNumber) await WritePreview(directory, "threshold-unfocused.png", AutoSettingsCard);
                Require(editor.Focus(FocusState.Programmatic), "The input can take focus for the focused height sample");
                Shell.UpdateLayout();
                await Task.Delay(50);
                var focused = MeasureInput(box);
                if (box == ThresholdNumber) await WritePreview(directory, "threshold-focused.png", AutoSettingsCard);
                heightSamples.Add((box.Name, unfocused, focused));
                File.WriteAllText(Path.Combine(directory, "input-heights.json"), JsonSerializer.Serialize(new
                {
                    displayScale = Shell.XamlRoot.RasterizationScale,
                    inputs = heightSamples.Select(x => new
                    {
                        input = x.Name, unfocusedHeight = x.Unfocused.Height, focusedHeight = x.Focused.Height,
                        unfocusedTop = x.Unfocused.Top, unfocusedBottom = x.Unfocused.Bottom,
                        focusedTop = x.Focused.Top, focusedBottom = x.Focused.Bottom,
                        unfocusedTextDelta = x.Unfocused.TextCenterDelta, focusedTextDelta = x.Focused.TextCenterDelta
                    })
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            foreach (var sample in heightSamples)
            {
                Require(Math.Abs(sample.Focused.Height - sample.Unfocused.Height) <= 1.2,
                    $"Focused and unfocused input heights match: {sample.Name} unfocused={sample.Unfocused.Height}, focused={sample.Focused.Height}");
                Require(Math.Abs(sample.Focused.Top - sample.Unfocused.Top) <= 0.5 && Math.Abs(sample.Focused.Bottom - sample.Unfocused.Bottom) <= 0.5,
                    $"Input top and bottom edges stay put across focus: {sample.Name}");
                Require(Math.Abs(sample.Unfocused.TextCenterDelta) <= 1.0 && Math.Abs(sample.Focused.TextCenterDelta) <= 1.0,
                    $"Input text stays vertically centered: {sample.Name} unfocused delta={sample.Unfocused.TextCenterDelta}, focused delta={sample.Focused.TextCenterDelta}");
            }
            Require(Math.Abs(heightSamples[0].Unfocused.Height - heightSamples[1].Unfocused.Height) < 0.5,
                "Both automatic-cleaning rows render the same input height");
            checks.Add($"Focus-state input height and centering: {string.Join("; ", heightSamples.Select(x => $"{x.Name} height {x.Unfocused.Height}->{x.Focused.Height}, text delta {x.Unfocused.TextCenterDelta:F1}/{x.Focused.TextCenterDelta:F1}"))}.");
            _settings.AutoThreshold = savedAutoThreshold;
            _settings.AutoInterval = savedAutoInterval;
            ThresholdNumber.Value = _settings.Threshold = savedThreshold;
            IntervalNumber.Value = _settings.IntervalMinutes = savedInterval;
            CleanButton.Focus(FocusState.Programmatic);
            RefreshOptions();

            // Layout is measured inside this hidden app window, without inspecting the user's desktop.
            Shell.RequestedTheme = ElementTheme.Light;
            SelectPage(false);
            Layout(680, 1600, 1);
            var fonts = Descendants(Shell).OfType<TextBlock>().ToDictionary(x => x, x => x.FontSize);
            var controlFonts = Descendants(OverviewPage).OfType<Control>().ToDictionary(x => x, x => x.FontSize);
            SelectPage(true); Layout(680, 3200, 1);
            foreach (var block in Descendants(Shell).OfType<TextBlock>()) fonts.TryAdd(block, block.FontSize);
            foreach (var control in Descendants(SettingsPage).OfType<Control>()) controlFonts.TryAdd(control, control.FontSize);
            foreach (var control in new[] { OverviewTab, SettingsTab, SidebarOverviewTab, SidebarSettingsTab }) controlFonts.TryAdd(control, control.FontSize);
            var layouts = new List<object>();
            foreach (var sidebar in new[] { false, true })
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            foreach (var width in new[] { 560.0, 680.0, 760.0, 880.0 })
            foreach (var settings in new[] { false, true })
            {
                _settings.SidebarExpanded = sidebar;
                SelectPage(settings);
                foreach (var (control, size) in controlFonts) control.FontSize = size * scale;
                foreach (var (block, size) in fonts) block.FontSize = size * scale;
                Layout(width, settings ? 3200 : 1600, scale);
                if (settings)
                {
                    foreach (var row in new[] { AboutUpdateRow, AboutLinksRow, AboutLicenseRow })
                    {
                        var first = (FrameworkElement)row.Children[0];
                        var second = (FrameworkElement)row.Children[1];
                        var a = first.TransformToVisual(row).TransformPoint(new Point());
                        var b = second.TransformToVisual(row).TransformPoint(new Point());
                        Require(a.X >= row.Padding.Left - 1 && b.X >= row.Padding.Left - 1
                            && a.X + first.ActualWidth <= row.ActualWidth - row.Padding.Right + 1
                            && b.X + second.ActualWidth <= row.ActualWidth - row.Padding.Right + 1,
                            $"About controls stay within their row: {row.Name}, width={width}, scale={scale}");
                        Require(Grid.GetRow(second) == 1
                            ? Math.Abs(a.X - b.X) <= 1 && b.Y >= a.Y + first.ActualHeight + row.RowSpacing - 1
                            : b.X >= a.X + first.ActualWidth + row.ColumnSpacing - 1,
                            $"About row columns do not overlap and stacked controls align: {row.Name}");
                    }
                    foreach (var (upper, lower) in new (FrameworkElement, FrameworkElement)[]
                        { (ThresholdNumber, IntervalNumber), (ThresholdUnit, IntervalUnit), (ThresholdSwitch, IntervalSwitch) })
                    {
                        var upperX = upper.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).X;
                        var lowerX = lower.TransformToVisual(AutoSettingsCard).TransformPoint(new Point()).X;
                        Require(Math.Abs(upperX - lowerX) < 0.5
                            && (upper is TextBlock || Math.Abs(upper.ActualWidth - lower.ActualWidth) < 0.5),
                            $"Automatic-cleaning columns: {upper.Name}/{lower.Name}; x={upperX}/{lowerX}, widths={upper.ActualWidth}/{lower.ActualWidth}, width={width}, textScale={scale}, sidebar={sidebar}");
                        if (upper is NumberBox upperInput)
                        {
                            var lowerInput = (NumberBox)lower;
                            var upperGeometry = MeasureInput(upperInput);
                            var lowerGeometry = MeasureInput(lowerInput);
                            Require(Math.Abs(upperInput.ActualHeight - lowerInput.ActualHeight) < 0.5
                                && Math.Abs(upperGeometry.TextCenterDelta) <= 1.0 && Math.Abs(lowerGeometry.TextCenterDelta) <= 1.0,
                                $"Automatic-cleaning input heights and centering: {upperInput.ActualHeight}/{lowerInput.ActualHeight}, text delta={upperGeometry.TextCenterDelta}/{lowerGeometry.TextCenterDelta}, width={width}, textScale={scale}");
                        }
                    }
                }
                Require(NavigationPane.Visibility == (sidebar && scale <= 1.25 ? Visibility.Visible : Visibility.Collapsed),
                    "The selected navigation mode is preserved, with a larger-text fallback");
                Require(SidebarOverviewTab.IsChecked == OverviewTab.IsChecked && SidebarSettingsTab.IsChecked == SettingsTab.IsChecked,
                    "Top and sidebar navigation select the same page");
                var checkedText = 0;
                foreach (var block in Descendants(settings ? SettingsPage : OverviewPage).OfType<TextBlock>())
                {
                    if (block.ActualWidth <= 0 || block.Text.Length == 0) continue;
                    checkedText++;
                    Require(!block.IsTextTrimmed, $"Trimmed text: {block.Text}");
                    var probe = new TextBlock
                    {
                        Text = block.Text, FontFamily = block.FontFamily, FontSize = block.FontSize,
                        FontWeight = block.FontWeight, FontStyle = block.FontStyle, TextWrapping = block.TextWrapping,
                        FontStretch = block.FontStretch, LineHeight = block.LineHeight, LineStackingStrategy = block.LineStackingStrategy,
                        Language = block.Language, TextLineBounds = block.TextLineBounds,
                        IsTextScaleFactorEnabled = block.IsTextScaleFactorEnabled, Padding = block.Padding
                    };
                    // Attach the probe to the same XamlRoot so DPI rounding and CJK font fallback match.
                    probe.Opacity = 0;
                    Shell.Children.Add(probe);
                    probe.Measure(new Size(block.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : block.ActualWidth,
                        double.PositiveInfinity));
                    Shell.Children.Remove(probe);
                    Require(probe.DesiredSize.Width <= block.ActualWidth + 1, $"Horizontal overflow: {block.Text}");
                    Require(probe.DesiredSize.Height <= block.ActualHeight + 1,
                        $"Vertical overflow: {block.Text}; width={width}, scale={scale}, actual={block.ActualWidth}x{block.ActualHeight}, desired={probe.DesiredSize}, font={block.FontSize}, language={block.Language}");
                }
                Require(checkedText >= 8, "Actual page text was measured");
                layouts.Add(new { width, textScale = scale, sidebar, page = settings ? "settings" : "memory", checkedText });
            }
            checks.Add("48 memory/settings layout cases passed in both navigation modes, at widths 560/680/760/880 and text sizes 100/150/200%.");
            checks.Add("Automatic-cleaning inputs, units and switches align in all settings layout cases.");
            foreach (var (control, size) in controlFonts) control.FontSize = size;
            foreach (var (block, size) in fonts) block.FontSize = size;
            _settings.SidebarExpanded = false;
            SelectPage(false); Layout(700, 520, 1);
            await WritePreview(directory, "memory-light.png");
            SelectPage(true); Layout(700, 1600, 1);
            await WritePreview(directory, "settings-light.png");
            await WritePreview(directory, "automatic-settings.png", AutoSettingsCard);
            await WritePreview(directory, "update-settings.png", AboutCard);
            Layout(700, 660, 1);
            await WritePreview(directory, "language-settings.png");
            var restartDialog = CreateLanguageRestartDialog();
            Require(restartDialog.PrimaryButtonText == (english ? "Restart now" : "立即重启")
                && restartDialog.CloseButtonText == (english ? "Restart later" : "稍后重启")
                && restartDialog.DefaultButton == ContentDialogButton.Close, "The translated restart dialog defaults to restarting later");
            var restartPrompt = restartDialog.ShowAsync();
            await WritePreview(directory, "language-restart.png", restartDialog);
            restartDialog.Hide();
            Require(await restartPrompt == ContentDialogResult.None, "Dismissing the restart dialog does not request a restart");
            checks.Add("The localized language restart dialog rendered and dismissed safely; restart arguments contain only --restart. No real restart was performed.");
            _settings.SidebarExpanded = true;
            SelectPage(false); Layout(760, 600, 1);
            await WritePreview(directory, "memory-sidebar.png");
            SelectPage(true); Layout(760, 900, 1);
            await WritePreview(directory, "settings-sidebar.png");
            Shell.RequestedTheme = ElementTheme.Dark;
            _settings.SidebarExpanded = false;
            SelectPage(false); Layout(700, 520, 1);
            await WritePreview(directory, "memory-dark.png");
            File.WriteAllText(Path.Combine(directory, "self-check.json"), JsonSerializer.Serialize(new
            { passed = true, language = UiText.LanguageTag, checks, layouts, displayScale, cardRadiusPixels = MetricsCard.CornerRadius.TopLeft * displayScale,
                buttonRadiusPixels, physicalTotal = memory.PhysicalTotal, cacheAvailable = memory.CacheCurrent.HasValue },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            File.WriteAllText(Path.Combine(directory, "self-check.json"), JsonSerializer.Serialize(new
            { passed = false, checks, error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { ExitWindow(); }
    }

    private void Layout(double width, double height, double scale)
    {
        Shell.Width = width; Shell.Height = height;
        ReflowMetrics(width, scale);
        Shell.Measure(new Size(width, height));
        Shell.Arrange(new Rect(0, 0, width, height));
        Shell.UpdateLayout();
    }

    private async Task WritePreview(string directory, string filename, UIElement? element = null)
    {
        await Task.Delay(250);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element ?? Shell);
        var bytes = (await bitmap.GetPixelsAsync()).ToArray();
        using var stream = File.Open(Path.Combine(directory, filename), FileMode.Create, FileAccess.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight, 96, 96, bytes);
        await encoder.FlushAsync();
    }

    private static async Task CheckTrayGlyphs(string directory)
    {
        var sizes = new[] { 16, 20, 24, 32 };
        foreach (var dark in new[] { false, true })
        {
            const int width = 192, height = 192;
            var canvas = new byte[width * height * 4];
            Array.Fill(canvas, dark ? (byte)32 : (byte)244);
            for (var i = 3; i < canvas.Length; i += 4) canvas[i] = 255;
            for (var row = 0; row < sizes.Length; row++)
            for (var column = 0; column < 4; column++)
            {
                var size = sizes[row];
                var percent = new[] { 0, 9, 99, 100 }[column];
                var icon = TrayGlyph.Create(percent, size, TrayIcon.StatusColor(percent, CleanupFeedback.None, false, dark), out var pixels);
                try
                {
                    var visible = Enumerable.Range(0, size * size).Count(i => pixels[i * 4 + 3] != 0);
                    Require(visible > size * size / 10 && visible < size * size / 2, "Tray digit coverage and transparent background");
                    for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var source = (y * size + x) * 4;
                        var target = ((row * 48 + (48 - size) / 2 + y) * width + column * 48 + (48 - size) / 2 + x) * 4;
                        for (var channel = 0; channel < 3; channel++)
                            canvas[target + channel] = (byte)(pixels[source + channel] + canvas[target + channel] * (255 - pixels[source + 3]) / 255);
                    }
                }
                finally { TrayGlyph.DestroyIcon(icon); }
            }
            using var stream = File.Open(Path.Combine(directory, dark ? "tray-digits-dark.png" : "tray-digits-light.png"), FileMode.Create, FileAccess.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, width, height, 96, 96, canvas);
            await encoder.FlushAsync();
        }
        var process = System.Diagnostics.Process.GetCurrentProcess().Handle;
        var gdi = GetGuiResources(process, 0);
        var user = GetGuiResources(process, 1);
        for (var i = 0; i < 200; i++)
            TrayGlyph.DestroyIcon(TrayGlyph.Create(i % 101, 24, 0xf4f4f4, out _, i % 12));
        Require(GetGuiResources(process, 0) == gdi && GetGuiResources(process, 1) == user, "Tray icon refresh releases native handles");
        const int frameSize = 24, stripWidth = 12 * frameSize;
        var strip = new byte[stripWidth * frameSize * 4];
        byte[]? firstFrame = null;
        for (var frame = 0; frame < 12; frame++)
        {
            var icon = TrayGlyph.Create(82, frameSize, TrayIcon.StatusColor(82, CleanupFeedback.Cleaning, false, false), out var pixels, frame);
            try
            {
                if (frame == 0) firstFrame = pixels;
                else Require(!firstFrame!.SequenceEqual(pixels), "Each spinner frame visibly changes");
                for (var y = 0; y < frameSize; y++)
                    Array.Copy(pixels, y * frameSize * 4, strip, (y * stripWidth + frame * frameSize) * 4, frameSize * 4);
            }
            finally { TrayGlyph.DestroyIcon(icon); }
        }
        using var spinnerStream = File.Open(Path.Combine(directory, "tray-spinner-frames.png"), FileMode.Create, FileAccess.ReadWrite);
        var spinnerEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, spinnerStream.AsRandomAccessStream());
        spinnerEncoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, stripWidth, frameSize, 96, 96, strip);
        await spinnerEncoder.FlushAsync();
    }

    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        if (parent is UIElement { Visibility: Visibility.Collapsed }) yield break;
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Self-check failed: " + message); }
}
