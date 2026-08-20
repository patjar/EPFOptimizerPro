using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EPFOptimizerPro.Windows;

public sealed class AuditDashboardDetailWindow : Window
{
    private readonly TextBox _reportText;
    private readonly Func<Task<string>> _refreshAction;

    public AuditDashboardDetailWindow(
        string title,
        string report,
        Func<Task<string>> refreshAction)
    {
        _refreshAction = refreshAction;
        Title = title;
        Width = 900;
        Height = 650;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { Margin = new Thickness(18) };

        var header = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.Children.Add(CreateButton("Copier", 100, (_, _) => CopyReport()));
        actions.Children.Add(CreateButton("Actualiser ce contrôle", 175, async (_, _) => await RefreshAsync()));
        actions.Children.Add(CreateButton("Fermer", 110, (_, _) => Close()));
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        _reportText = new TextBox
        {
            Text = report,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
        };
        root.Children.Add(_reportText);
        Content = root;
    }

    private async Task RefreshAsync()
    {
        _reportText.Text = "Vérification en cours...";
        try
        {
            _reportText.Text = await _refreshAction();
        }
        catch (Exception ex)
        {
            _reportText.Text = "Erreur pendant la vérification :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }

    private void CopyReport()
    {
        if (!string.IsNullOrWhiteSpace(_reportText.Text))
            Clipboard.SetText(_reportText.Text);
    }

    private static Button CreateButton(string content, double width, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = content,
            MinWidth = width,
            Margin = new Thickness(0, 0, 10, 6),
            Padding = new Thickness(12, 7, 12, 7)
        };
        button.Click += handler;
        return button;
    }
}