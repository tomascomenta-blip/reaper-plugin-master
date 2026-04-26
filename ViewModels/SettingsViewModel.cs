// =============================================================================
// ViewModels/SettingsViewModel.cs
// =============================================================================
using System.IO;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReaperPluginManager.Services;
using Serilog;

namespace ReaperPluginManager.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IInstallerService _installer;
        private readonly ILogger _log;

        // CommunityToolkit genera la propiedad pública como PascalCase del campo
        // _vst2Path  → Vst2Path  (pero el XAML y el .cs del repo usan VST2Path)
        // Usamos propiedades manuales para mantener la capitalización exacta.

        private string _vst2Path  = string.Empty;
        private string _vst3Path  = string.Empty;
        private string _jsfxPath  = string.Empty;
        private string _reaperPath = string.Empty;

        public string VST2Path
        {
            get => _vst2Path;
            set => SetProperty(ref _vst2Path, value);
        }

        public string VST3Path
        {
            get => _vst3Path;
            set => SetProperty(ref _vst3Path, value);
        }

        public string JSFXPath
        {
            get => _jsfxPath;
            set => SetProperty(ref _jsfxPath, value);
        }

        public string ReaperPath
        {
            get => _reaperPath;
            set => SetProperty(ref _reaperPath, value);
        }

        [ObservableProperty] private bool   _autoScanOnAdd       = true;
        [ObservableProperty] private bool   _autoInstallSafe     = false;
        [ObservableProperty] private bool   _enableSandbox       = true;
        [ObservableProperty] private bool   _enableDefender      = true;
        [ObservableProperty] private bool   _checkUpdatesOnStart = true;
        [ObservableProperty] private string _logDirectory        = string.Empty;
        [ObservableProperty] private string _quarantineDirectory = string.Empty;

        public SettingsViewModel(IInstallerService installer, ILogger logger)
        {
            _installer = installer;
            _log       = logger.ForContext<SettingsViewModel>();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var paths   = _installer.GetDefaultInstallPaths();
            VST2Path    = paths.VST2Path;
            VST3Path    = paths.VST3Path;
            JSFXPath    = paths.JSFXPath;
            ReaperPath  = paths.ReaperPath;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var baseDir = Path.Combine(appData, "ReaperPluginManager");
            LogDirectory        = Path.Combine(baseDir, "Logs");
            QuarantineDirectory = Path.Combine(baseDir, "Quarantine");
        }

        [RelayCommand]
        private void SaveSettings()
        {
            _log.Information("Configuración guardada.");
        }

        [RelayCommand]
        private void OpenLogDirectory()
        {
            if (Directory.Exists(LogDirectory))
                System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
        }

        [RelayCommand]
        private void OpenQuarantineDirectory()
        {
            if (Directory.Exists(QuarantineDirectory))
                System.Diagnostics.Process.Start("explorer.exe", QuarantineDirectory);
        }
    }
}
