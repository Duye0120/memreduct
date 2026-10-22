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
            Require(SettingsTab.Content?.ToString() == (english ? "Settings" : "设置"), "XAML loads the selected language");
            Require(WorkingSetCheck.Content?.ToString() == (english ? "Process working sets" : "进程工作集"), "Settings controls are translated");
            Require(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(PinSwitch) == (english ? "Always on top" : "窗口始终置顶"),
                "Accessibility labels are translated");
            Require(UiText.Get("TrayOpen") == (english ? "Open Mem Reduct" : "打开 Mem Reduct"), "Tray resources are translated");
            Require(UiText.Get("PermissionRequired") == (english ? "Cleaning memory requires administrator permission." : "清理内存需要管理员权限。"),
                "Permission messages are translated");
            Require(LanguageChoice.Items.Count == 3, "Only supported languages and the system option are listed");
            var selectedLanguage = LanguageChoice.SelectedIndex;
            LanguageChoice.SelectedIndex = selectedLanguage == 2 ? 1 : 2;
            var chosenLanguage = selectedLanguage == 2 ? "zh-CN" : "en-US";
            Require(_settings.Language == chosenLanguage, "Language selection reaches settings");
            var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;
            Require(roundTrip.Language == chosenLanguage && roundTrip.Areas == _settings.Areas, "Language survives serialization without changing cleaning areas");
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
            }
            _settings.Theme = savedTheme; ApplyTheme();
            var displayScale = Shell.XamlRoot.RasterizationScale;
            var buttonRadiusPixels = CleanButton.CornerRadius.TopLeft * displayScale;
            Require(Math.Abs(buttonRadiusPixels - 10) < 0.01,
                $"Button radius must be 10 screen pixels; actual={buttonRadiusPixels}, scale={displayScale}");
            foreach (var card in new[] { MetricsCard, CleanCard, AppearanceCard, CleanupSettingsCard, AutoSettingsCard, AboutCard })
                Require(Math.Abs(card.CornerRadius.TopLeft * displayScale - 12) < 0.01, "Cards must have 12 screen-pixel radii");
            checks.Add($"Light/dark/system caption themes and physical 12px card / 10px button radii passed at scale {displayScale}.");
            Require(Marshal.SizeOf<MemoryEngine.MemoryStatus>() == 64, "MEMORYSTATUSEX ABI");
            Require(Marshal.SizeOf<MemoryEngine.PerformanceInfo>() == 104, "PERFORMANCE_INFORMATION ABI");
            Require(Marshal.SizeOf<MemoryEngine.FileCacheInfo>() == 64, "SYSTEM_FILECACHE_INFORMATION ABI");
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
            options.CleanWorkingSets = options.CleanSystemCache = options.CleanLowPriorityStandby = false;
            Require(!options.IsAutoDue(100, now, now.AddDays(-1)), "Empty cleanup selection");
            var rejected = false;
            try { MemoryEngine.ValidateAreas((CleanAreas)8); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Unsupported cleanup command rejected");
            checks.Add("Settings bounds, automatic-cleanup deadlines, cooldown and cleanup selection passed.");

            UpdateStats(memory);
            var valueControl = PhysicalValue;
            var writes = 0;
            var callback = PhysicalValue.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => writes++);
            for (var i = 0; i < 1000; i++) UpdateStats(memory);
            PhysicalValue.UnregisterPropertyChangedCallback(TextBlock.TextProperty, callback);
            Require(writes == 0 && ReferenceEquals(valueControl, PhysicalValue), "Unchanged samples never rewrite text or replace controls");
            checks.Add("1,000 identical samples caused zero text writes; existing visual controls were retained.");

            // Layout is measured inside this hidden app window, without inspecting the user's desktop.
            Shell.RequestedTheme = ElementTheme.Light;
            SelectPage(false);
            Layout(680, 1600, 1);
            var fonts = Descendants(Shell).OfType<TextBlock>().ToDictionary(x => x, x => x.FontSize);
            var controlFonts = Descendants(OverviewPage).OfType<Control>().ToDictionary(x => x, x => x.FontSize);
            SelectPage(true); Layout(680, 3200, 1);
            foreach (var block in Descendants(Shell).OfType<TextBlock>()) fonts.TryAdd(block, block.FontSize);
            foreach (var control in Descendants(SettingsPage).OfType<Control>()) controlFonts.TryAdd(control, control.FontSize);
            var layouts = new List<object>();
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            foreach (var width in new[] { 560.0, 680.0, 880.0 })
            foreach (var settings in new[] { false, true })
            {
                SelectPage(settings);
                foreach (var (control, size) in controlFonts) control.FontSize = size * scale;
                foreach (var (block, size) in fonts) block.FontSize = size * scale;
                Layout(width, settings ? 3200 : 1600, scale);
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
                layouts.Add(new { width, textScale = scale, page = settings ? "settings" : "memory", checkedText });
            }
            checks.Add("18 memory/settings layout cases passed at widths 560/680/880 and text sizes 100/150/200%.");
            foreach (var (control, size) in controlFonts) control.FontSize = size;
            foreach (var (block, size) in fonts) block.FontSize = size;
            SelectPage(false); Layout(600, 520, 1);
            await WritePreview(directory, "memory-light.png");
            SelectPage(true); Layout(680, 1600, 1);
            await WritePreview(directory, "settings-light.png");
            Layout(600, 600, 1);
            await WritePreview(directory, "language-settings.png");
            Shell.RequestedTheme = ElementTheme.Dark;
            SelectPage(false); Layout(600, 520, 1);
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

    private async Task WritePreview(string directory, string filename)
    {
        await Task.Delay(250);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(Shell);
        var bytes = (await bitmap.GetPixelsAsync()).ToArray();
        using var stream = File.Open(Path.Combine(directory, filename), FileMode.Create, FileAccess.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight, 96, 96, bytes);
        await encoder.FlushAsync();
    }

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
