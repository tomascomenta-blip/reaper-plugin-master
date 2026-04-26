// =============================================================================
// ViewModels/MainViewModel.cs
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ReaperPluginManager.Database;
using ReaperPluginManager.Models;
using ReaperPluginManager.Services;
using Serilog;

namespace ReaperPluginManager.ViewModels
{
    // ─── Wrapper observable para Plugin ──────────────────────────────────────
    public partial class PluginViewModel : ObservableObject
    {
        public Plugin Plugin { get; }

        public PluginViewModel(Plugin plugin) => Plugin = plugin;

        public PluginStatus        Status         => Plugin.Status;
        public SecurityClassification SecurityStatus => Plugin.SecurityStatus;
        public bool IsFavorite                    => Plugin.IsFavorite;

        public string StatusIcon => Plugin.Status switch
        {
            PluginStatus.Installed       => "✅",
            PluginStatus.Blocked         => "🚫",
            PluginStatus.Failed          => "❌",
            PluginStatus.Downloading     => "⬇️",
            PluginStatus.Scanning        => "🔍",
            PluginStatus.Testing         => "🧪",
            PluginStatus.Installing      => "📦",
            PluginStatus.UpdateAvailable => "🔄",
            _                            => "⏳"
        };

        public string SecurityIcon => Plugin.SecurityStatus switch
        {
            SecurityClassification.Safe       => "🛡️ Safe",
            SecurityClassification.Suspicious => "⚠️ Susp.",
            SecurityClassification.Blocked    => "🚫 Block",
            _                                 => "❓ ??"
        };

        public void Refresh() => OnPropertyChanged(string.Empty);
    }

    // ─── MainViewModel ────────────────────────────────────────────────────────
    public partial class MainViewModel : ObservableObject
    {
        internal readonly IPluginDatabase _db;
        private readonly ISecurityService _security;
        private readonly ISandboxService  _sandbox;
        private readonly IDownloadService _downloader;
        private readonly IInstallerService _installer;
        private readonly ILogger _log;

        [ObservableProperty] private ObservableCollection<PluginViewModel> _plugins  = new();
        [ObservableProperty] private ObservableCollection<PluginCategory>  _categories = new();
        [ObservableProperty] private ObservableCollection<string>          _activityLog = new();

        [ObservableProperty] private PluginViewModel? _selectedPlugin;
        [ObservableProperty] private string _searchQuery     = string.Empty;
        [ObservableProperty] private string _statusMessage   = "Listo";
        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private bool   _isOperationRunning;
        [ObservableProperty] private double _overallProgress;
        [ObservableProperty] private bool   _showOnlyFavorites;
        [ObservableProperty] private bool   _showOnlyInstalled;
        [ObservableProperty] private string _selectedCategory = "Todos";

        [ObservableProperty] private PluginStatus? _filterStatus;
        [ObservableProperty] private string?       _filterFormat;

        // Stats
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _installedCount;
        [ObservableProperty] private int _blockedCount;
        [ObservableProperty] private int _favoritesCount;

        private CancellationTokenSource? _cts;

        public MainViewModel(
            IPluginDatabase db,
            ISecurityService security,
            ISandboxService sandbox,
            IDownloadService downloader,
            IInstallerService installer,
            ILogger logger)
        {
            _db         = db;
            _security   = security;
            _sandbox    = sandbox;
            _downloader = downloader;
            _installer  = installer;
            _log        = logger.ForContext<MainViewModel>();
        }

        // ─── Inicialización ───────────────────────────────────────────────────
        public async Task InitializeAsync()
        {
            await LoadPluginsAsync();
            LoadCategories();
        }

