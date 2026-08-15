using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EPFOptimizerPro;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\EPFOptimizerPro-SingleInstance";
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);

        if (!_ownsSingleInstanceMutex)
        {
            MessageBox.Show(
                "EPF Optimizer Pro est deja lance.",
                "EPF Optimizer Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandledException", e.Exception);

        MessageBox.Show(
            "Une erreur a été interceptée par la protection anti-crash." + Environment.NewLine + Environment.NewLine +
            e.Exception.Message + Environment.NewLine + Environment.NewLine +
            "Un journal a été écrit dans C:\\ProgramData\\EPFOptimizerPro\\crash.log.",
            "EPF Optimizer Pro - Protection anti-crash",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogCrash("UnhandledException", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
    private static void LogCrash(string source, Exception? exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EPFOptimizerPro");

            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, "crash.log");
            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine($"Date       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Source     : {source}");
            sb.AppendLine($"Message    : {exception?.Message ?? "Exception non disponible"}");
            sb.AppendLine("Details    :");
            sb.AppendLine(exception?.ToString() ?? "Aucun detail disponible.");
            sb.AppendLine();

            File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Ne jamais provoquer un deuxième crash pendant la journalisation.
        }
    }
}
