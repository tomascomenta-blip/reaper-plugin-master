// =============================================================================
// Views/AddPluginDialog.xaml.cs
// =============================================================================
using System.Windows;
using ReaperPluginManager.ViewModels;

namespace ReaperPluginManager.Views
{
    public partial class AddPluginDialog : Window
    {
        public AddPluginDialog(AddPluginViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequested += (_, result) =>
            {
                DialogResult = result;
                Close();
            };
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
