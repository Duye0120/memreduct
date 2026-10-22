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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var checking = Arguments.Contains("--self-check");
            if (!checking && Arguments.Contains("--install-update"))
            {
                var index = Array.IndexOf(Arguments, "--install-update");
                if (index + 1 >= Arguments.Length) throw new ArgumentException("Invalid update request.");
                UiText.Initialize("System");
                await UpdateService.InstallDownloadedAsync(Arguments[index + 1]);
                Exit(); return;
            }
            if (!checking && Arguments.Contains("--configure-startup"))
            {
                var index = Array.IndexOf(Arguments, "--configure-startup");
                if (index + 2 >= Arguments.Length || Arguments[index + 1] is not ("on" or "off")) throw new ArgumentException("Invalid startup request.");
                StartupRegistration.ConfigureInstalled(Arguments[index + 1] == "on", Arguments[index + 2]);
                Exit(); return;
            }
            string? startupResumeError = null;
            if (!checking && StartupRegistration.ShouldReuseStartup(Installation.IsInstalled, MemoryEngine.IsAdministrator, Arguments))
            {
                try
                {
                    if (StartupRegistration.IsInstalledStartupEnabled())
                    {
                        // Do not acquire the instance mutex while the elevated process is starting.
                        StartupRegistration.RunInstalled();
                        if (await WaitForWindowAsync(TrayIcon.TryShowExistingWindow, TimeSpan.FromSeconds(10)))
                        { Exit(); return; }
                        throw new TimeoutException("The authorized startup task did not open a responsive window within 10 seconds.");
                    }
                }
                catch (Exception error) { startupResumeError = error.Message; }
            }
            if (!checking)
            {
                _instance = new Mutex(false, @"Local\MemReduct.Fluent." + WindowsIdentity.GetCurrent().User!.Value);
                var timeout = Arguments.Contains("--elevated") || Arguments.Contains("--restart") ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
                bool acquired;
                try { acquired = _instance.WaitOne(timeout); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { TrayIcon.ShowExistingWindow(); _instance.Dispose(); _instance = null; Exit(); return; }
                _installerMutex = new Mutex(false, @"Local\MemReduct.Fluent.Running");
                var importIndex = Array.IndexOf(Arguments, "--import-settings");
                if (Installation.IsInstalled && importIndex >= 0 && importIndex + 1 < Arguments.Length && Arguments[importIndex + 1].Length > 0)
                    SettingsStore.ImportIfMissing(Arguments[importIndex + 1], Installation.SettingsPath);
            }
            var settings = checking ? new AppSettings { Language = "zh-CN" } : SettingsStore.Load();
            var languageIndex = Array.IndexOf(Arguments, "--check-language");
            if (checking && languageIndex >= 0 && languageIndex + 1 < Arguments.Length)
                settings.Language = Arguments[languageIndex + 1];
            settings.Normalize();
            UiText.Initialize(settings.Language);
            _window = new MainWindow(settings, checking);
            if (startupResumeError is not null) _window.ReportStartupResumeFailure(startupResumeError);
            if (checking)
            {
                var index = Array.IndexOf(Arguments, "--self-check");
                if (index + 1 >= Arguments.Length) throw new ArgumentException("--self-check 需要一个输出目录。");
                _window.RunSelfCheck(Path.GetFullPath(Arguments[index + 1]));
            }
            else if (!Arguments.Contains("--minimized") || !_window.CanStayInTray) _window.Activate();
        }
        catch (Exception error)
        {
            if (!Arguments.Contains("--self-check") && !Arguments.Contains("--configure-startup"))
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

    internal static async Task<bool> WaitForWindowAsync(Func<bool> showWindow, TimeSpan timeout)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (showWindow()) return true;
            var remaining = timeout - timer.Elapsed;
            if (remaining <= TimeSpan.Zero) return false;
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100));
        }
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
