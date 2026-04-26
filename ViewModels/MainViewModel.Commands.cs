// =============================================================================
// ViewModels/MainViewModel.Commands.cs
// Comandos adicionales del ViewModel principal (partial class)
// =============================================================================
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ReaperPluginManager.Models;
using ReaperPluginManager.Services;
using ReaperPluginManager.Views;

namespace ReaperPluginManager.ViewModels
{
    public partial class MainViewModel
    {
        // ─── Abrir diálogo de agregar plugin ─────────────────────────────────
        [RelayCommand]
        public async Task OpenAddPlugin()
        {
            var vm  = App.Services.GetRequiredService<AddPluginViewModel>();
            var dlg = new AddPluginDialog(vm) { Owner = GetMainWindow() };

            if (dlg.ShowDialog() == true && vm.CreatedPlugin != null)
            {
                var pluginVm = new PluginViewModel(vm.CreatedPlugin);
                Plugins.Add(pluginVm);
                SelectedPlugin = pluginVm;
                UpdateStats();

                // Preguntar si instalar ahora
                var result = MessageBox.Show(
                    $"Plugin '{vm.CreatedPlugin.Name}' agregado.\n¿Iniciar instalación ahora?",
                    "Plugin Agregado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    await RunFullPipelineCommand.ExecuteAsync(pluginVm);
            }
        }

        // ─── Abrir configuración ──────────────────────────────────────────────
        [RelayCommand]
        public void OpenSettings()
        {
            var vm  = App.Services.GetRequiredService<SettingsViewModel>();
            var dlg = new SettingsWindow(vm) { Owner = GetMainWindow() };
            dlg.ShowDialog();
        }

        // ─── Filtros rápidos por vista ────────────────────────────────────────
        [RelayCommand]
        public async Task SetQuickFilter(string filter)
        {
            ShowOnlyFavorites = false;
            ShowOnlyInstalled = false;
            FilterStatus      = null;

            switch (filter)
            {
                case "Installed":  ShowOnlyInstalled = true; break;
                case "Favorites":  ShowOnlyFavorites = true; break;
                case "Pending":    FilterStatus = PluginStatus.Pending; break;
                case "Blocked":    FilterStatus = PluginStatus.Blocked; break;
                default: break; // "All"
            }
            await ApplyFiltersAsync();
        }

        // ─── Filtrar por categoría ────────────────────────────────────────────
        [RelayCommand]
        public async Task FilterByCategory(string category)
        {
            SelectedCategory = category;
            await ApplyFiltersAsync();
        }

        // ─── Limpiar filtros ──────────────────────────────────────────────────
        [RelayCommand]
        public async Task ClearFilters()
        {
            SearchQuery       = string.Empty;
            FilterFormat      = null;
            FilterStatus      = null;
            ShowOnlyFavorites = false;
            ShowOnlyInstalled = false;
            SelectedCategory  = "Todos";
            await LoadPluginsAsync();
        }

        // ─── Eliminar plugin de la lista ──────────────────────────────────────
        [RelayCommand]
        public void RemovePlugin(PluginViewModel vm)
        {
            var confirm = MessageBox.Show(
                $"¿Eliminar '{vm.Plugin.Name}' de la lista?\nEsto no desinstala el plugin del sistema.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            _db.DeletePlugin(vm.Plugin.Id);
            Plugins.Remove(vm);
            if (SelectedPlugin == vm) SelectedPlugin = null;
            UpdateStats();
            Log($"🗑️ {vm.Plugin.Name} eliminado de la lista.");
        }

        // ─── Menú de categorías ───────────────────────────────────────────────
        [RelayCommand]
        public void OpenCategoryMenu(PluginViewModel vm)
        {
            // En producción mostrar un popup con las categorías disponibles
            var cats = Categories.Select(c => c.Name).ToList();
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Ingresa la categoría:",
                "Cambiar Categoría",
                vm.Plugin.Category);

            if (!string.IsNullOrWhiteSpace(input))
                _ = AssignCategoryCommand.ExecuteAsync((vm, input));
        }

        // ─── Verificar actualizaciones ────────────────────────────────────────
        [RelayCommand]
        public async Task CheckForUpdates()
        {
            var updateSvc = App.Services.GetRequiredService<IUpdateService>();
            Log("🔄 Verificando actualizaciones...");
            StatusMessage = "Verificando actualizaciones...";

            var updates = (await updateSvc.CheckForUpdatesAsync()).ToList();

            if (updates.Count == 0)
            {
                Log("✅ Todos los plugins están actualizados.");
                StatusMessage = "Todos los plugins están actualizados.";
            }
            else
            {
                Log($"📢 {updates.Count} actualización(es) disponible(s).");
                StatusMessage = $"{updates.Count} actualización(es) disponible(s).";

                // Refrescar la lista para mostrar los indicadores de actualización
                await LoadPluginsAsync();
                UpdateStats();
            }
        }

        // ─── Asignar rating ───────────────────────────────────────────────────
        [RelayCommand]
        public async Task SetRating(int rating)
        {
            if (SelectedPlugin == null) return;
            SelectedPlugin.Plugin.UserRating = rating;
            _db.UpsertPlugin(SelectedPlugin.Plugin);
            SelectedPlugin.Refresh();
            Log($"⭐ {SelectedPlugin.Plugin.Name} calificado con {rating}/5");
            await Task.CompletedTask;
        }

        // ─── Helper para obtener ventana principal ────────────────────────────
        private static Window? GetMainWindow() =>
            Application.Current?.MainWindow;
    }
}
