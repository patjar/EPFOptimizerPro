using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class AuditManagementWindow : Window
{
    private readonly IEnumerable<object> _completedTasks;

    public AuditManagementWindow(string name, string status, string progress, string message, IEnumerable<object> completedTasks)
    {
        _completedTasks = completedTasks;

        Title = "Gestion des audits";
        Width = 820;
        Height = 430;
        MinWidth = 760;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel
        {
            Margin = new Thickness(18)
        };

        var title = new TextBlock
        {
            Text = "Gestion des audits",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var actions = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 12, 0, 0)
        };
        DockPanel.SetDock(actions, Dock.Bottom);

        AddSection(actions, "Audit", new[]
        {
            CreateButton("Afficher les erreurs", 220, (_, _) => ShowErrors()),
            CreateButton("Fermer", 120, (_, _) => Close())
        });

        root.Children.Add(actions);

        string body =
            "Tache : " + name + Environment.NewLine +
            "Statut : " + status + Environment.NewLine +
            "Progression : " + progress + " %" + Environment.NewLine +
            Environment.NewLine +
            "Resultat actuel de l'audit :" + Environment.NewLine +
            (string.IsNullOrWhiteSpace(message) ? "Aucun detail disponible." : message) + Environment.NewLine +
            Environment.NewLine +
            "Actions disponibles :" + Environment.NewLine +
            "- Afficher les erreurs : affiche les erreurs et avertissements detectes par l'audit.";

        var text = new TextBox
        {
            Text = body,
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

    private void ShowErrors()
    {
        IReadOnlyList<AuditProblemSummary> problems = AuditProblemsFilterService.GetErrors(_completedTasks);
        var window = new AuditProblemsWindow(problems)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private static void AddSection(Panel parent, string title, IEnumerable<Button> buttons)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 4)
        });

        var row = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 2)
        };

        foreach (Button button in buttons)
        {
            row.Children.Add(button);
        }

        parent.Children.Add(row);
    }

    private static Button CreateButton(string content, double minWidth, RoutedEventHandler clickHandler)
    {
        var button = new Button
        {
            Content = content,
            MinWidth = minWidth,
            Margin = new Thickness(0, 0, 10, 6),
            Padding = new Thickness(12, 8, 12, 8)
        };

        button.Click += clickHandler;
        return button;
    }
}