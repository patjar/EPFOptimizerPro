using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class AuditProblemsWindow : Window
{
    public AuditProblemsWindow(IReadOnlyList<AuditProblemSummary> problems)
    {
        Title = "Erreurs d'audit";
        Width = 760;
        Height = 520;
        MinWidth = 680;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel
        {
            Margin = new Thickness(18)
        };

        var title = new TextBlock
        {
            Text = problems.Count == 0 ? "Aucune erreur détectée" : $"Erreurs d'audit ({problems.Count})",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        DockPanel.SetDock(actions, Dock.Bottom);

        var close = new Button
        {
            Content = "Fermer",
            MinWidth = 120,
            Padding = new Thickness(12, 8, 12, 8)
        };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        root.Children.Add(actions);

        var text = new TextBox
        {
            Text = AuditProblemsSummaryProvider.Format(problems),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(10)
        };

        root.Children.Add(text);
        Content = root;
    }
}