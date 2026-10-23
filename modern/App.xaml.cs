using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.UI.Xaml;

namespace MemReduct.Fluent;

public partial class App : Application
{
    private Mutex? _instance;
    private Mutex? _installerMutex;
    private MainWindow? _window;
    internal static string[] Arguments => Environment.GetCommandLineArgs();
    public App()
    {
        UnhandledException += (_, args) => RecordCheckFailure(args.Exception);
        try { InitializeComponent(); }
        catch (Exception error) { RecordCheckFailure(error); throw; }
    }

    private static void RecordCheckFailure(Exception error)
    {
        var index = Array.IndexOf(Arguments, "--self-check");
        if (index < 0 || index + 1 >= Arguments.Length) return;
        var directory = Path.GetFullPath(Arguments[index + 1]);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "self-check.json"),
            System.Text.Json.JsonSerializer.Serialize(new { passed = false, error = error.ToString() }));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var checking = Arguments.Contains("--self-check");
            AppSettings? elevatedSettings = null;
            if (!checking && Arguments.Contains(ElevationHandoff.Argument))
            {
                Guid request;
                try { request = ElevationHandoff.Validate(Arguments, WindowsIdentity.GetCurrent().User!.Value, MemoryEngine.IsAdministrator); }
                catch (UnauthorizedAccessException)
                { Environment.ExitCode = ElevationHandoff.WrongAccountExitCode; Exit(); return; }
                // Read only after matching the caller; the original window stays alive until this succeeds.
                elevatedSettings = SettingsStore.Load();
                ElevationHandoff.WaitForTransfer(request, TimeSpan.FromSeconds(10));
            }
            if (!checking)
            {
                var timeout = Arguments.Contains("--elevated") || Arguments.Contains("--restart") ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
                using var identity = WindowsIdentity.GetCurrent();
                _instance = AcquireInstanceMutex(@"Local\MemReduct.Fluent." + identity.User!.Value, timeout);
                if (_instance is null) { TrayIcon.ShowExistingWindow(); Exit(); return; }
                _installerMutex = new Mutex(false, @"Local\MemReduct.Fluent.Running");
                var importIndex = Array.IndexOf(Arguments, "--import-settings");
                if (Installation.IsInstalled && importIndex >= 0 && importIndex + 1 < Arguments.Length && Arguments[importIndex + 1].Length > 0)
                    SettingsStore.ImportIfMissing(Arguments[importIndex + 1], Installation.SettingsPath);
            }
            var settings = checking ? new AppSettings { Language = "zh-CN" } : elevatedSettings ?? SettingsStore.Load();
            var languageIndex = Array.IndexOf(Arguments, "--check-language");
            if (checking && languageIndex >= 0 && languageIndex + 1 < Arguments.Length)
                settings.Language = Arguments[languageIndex + 1];
            settings.Normalize();
            UiText.Initialize(settings.Language);
            _window = new MainWindow(settings, checking);
            if (checking)
            {
                var index = Array.IndexOf(Arguments, "--self-check");
                if (index + 1 >= Arguments.Length) throw new ArgumentException("--self-check 需要一个输出目录。");
                var directory = Path.GetFullPath(Arguments[index + 1]);
                if (Arguments.Contains("--visual-preview")) _window.RunAnimationPreview(directory);
                else _window.RunSelfCheck(directory);
            }
            else if (!Arguments.Contains("--minimized") || !_window.CanStayInTray) _window.Activate();
        }
        catch (Exception error)
        {
            if (!Arguments.Contains("--self-check") && !Arguments.Contains("--configure-startup") && !Arguments.Contains(ElevationHandoff.Argument))
                MessageBox(IntPtr.Zero, error.Message + "\n" + UiText.Get("OriginalUntouched"), "Mem Reduct Fluent", 0x10);
            else if (Arguments.Contains("--self-check"))
            {
                Console.Error.WriteLine(error);
                var index = Array.IndexOf(Arguments, "--self-check");
                if (index + 1 < Arguments.Length)
                {
                    var directory = Path.GetFullPath(Arguments[index + 1]);
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "self-check.json"),
                        System.Text.Json.JsonSerializer.Serialize(new { passed = false, error = error.ToString() }));
                }
            }
            Environment.ExitCode = 1;
            Finish();
        }
    }

    internal static async Task<bool> WaitUntilAsync(Func<bool> ready, TimeSpan timeout)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (ready()) return true;
            var remaining = timeout - timer.Elapsed;
            if (remaining <= TimeSpan.Zero) return false;
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100));
        }
    }

    internal static Mutex? AcquireInstanceMutex(string name, TimeSpan timeout)
    {
        var instance = new Mutex(false, name);
        var acquired = false;
        try
        {
            try { acquired = instance.WaitOne(timeout); }
            catch (AbandonedMutexException) { acquired = true; }
            return acquired ? instance : null;
        }
        finally { if (!acquired) instance.Dispose(); }
    }

    internal void Finish()
    {
        if (_instance is not null) { _instance.ReleaseMutex(); _instance.Dispose(); _instance = null; }
        _installerMutex?.Dispose();
        Exit();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string text, string title, uint flags);
}
