// =============================================================================
// Views/MainWindow.xaml.cs
// =============================================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using ReaperPluginManager.ViewModels;

namespace ReaperPluginManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var chrome = new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false,
                NonClientFrameEdges = NonClientFrameEdges.None
            };
            WindowChrome.SetWindowChrome(this, chrome);

            var vm = App.Services.GetRequiredService<MainViewModel>();
            DataContext = vm;
            Loaded += async (_, _) => await vm.InitializeAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        // Abre el menú contextual de los 3 puntos con click izquierdo
        private void DotsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // Abre la ventana de búsqueda online
        private void BtnBuscarOnline_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new BuscarOnlineWindow();
            ventana.Owner = this;
            ventana.ShowDialog();
        }

        // ── Navegación entre pestaña Plugins / Ayuda ──────────────────────────

        // Colores usados en la navegación de Ayuda (hardcoded para evitar
        // dependencia de recursos MaterialDesign que pueden no estar listos)
        private static readonly System.Windows.Media.SolidColorBrush _accentBrush =
            new(System.Windows.Media.Color.FromRgb(0x62, 0x00, 0xEE));   // #6200EE — primary de MaterialDesign
        private static readonly System.Windows.Media.SolidColorBrush _mutedBrush =
            new(System.Windows.Media.Color.FromRgb(0x78, 0x90, 0x9C));   // #78909C
        private static readonly System.Windows.Media.SolidColorBrush _whiteBrush =
            System.Windows.Media.Brushes.White;
        private static readonly System.Windows.Media.SolidColorBrush _transpBrush =
            System.Windows.Media.Brushes.Transparent;

        private void TabBtnPlugins_Click(object sender, RoutedEventArgs e)
        {
            PanelPlugins.Visibility = Visibility.Visible;
            PanelAyuda.Visibility   = Visibility.Collapsed;

            TabBtnPlugins.Foreground  = _whiteBrush;
            TabBtnPlugins.BorderBrush = _accentBrush;
            TabBtnAyuda.Foreground    = _mutedBrush;
            TabBtnAyuda.BorderBrush   = _transpBrush;
        }

        private void TabBtnAyuda_Click(object sender, RoutedEventArgs e)
        {
            PanelPlugins.Visibility = Visibility.Collapsed;
            PanelAyuda.Visibility   = Visibility.Visible;

            TabBtnAyuda.Foreground    = _whiteBrush;
            TabBtnAyuda.BorderBrush   = _accentBrush;
            TabBtnPlugins.Foreground  = _mutedBrush;
            TabBtnPlugins.BorderBrush = _transpBrush;
        }

        // ── Navegación de secciones dentro de Ayuda ──────────────────────────

        private static readonly string[] _helpSecNames =
        {
            "HelpSec0","HelpSec1","HelpSec2","HelpSec3","HelpSec4","HelpSec5","HelpSec6"
        };
        private static readonly string[] _helpNavNames =
        {
            "HelpNav0","HelpNav1","HelpNav2","HelpNav3","HelpNav4","HelpNav5","HelpNav6"
        };

        private void HelpNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            int idx = int.Parse(btn.Tag.ToString()!);

            // Mostrar sección seleccionada, ocultar el resto
            for (int i = 0; i < _helpSecNames.Length; i++)
            {
                var sec = (FrameworkElement)FindName(_helpSecNames[i]);
                sec.Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
            }

            // Actualizar estilos del sidebar
            for (int i = 0; i < _helpNavNames.Length; i++)
            {
                var nav = (Button)FindName(_helpNavNames[i]);
                nav.Foreground  = i == idx ? _whiteBrush  : _mutedBrush;
                nav.BorderBrush = i == idx ? _accentBrush : _transpBrush;
            }
        }
    }
}