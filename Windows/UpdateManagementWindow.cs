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
            Text = "Gestion des mises a jour",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var btnWindowsDetails = CreateButton("Voir les updates Windows", 180);
        btnWindowsDetails.Click += (_, _) => UpdateActionScriptService.StartPowerShell(this, "Details Windows Update", UpdateActionScriptService.BuildWindowsUpdateDetailsScript());
        buttons.Children.Add(btnWindowsDetails);

        var btnWindowsInstall = CreateButton("Installer Windows Update", 190);
        btnWindowsInstall.Click += (_, _) => UpdateActionScriptService.StartPowerShell(this, "Installer Windows Update", UpdateActionScriptService.BuildWindowsUpdateInstallScript());
        buttons.Children.Add(btnWindowsInstall);

        var btnStoreUpdate = CreateButton("Lancer update Microsoft Store", 220);
        btnStoreUpdate.Click += (_, _) => UpdateActionScriptService.StartPowerShell(this, "Update Microsoft Store", UpdateActionScriptService.BuildMicrosoftStoreUpdateScript());
        buttons.Children.Add(btnStoreUpdate);

        var btnClose = CreateButton("Fermer", 100);
        btnClose.Click += (_, _) => Close();
        buttons.Children.Add(btnClose);

        root.Children.Add(buttons);

        string body =
            "Tache : " + name + Environment.NewLine +
            "Statut : " + status + Environment.NewLine +
            "Progression : " + progress + " %" + Environment.NewLine +
            Environment.NewLine +
            "Resultat actuel de l'audit :" + Environment.NewLine +
            (string.IsNullOrWhiteSpace(message) ? "Aucun detail disponible." : message) + Environment.NewLine +
            Environment.NewLine +
            "Actions disponibles :" + Environment.NewLine +
            "- Voir les updates Windows : ouvre une console avec le detail des mises a jour detectees." + Environment.NewLine +
            "- Installer Windows Update : demande confirmation puis installe les updates Windows detectees." + Environment.NewLine +
            "- Lancer update Microsoft Store : lance winget upgrade --all --source msstore.";

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

    private static Button CreateButton(string content, double minWidth)
    {
        return new Button
        {
            Content = content,
            MinWidth = minWidth,
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(12, 8, 12, 8)
        };
    }
}