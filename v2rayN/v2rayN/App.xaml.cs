using v2rayN.Manager;
using v2rayN.Views;

namespace v2rayN;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private static readonly string StartupDiagnosticPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "v2rayN-MB", "startup-diagnostic.log");

    private static void StartupDiagnostic(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupDiagnosticPath)!);
            File.AppendAllText(StartupDiagnosticPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
    public static EventWaitHandle ProgramStarted;

    public App()
    {
        StartupDiagnostic("APP CONSTRUCTOR");
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// Open only one process
    /// </summary>
    /// <param name="e"></param>
    protected override void OnStartup(StartupEventArgs e)
    {
        StartupDiagnostic("ONSTARTUP ENTER");
        try
        {
        var exePathKey = Utils.GetMd5(Utils.GetExePath());

        var rebootas = e.Args.Any(t => t == Global.RebootAs);
        ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out var bCreatedNew);
        StartupDiagnostic($"Single instance: Reboot={rebootas}, CreatedNew={bCreatedNew}");
        if (!rebootas && !bCreatedNew)
        {
            StartupDiagnostic("SECOND INSTANCE EXIT");
            ProgramStarted.Set();
            Environment.Exit(0);
            return;
        }

        StartupDiagnostic("INIT APP BEGIN");
        if (!AppManager.Instance.InitApp())
        {
            StartupDiagnostic("INIT APP FAILED");
            UI.Show($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
            Environment.Exit(0);
            return;
        }

        StartupDiagnostic("INIT APP SUCCESS");
        AppManager.Instance.WindowDialog = new WindowDialog();

        StartupDiagnostic("INIT COMPONENTS BEGIN");
        AppManager.Instance.InitComponents();
        StartupDiagnostic("INIT COMPONENTS SUCCESS");

        StartupDiagnostic("REACTIVEUI BUILDER BEGIN");
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();
        StartupDiagnostic("REACTIVEUI BUILDER SUCCESS");

        base.OnStartup(e);
        StartupDiagnostic("BASE ONSTARTUP SUCCESS");

        StartupDiagnostic("MAIN VIEWMODEL BEGIN");
        var mainWindowViewModel = new MainWindowViewModel();
        StartupDiagnostic("MAIN VIEWMODEL CREATED");
        var viewFor = SimpleViewLocator.Instance.ResolveView(mainWindowViewModel);
        StartupDiagnostic("VIEW RESOLVED: " + (viewFor?.GetType().FullName ?? "NULL"));
        viewFor!.ViewModel = mainWindowViewModel;

        var mainWindow = (MainWindow)viewFor;
        MainWindow = mainWindow;
        StartupDiagnostic("MAINWINDOW ASSIGNED; SHOW BEGIN");
        mainWindow.Show();
        StartupDiagnostic($"SHOW RETURNED: Visible={mainWindow.IsVisible}, State={mainWindow.WindowState}");
        mainWindow.Activate();
        StartupDiagnostic("ACTIVATE CALLED");
        }
        catch (Exception ex)
        {
            StartupDiagnostic("STARTUP EXCEPTION: " + ex);
            throw;
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostic("DISPATCHER EXCEPTION: " + e.Exception);
        Logging.SaveLog("App_DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject != null)
        {
            StartupDiagnostic("APPDOMAIN EXCEPTION: " + e.ExceptionObject);
            Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupDiagnostic("UNOBSERVED TASK EXCEPTION: " + e.Exception);
        Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logging.SaveLog("OnExit");
        base.OnExit(e);
        Process.GetCurrentProcess().Kill();
    }
}
