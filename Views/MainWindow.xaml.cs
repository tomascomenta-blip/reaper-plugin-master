// =============================================================================
// Views/MainWindow.xaml.cs
// =============================================================================
using System.Windows;
using System.Windows.Input;
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

        // Permite mover la ventana arrastrando la barra de título
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Doble clic: maximizar/restaurar
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
