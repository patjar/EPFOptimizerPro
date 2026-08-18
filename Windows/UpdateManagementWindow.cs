using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class UpdateManagementWindow : Window
{
    public UpdateManagementWindow(string name, string status, string progress, string message)
    {
        Title = "Gestion des mises a jour";
        Width = 900;
        Height = 520;
        MinWidth = 820;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel
        {
            Margin = new Thickness(18)
        };

        var title = new TextBlock
        {
            Text = "Gestion des mises a jour",
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

        AddSection(actions, "Windows Update", new[]
        {
            CreateButton("Voir les updates Windows", 210, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Details Windows Update", UpdateActionScriptService.BuildWindowsUpdateDetailsScript())),
            CreateButton("Installer les updates Windows", 230, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Installer les updates Windows", UpdateActionScriptService.BuildWindowsUpdateInstallScript()))
        });

        AddSection(actions, "Applications winget", new[]
        {
            CreateButton("Voir les updates applications", 230, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Updates applications winget", UpdateActionScriptService.BuildWingetAppsListScript())),
            CreateButton("Installer les updates applications", 260, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Installer updates applications winget", UpdateActionScriptService.BuildWingetAppsUpdateScript())),
            CreateButton("Résoudre updates inconnues", 240, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Résoudre updates inconnues winget", UpdateActionScriptService.BuildWingetUnknownAppsUpdateScript()))
        });

        AddSection(actions, "Microsoft Store", new[]
        {
            CreateButton("Ouvrir Microsoft Store", 220, (_, _) => UpdateActionScriptService.StartPowerShell(this, "Ouvrir Microsoft Store", UpdateActionScriptService.BuildMicrosoftStoreOpenScript()))
        });

        AddSection(actions, "Actions", new[]
        {
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
            "Organisation :" + Environment.NewLine +
            "- Windows Update : mises a jour systeme et Defender." + Environment.NewLine +
            "- Applications winget : applications classiques gerees par winget." + Environment.NewLine +
            "- Updates inconnues : utilise winget --include-unknown pour inclure les versions non determinees." + Environment.NewLine +
            "- Microsoft Store : ouverture du Store pour les mises a jour natives.";

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