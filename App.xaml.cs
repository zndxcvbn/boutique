using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autofac;
using RequiemGlamPatcher.Models;
using RequiemGlamPatcher.Services;
using RequiemGlamPatcher.ViewModels;
using RequiemGlamPatcher.Views;

namespace RequiemGlamPatcher;

public partial class App : Application
{
    private IContainer? _container;
    private readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RequiemGlamPatcher.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitializeLogging();
        Log("Application startup invoked.");

        // Configure dependency injection
        var builder = new ContainerBuilder();

        // Register models
        builder.RegisterType<PatcherSettings>().AsSelf().SingleInstance();

        // Register services
        builder.RegisterType<MutagenService>().As<IMutagenService>().SingleInstance();
        builder.RegisterType<PatchingService>().As<IPatchingService>().SingleInstance();
        builder.RegisterType<MatchingService>().As<IMatchingService>().SingleInstance();

        // Register ViewModels
        builder.RegisterType<MainViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<SettingsViewModel>().AsSelf().SingleInstance();

        // Register Views
        builder.RegisterType<MainWindow>().AsSelf();

        _container = builder.Build();

        // Show main window
        try
        {
            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.Show();
            Log("Main window displayed.");
        }
        catch (Exception ex)
        {
            Log("Failed to show main window.", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("Application exit.");
        _container?.Dispose();
        base.OnExit(e);
    }

    private void InitializeLogging()
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Log("Logging initialized.");

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Log("AppDomain unhandled exception.", args.ExceptionObject as Exception);

            DispatcherUnhandledException += (_, args) =>
            {
                Log("Dispatcher unhandled exception.", args.Exception);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log("Unobserved task exception.", args.Exception);
                args.SetObserved();
            };
        }
        catch
        {
            // Ignored – we avoid throwing during startup if logging fails.
        }
    }

    private void Log(string message, Exception? ex = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            if (ex != null)
            {
                builder.AppendLine(ex.ToString());
            }

            File.AppendAllText(_logFilePath, builder.ToString());
        }
        catch
        {
            // Swallow logging failures to prevent secondary crashes.
        }
    }
}