        internal async Task LoadPluginsAsync()
        {
            IsLoading = true;
            try
            {
                var all = await Task.Run(() => _db.GetAllPlugins().ToList());
                Plugins = new ObservableCollection<PluginViewModel>(
                    all.Select(p => new PluginViewModel(p)));
                UpdateStats();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadCategories()
        {
            var cats = _db.GetAllCategories().ToList();
            Categories = new ObservableCollection<PluginCategory>(cats);
        }

        internal void UpdateStats()
        {
            TotalCount     = Plugins.Count;
            InstalledCount = Plugins.Count(p => p.Plugin.IsInstalled);
            BlockedCount   = Plugins.Count(p => p.Plugin.Status == PluginStatus.Blocked);
            FavoritesCount = Plugins.Count(p => p.Plugin.IsFavorite);
        }

        // ─── Filtros ──────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task ApplyFilters()
        {
            IsLoading = true;
            try
            {
                var all = await Task.Run(() => _db.GetAllPlugins().ToList());

                var filtered = all.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var q = SearchQuery.Trim().ToLowerInvariant();
                    filtered = filtered.Where(p =>
                        p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        p.Developer.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
                }

                if (ShowOnlyFavorites) filtered = filtered.Where(p => p.IsFavorite);
                if (ShowOnlyInstalled) filtered = filtered.Where(p => p.IsInstalled);
                if (FilterStatus.HasValue)   filtered = filtered.Where(p => p.Status == FilterStatus.Value);
                if (!string.IsNullOrEmpty(FilterFormat))
                    filtered = filtered.Where(p => p.Format.ToString() == FilterFormat);

                if (SelectedCategory != "Todos" && !string.IsNullOrEmpty(SelectedCategory))
                    filtered = filtered.Where(p => p.Category == SelectedCategory);

                Plugins = new ObservableCollection<PluginViewModel>(
                    filtered.Select(p => new PluginViewModel(p)));
                UpdateStats();
            }
            finally
            {
                IsLoading = false;
            }
        }

        internal async Task ApplyFiltersAsync() => await ApplyFilters();

        // ─── Pipeline de instalación ──────────────────────────────────────────
        [RelayCommand]
        public async Task RunFullPipeline(PluginViewModel vm)
        {
            _cts = new CancellationTokenSource();
            IsOperationRunning = true;
            OverallProgress    = 0;
            var plugin = vm.Plugin;

            try
            {
                // 1. Descarga
                SetStatus(plugin, PluginStatus.Downloading, vm);
                Log($"⬇️ Descargando {plugin.Name}...");

                var dlProgress = new Progress<double>(p =>
                {
                    OverallProgress = p * 0.25;
                    StatusMessage   = $"Descargando... {p:F0}%";
                });

                var dlResult = await _downloader.DownloadPluginAsync(plugin, dlProgress, _cts.Token);
                if (!dlResult.Success) { Fail(plugin, vm, dlResult.Error); return; }

                plugin.TempFilePath   = dlResult.FilePath;
                plugin.FileSizeBytes  = dlResult.Bytes;

                // 2. Seguridad
                SetStatus(plugin, PluginStatus.Scanning, vm);
                Log($"🔍 Escaneando seguridad de {plugin.Name}...");
                OverallProgress = 25;

                var secResult = await _security.ScanFileAsync(
                    plugin.TempFilePath, plugin.ExpectedSHA256,
                    new Progress<string>(s => StatusMessage = s), _cts.Token);

                plugin.LastSecurityResult = secResult;
                plugin.SecurityStatus     = secResult.Classification;

                if (secResult.Classification == SecurityClassification.Blocked)
                {
                    SetStatus(plugin, PluginStatus.Blocked, vm);
                    Log($"🚫 {plugin.Name} bloqueado: amenaza detectada");
                    _db.UpsertPlugin(plugin);
                    return;
                }

                // 3. Sandbox
                SetStatus(plugin, PluginStatus.Testing, vm);
                Log($"🧪 Sandbox de {plugin.Name}...");
                OverallProgress = 50;

                var sbResult = await _sandbox.TestPluginAsync(
                    plugin,
                    new Progress<string>(s => StatusMessage = s), _cts.Token);

                plugin.LastSandboxResult = sbResult;

                if (!sbResult.Passed)
                {
                    Log($"⚠️ Sandbox advertencia: {sbResult.Verdict}");
                }

                // 4. Instalación
                SetStatus(plugin, PluginStatus.Installing, vm);
                Log($"📦 Instalando {plugin.Name}...");
                OverallProgress = 75;

                var instResult = await _installer.InstallPluginAsync(
                    plugin, null,
                    new Progress<string>(s => StatusMessage = s), _cts.Token);

                if (!instResult.Success) { Fail(plugin, vm, instResult.Error); return; }

                // Éxito
                plugin.InstallPath   = instResult.InstallPath;
                plugin.IsInstalled   = true;
                plugin.InstalledDate = DateTime.UtcNow;
                plugin.LastScanDate  = DateTime.UtcNow;
                SetStatus(plugin, PluginStatus.Installed, vm);
                _db.UpsertPlugin(plugin);

                OverallProgress = 100;
                Log($"✅ {plugin.Name} instalado correctamente en {plugin.InstallPath}");
                StatusMessage = $"{plugin.Name} instalado.";
                UpdateStats();
            }
            catch (OperationCanceledException)
            {
                SetStatus(plugin, PluginStatus.Pending, vm);
                Log($"⏹️ Instalación de {plugin.Name} cancelada.");
                StatusMessage = "Operación cancelada.";
            }
            catch (Exception ex)
            {
                Fail(plugin, vm, ex.Message);
                _log.Error(ex, "Error en pipeline de {Name}", plugin.Name);
            }
            finally
            {
                IsOperationRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        [RelayCommand]
        public void CancelOperation() => _cts?.Cancel();

        [RelayCommand]
        public void ToggleFavorite(PluginViewModel vm)
        {
            vm.Plugin.IsFavorite = !vm.Plugin.IsFavorite;
            _db.UpsertPlugin(vm.Plugin);
            vm.Refresh();
            UpdateStats();
        }

        [RelayCommand]
        public async Task UninstallPlugin(PluginViewModel vm)
        {
            var ok = await _installer.UninstallPluginAsync(vm.Plugin);
            if (ok)
            {
                vm.Plugin.IsInstalled = false;
                vm.Plugin.Status      = PluginStatus.Pending;
                _db.UpsertPlugin(vm.Plugin);
                vm.Refresh();
                UpdateStats();
                Log($"🗑️ {vm.Plugin.Name} desinstalado.");
            }
        }

        [RelayCommand]
        public async Task AssignCategory((PluginViewModel Vm, string Category) args)
        {
            args.Vm.Plugin.Category = args.Category;
            _db.UpsertPlugin(args.Vm.Plugin);
            args.Vm.Refresh();
            Log($"📁 {args.Vm.Plugin.Name} → categoría '{args.Category}'");
            await Task.CompletedTask;
        }

        [RelayCommand]
        public void CreateCategory()
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre de la nueva categoría:", "Nueva Categoría", string.Empty);
            if (string.IsNullOrWhiteSpace(name)) return;

            var cat = new PluginCategory { Name = name, Color = "#607D8B" };
            _db.UpsertCategory(cat);
            Categories.Add(cat);
            Log($"📁 Categoría creada: {name}");
        }

        // ─── Helpers internos ─────────────────────────────────────────────────
        internal void Log(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (ActivityLog.Count > 200) ActivityLog.RemoveAt(ActivityLog.Count - 1);
            });
        }

        private void SetStatus(Plugin plugin, PluginStatus status, PluginViewModel vm)
        {
            plugin.Status = status;
            _db.UpsertPlugin(plugin);
            vm.Refresh();
            StatusMessage = status.ToString();
        }

        private void Fail(Plugin plugin, PluginViewModel vm, string error)
        {
            plugin.Status = PluginStatus.Failed;
            _db.UpsertPlugin(plugin);
            vm.Refresh();
            StatusMessage = $"Error: {error}";
            Log($"❌ Error en {plugin.Name}: {error}");
        }
    }
}
