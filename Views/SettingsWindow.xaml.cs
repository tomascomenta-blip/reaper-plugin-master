// Views/SettingsWindow.xaml.cs
using System.Windows;
using ReaperPluginManager.ViewModels;

namespace ReaperPluginManager.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
