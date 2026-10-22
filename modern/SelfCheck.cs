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
            Shell.RequestedTheme = ElementTheme.Dark;
            SelectPage(false); Layout(600, 520, 1);
            await WritePreview(directory, "memory-dark.png");
            File.WriteAllText(Path.Combine(directory, "self-check.json"), JsonSerializer.Serialize(new
            { passed = true, checks, layouts, physicalTotal = memory.PhysicalTotal, cacheAvailable = memory.CacheCurrent.HasValue },
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
