// =============================================================================
// ViewModels/PluginDetailViewModel.cs
// =============================================================================
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReaperPluginManager.Models;

namespace ReaperPluginManager.ViewModels
{
    public partial class PluginDetailViewModel : ObservableObject
    {
        [ObservableProperty] private Plugin? _plugin;

        public static IReadOnlyList<int> StarRange { get; } = new[] { 1, 2, 3, 4, 5 };
    }

    public partial class PluginViewModel
    {
        // Propiedad estática para el range de estrellas en la UI
        public static IReadOnlyList<int> StarRange { get; } = new[] { 1, 2, 3, 4, 5 };
    }
}
