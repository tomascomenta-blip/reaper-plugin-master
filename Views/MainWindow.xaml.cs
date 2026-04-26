// =============================================================================
// Views/MainWindow.xaml.cs
// =============================================================================
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ReaperPluginManager.ViewModels;

namespace ReaperPluginManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = App.Services.GetRequiredService<MainViewModel>();
            DataContext = vm;
            Loaded += async (_, _) => await vm.InitializeAsync();
        }
    }
}
