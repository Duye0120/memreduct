using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.UI.Xaml;

namespace MemReduct.Fluent;

public partial class App : Application
{
    private Mutex? _instance;
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
            if (!checking)
            {
                _instance = new Mutex(false, @"Local\MemReduct.Fluent." + WindowsIdentity.GetCurrent().User!.Value);
                var timeout = Arguments.Contains("--elevated") ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
                bool acquired;
                try { acquired = _instance.WaitOne(timeout); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { TrayIcon.ShowExistingWindow(); _instance.Dispose(); _instance = null; Exit(); return; }
            }
            var settings = checking ? new AppSettings { Language = "zh-CN" } : SettingsStore.Load();
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
                _window.RunSelfCheck(Path.GetFullPath(Arguments[index + 1]));
            }
            else if (!Arguments.Contains("--minimized") || !_window.CanStayInTray) _window.Activate();
        }
        catch (Exception error)
        {
            if (!Arguments.Contains("--self-check"))
                MessageBox(IntPtr.Zero, error.Message + "\n" + UiText.Get("OriginalUntouched"), "Mem Reduct Fluent", 0x10);
            else
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

    internal void Finish()
    {
        if (_instance is not null) { _instance.ReleaseMutex(); _instance.Dispose(); _instance = null; }
        Exit();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string text, string title, uint flags);
}
