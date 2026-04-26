// =============================================================================
// ViewModels/AddPluginViewModel.cs
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReaperPluginManager.Database;
using ReaperPluginManager.Models;

namespace ReaperPluginManager.ViewModels
{
    public partial class AddPluginViewModel : ObservableObject
    {
        private readonly IPluginDatabase _db;
        public event EventHandler<bool>? CloseRequested;

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _developer = string.Empty;
        [ObservableProperty] private string _version = "1.0.0";
        [ObservableProperty] private PluginFormat _format = PluginFormat.VST3;
        [ObservableProperty] private string _downloadUrl = string.Empty;
        [ObservableProperty] private string _expectedSHA256 = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _category = "Sin categoría";
        [ObservableProperty] private string _tagsText = string.Empty;
        [ObservableProperty] private string _validationError = string.Empty;

        public List<PluginFormat> AvailableFormats { get; } =
            Enum.GetValues<PluginFormat>().Where(f => f != PluginFormat.Unknown).ToList();

        public List<string> AvailableCategories { get; private set; } = new();

        public Plugin? CreatedPlugin { get; private set; }

        public AddPluginViewModel(IPluginDatabase db)
        {
            _db = db;
            LoadCategories();
        }

        private void LoadCategories()
        {
            AvailableCategories = _db.GetAllCategories()
                .Select(c => c.Name)
                .ToList();
            AvailableCategories.Insert(0, "Sin categoría");
        }

        [RelayCommand]
        private void BrowseLocalFile()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Seleccionar archivo de plugin",
                Filter = "Plugins de audio|*.dll;*.vst3;*.jsfx|Archivos ZIP|*.zip|Todos|*.*"
            };
            if (dlg.ShowDialog() == true)
                DownloadUrl = $"file:///{dlg.FileName.Replace('\\', '/')}";
        }

        [RelayCommand]
        private async Task AddAndInstall()
        {
            ValidationError = string.Empty;

            if (string.IsNullOrWhiteSpace(Name))
            { ValidationError = "El nombre es requerido."; return; }

            if (string.IsNullOrWhiteSpace(DownloadUrl))
            { ValidationError = "La URL o ruta de descarga es requerida."; return; }

            CreatedPlugin = new Plugin
            {
                Name         = Name.Trim(),
                Developer    = Developer.Trim(),
                Version      = Version.Trim(),
                Format       = Format,
                DownloadUrl  = DownloadUrl.Trim(),
                ExpectedSHA256 = ExpectedSHA256.Trim(),
                Description  = Description.Trim(),
                Category     = Category,
                Tags         = TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim())
                                       .Where(t => !string.IsNullOrEmpty(t))
                                       .ToList(),
                Status       = PluginStatus.Pending
            };

            _db.UpsertPlugin(CreatedPlugin);
            await Task.CompletedTask;
            CloseRequested?.Invoke(this, true);
        }
    }
}
