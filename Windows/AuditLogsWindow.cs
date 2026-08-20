using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class AuditLogsWindow : Window
{
    private readonly ComboBox _logsList;
    private readonly TextBlock _metadata;
    private readonly TextBox _content;
    private IReadOnlyList<AuditLogFileInfo> _logs = Array.Empty<AuditLogFileInfo>();

    public AuditLogsWindow()
    {
        Title = "Journaux EPFOptimizerPro";
        Width = 920;
        Height = 650;
        MinWidth = 760;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { Margin = new Thickness(18) };

        var title = new TextBlock
        {
            Text = "Journaux EPFOptimizerPro",
            FontSize = 23,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var selectorPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        selectorPanel.Children.Add(new TextBlock
        {
            Text = "Journal sélectionné",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });

        _logsList = new ComboBox
        {
            MinHeight = 34,
            DisplayMemberPath = nameof(AuditLogFileInfo.DisplayName)
        };
        _logsList.SelectionChanged += (_, _) => DisplaySelectedLog();
        selectorPanel.Children.Add(_logsList);

        _metadata = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 7, 0, 0)
        };
        selectorPanel.Children.Add(_metadata);
        DockPanel.SetDock(selectorPanel, Dock.Top);
        root.Children.Add(selectorPanel);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.Children.Add(CreateButton("Actualiser", 110, (_, _) => RefreshLogs()));
        actions.Children.Add(CreateButton("Copier", 100, (_, _) => CopyContent()));
        actions.Children.Add(CreateButton("Ouvrir le fichier", 150, (_, _) => OpenSelectedFile()));
        actions.Children.Add(CreateButton("Ouvrir le dossier", 150, (_, _) => OpenSelectedFolder()));
        actions.Children.Add(CreateButton("Fermer", 110, (_, _) => Close()));
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        _content = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            Padding = new Thickness(12),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
        };
        root.Children.Add(_content);

        Content = root;
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        string? previousPath = (_logsList.SelectedItem as AuditLogFileInfo)?.FullPath;
        _logs = AuditLogDiscoveryService.FindLogs();
        _logsList.ItemsSource = _logs;

        if (_logs.Count == 0)
        {
            _logsList.IsEnabled = false;
            _metadata.Text = "Aucun journal EPFOptimizerPro disponible.";
            _content.Text =
                "Aucun fichier .log pertinent n'a été trouvé dans ProgramData, LocalAppData ou Temp." +
                Environment.NewLine + Environment.NewLine +
                "Les fichiers JSON, HTML, TXT et les données IA ne sont pas affichés ici.";
            return;
        }

        _logsList.IsEnabled = true;
        AuditLogFileInfo selected = _logs.FirstOrDefault(
            item => item.FullPath.Equals(previousPath, StringComparison.OrdinalIgnoreCase))
            ?? _logs[0];
        _logsList.SelectedItem = selected;
    }

    private void DisplaySelectedLog()
    {
        if (_logsList.SelectedItem is not AuditLogFileInfo log) return;

        _metadata.Text =
            $"Chemin : {log.FullPath}" + Environment.NewLine +
            $"Taille : {log.Length:N0} octets    Modifié le : {log.LastWriteTime:dd/MM/yyyy HH:mm:ss}";
        _content.Text = AuditLogDiscoveryService.ReadLog(log);
        _content.ScrollToEnd();
    }

    private void CopyContent()
    {
        if (!string.IsNullOrEmpty(_content.Text)) Clipboard.SetText(_content.Text);
    }

    private void OpenSelectedFile()
    {
        if (_logsList.SelectedItem is not AuditLogFileInfo log || !File.Exists(log.FullPath)) return;
        Process.Start(new ProcessStartInfo { FileName = log.FullPath, UseShellExecute = true });
    }

    private void OpenSelectedFolder()
    {
        if (_logsList.SelectedItem is not AuditLogFileInfo log) return;
        string? folder = Path.GetDirectoryName(log.FullPath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
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