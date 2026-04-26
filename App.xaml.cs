// =============================================================================
// App.xaml.cs
// Punto de entrada: configuración de DI, Serilog, y arranque de la ventana principal
// =============================================================================
using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ReaperPluginManager.Database;
using ReaperPluginManager.Services;
using ReaperPluginManager.ViewModels;
using Serilog;

namespace ReaperPluginManager
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        public new static App Current => (App)Application.Current;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureLogging();
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            Log.Information("ReaperPluginManager arrancando...");

            var mainWindow = Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();

            // Manejador global de excepciones no controladas
            DispatcherUnhandledException += (_, ex) =>
            {
                Log.Fatal(ex.Exception, "Excepción no controlada en UI thread");
                MessageBox.Show(
                    $"Error inesperado:\n{ex.Exception.Message}\n\nRevisa los logs para más detalles.",
                    "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                Log.Fatal(ex.ExceptionObject as Exception, "Excepción en dominio de aplicación");
            };

            TaskScheduler.UnobservedTaskException += (_, ex) =>
            {
                Log.Warning(ex.Exception, "Tarea no observada con excepción");
                ex.SetObserved();
            };
        }

        private static void ConfigureLogging()
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReaperPluginManager", "Logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()
                .CreateLogger();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddSingleton<ILogger>(Log.Logger);

            // HTTP
            services.AddHttpClient("PluginDownloader", client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/octet-stream, application/json");
                client.Timeout = TimeSpan.FromMinutes(10);
            });

            // Base de datos
            services.AddSingleton<IPluginDatabase, PluginDatabase>();

            // Servicios
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<ISandboxService,  SandboxService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IInstallerService, InstallerService>();
            services.AddSingleton<IUpdateService,   UpdateService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<AddPluginViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<PluginDetailViewModel>();

            // Vistas
            services.AddTransient<Views.MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("ReaperPluginManager cerrando...");
            Log.CloseAndFlush();
            if (Services is IDisposable d) d.Dispose();
            base.OnExit(e);
        }
    }
}
