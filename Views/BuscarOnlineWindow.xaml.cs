// =============================================================================
// Views/BuscarOnlineWindow.xaml.cs
// =============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReaperPluginManager.Views
{
    public partial class BuscarOnlineWindow : Window
    {
        public BuscarOnlineWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                BuscarPlugins();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e) => BuscarPlugins();

        private void BuscarPlugins()
        {
            var query = TxtBuscar.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            // Mostrar cargando
            PanelInicio.Visibility = Visibility.Collapsed;
            ScrollResultados.Visibility = Visibility.Collapsed;
            PanelCargando.Visibility = Visibility.Visible;
            TxtStatus.Text = $"Buscando \"{query}\"...";

            // Construir links según sitios seleccionados
            var sitios = new List<(string nombre, string url, string descripcion, string color)>();

            var q = Uri.EscapeDataString(query);

            if (ChkKVR.IsChecked == true)
                sitios.Add(("KVR Audio", $"https://www.kvraudio.com/search.php?q={q}&type=p&f=f&srl=3", "Base de datos más completa de plugins VST/AU. Incluye freeware y comerciales.", "#1565C0"));

            if (ChkVST4Free.IsChecked == true)
                sitios.Add(("VST4Free", $"https://vst4free.com/?search={q}", "Solo plugins gratuitos. Ideal para encontrar versiones free de calidad.", "#2E7D32"));

            if (ChkBPB.IsChecked == true)
                sitios.Add(("Bedroom Producers Blog", $"https://bedroomproducersblog.com/?s={q}+vst+plugin+free", "Blog de referencia para plugins gratis con reviews y tutoriales.", "#6A1B9A"));

            if (ChkPluginBoutique.IsChecked == true)
                sitios.Add(("Plugin Boutique", $"https://www.pluginboutique.com/search?search_query={q}", "Tienda con plugins comerciales. Frecuentes descuentos y bundles.", "#B71C1C"));

            if (ChkGearspace.IsChecked == true)
                sitios.Add(("Gearspace", $"https://gearspace.com/board/search.php?query={q}+vst+plugin", "Foro profesional con opiniones reales de productores y ingenieros.", "#E65100"));

            // Siempre incluir Google como respaldo
            sitios.Add(("Buscar en Google", $"https://www.google.com/search?q={q}+VST+plugin+free+download", "Búsqueda general — útil para encontrar la página oficial del plugin.", "#37474F"));

            // Renderizar resultados
            PanelResultados.Children.Clear();

            // Título
            var titulo = new TextBlock
            {
                Text = $"Resultados para: \"{query}\"",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 16)
            };
            PanelResultados.Children.Add(titulo);

            // Nota informativa
            var nota = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 40, 55)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var notaTexto = new TextBlock
            {
                Text = "Haz clic en cualquier sitio para abrirlo en tu navegador. Cuando encuentres el plugin, descarga el instalador y luego agrégalo aquí con el botón \"Agregar Plugin\".",
                Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            nota.Child = notaTexto;
            PanelResultados.Children.Add(nota);

            // Tarjeta por cada sitio
            foreach (var (nombre, url, descripcion, colorHex) in sitios)
            {
                var card = CrearTarjetaSitio(nombre, url, descripcion, colorHex);
                PanelResultados.Children.Add(card);
            }

            PanelCargando.Visibility = Visibility.Collapsed;
            ScrollResultados.Visibility = Visibility.Visible;
            TxtStatus.Text = $"Se encontraron {sitios.Count} sitios para buscar \"{query}\"";
        }

        private Border CrearTarjetaSitio(string nombre, string url, string descripcion, string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 12),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0, 0, 0, 0)
            };

            // Hover effect
            card.MouseEnter += (s, e) =>
                card.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            card.MouseLeave += (s, e) =>
                card.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            card.MouseLeftButtonUp += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Barra de color lateral
            var barra = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 14, 0)
            };
            Grid.SetColumn(barra, 0);

            // Info
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var txtNombre = new TextBlock
            {
                Text = nombre,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 3)
            };
            var txtDesc = new TextBlock
            {
                Text = descripcion,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
                TextWrapping = TextWrapping.Wrap
            };
            info.Children.Add(txtNombre);
            info.Children.Add(txtDesc);
            Grid.SetColumn(info, 1);

            // Icono de abrir
            var txtAbrir = new TextBlock
            {
                Text = "Abrir →",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 149, 200)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(txtAbrir, 2);

            grid.Children.Add(barra);
            grid.Children.Add(info);
            grid.Children.Add(txtAbrir);
            card.Child = grid;

            return card;
        }
    }
}
