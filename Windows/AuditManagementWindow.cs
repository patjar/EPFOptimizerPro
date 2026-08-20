using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class AuditManagementWindow : Window
{
    private readonly string _name;
    private readonly string _status;
    private readonly string _progress;
    private readonly string _message;
    private readonly IEnumerable<object> _completedTasks;
    private readonly TextBox _summaryText;
    private readonly TextBox _problemsText;
    private readonly TextBox _developerText;
    private readonly TextBox _deadCodeText;

    public AuditManagementWindow(
        string name,
        string status,
        string progress,
        string message,
        IEnumerable<object> completedTasks)
    {
        _name = name;
        _status = status;
        _progress = progress;
        _message = message;
        _completedTasks = completedTasks;

        Title = "Gestion des audits";
        Width = 900;
        Height = 620;
        MinWidth = 780;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { Margin = new Thickness(18) };

        var header = new TextBlock
        {
            Text = "Gestion des audits",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        _summaryText = CreateReadOnlyTextBox();
        _problemsText = CreateReadOnlyTextBox();
        _developerText = CreateReadOnlyTextBox();
        _deadCodeText = CreateReadOnlyTextBox();

        var tabs = new TabControl();
        tabs.Items.Add(CreateTab("Résumé", _summaryText));
        tabs.Items.Add(CreateTab("Problèmes", BuildProblemsPanel()));
        tabs.Items.Add(CreateTab("Développeur", BuildDeveloperPanel()));
        tabs.Items.Add(CreateTab("Code mort [Expérimental]", BuildDeadCodePanel()));
        root.Children.Add(tabs);

        Content = root;
        RefreshContent();
    }

    private UIElement BuildFooter()
    {
        var row = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        row.Children.Add(CreateButton("Copier le résumé", 160, (_, _) => CopySummary()));
        row.Children.Add(CreateButton("Actualiser", 120, (_, _) => RefreshContent()));
        row.Children.Add(CreateButton("Fermer", 120, (_, _) => Close()));
        return row;
    }

    private UIElement BuildProblemsPanel()
    {
        var panel = new DockPanel();
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        actions.Children.Add(CreateButton("Afficher les erreurs", 190, (_, _) => ShowErrors()));
        DockPanel.SetDock(actions, Dock.Top);
        panel.Children.Add(actions);
        panel.Children.Add(_problemsText);
        return panel;
    }

    private UIElement BuildDeveloperPanel()
    {
        var panel = new DockPanel();
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        actions.Children.Add(CreateButton("Voir les journaux", 160, (_, _) => OpenLogs()));
        actions.Children.Add(CreateButton("Copier les infos techniques", 210, (_, _) => CopyDeveloperInfo()));
        actions.Children.Add(CreateButton("Tester le canal de mise à jour", 220, async (_, _) => await TestUpdateChannelAsync()));
        actions.Children.Add(CreateButton("Vérifier la cohérence des versions", 240, (_, _) => CheckVersionConsistency()));
        actions.Children.Add(CreateButton("Vérifier le dernier MSI", 190, async (_, _) => await CheckMsiSignatureAsync()));
        actions.Children.Add(CreateButton("Vérifier le dépôt Git", 180, (_, _) => CheckGitRepositoryHealth()));
        DockPanel.SetDock(actions, Dock.Top);
        panel.Children.Add(actions);
        panel.Children.Add(_developerText);
        return panel;
    }

    private UIElement BuildDeadCodePanel()
    {
        var panel = new DockPanel();
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        actions.Children.Add(CreateButton("Analyser le projet", 170, (_, _) => ScanDeadCode()));
        actions.Children.Add(CreateButton("Copier les résultats", 180, (_, _) => CopyDeadCodeResults()));
        DockPanel.SetDock(actions, Dock.Top);
        panel.Children.Add(actions);
        _deadCodeText.Text = AuditDeadCodeInfoProvider.Build();
        panel.Children.Add(_deadCodeText);
        return panel;
    }

    private void ScanDeadCode()
    {
        string projectRoot = FindProjectRoot();
        _deadCodeText.Text = AuditDeadCodeScannerService.Scan(projectRoot);
    }

    private void CopyDeadCodeResults()
    {
        Clipboard.SetText(_deadCodeText.Text);
    }

    private static string FindProjectRoot()
    {
        string current = AppContext.BaseDirectory;
        DirectoryInfo? directory = new DirectoryInfo(current);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EPFOptimizerPro.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Environment.CurrentDirectory;
    }

    private void RefreshContent()
    {
        IReadOnlyList<AuditProblemSummary> problems =
            AuditProblemsFilterService.GetErrors(_completedTasks);

        _summaryText.Text = AuditManagementSummaryProvider.Build(
            _name, _status, _progress, _message, problems);
        _problemsText.Text = AuditProblemsSummaryProvider.Format(problems);
        _developerText.Text = AuditDeveloperInfoProvider.Build();
    }

    private void ShowErrors()
    {
        IReadOnlyList<AuditProblemSummary> problems =
            AuditProblemsFilterService.GetErrors(_completedTasks);
        var window = new AuditProblemsWindow(problems) { Owner = this };
        window.ShowDialog();
    }

    private void CopySummary()
    {
        Clipboard.SetText(_summaryText.Text);
    }

    private void CopyDeveloperInfo()
    {
        Clipboard.SetText(_developerText.Text);
    }

    private void CheckGitRepositoryHealth()
    {
        try
        {
            _developerText.Text = AuditGitRepositoryHealthService.BuildReport();
        }
        catch (Exception ex)
        {
            _developerText.Text = "Erreur pendant le contrôle du dépôt Git :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }
    private async Task CheckMsiSignatureAsync()
    {
        _developerText.Text = "Vérification du dernier MSI en cours...";
        try
        {
            _developerText.Text = await AuditMsiSignatureService.BuildReportAsync();
        }
        catch (Exception ex)
        {
            _developerText.Text = "Erreur pendant la vérification du MSI :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }
    private void CheckVersionConsistency()
    {
        try
        {
            _developerText.Text = AuditVersionConsistencyService.BuildReport();
        }
        catch (Exception ex)
        {
            _developerText.Text = "Erreur pendant le contrôle de cohérence des versions :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }
    private async Task TestUpdateChannelAsync()
    {
        _developerText.Text = "Diagnostic du canal de mise à jour en cours...";

        try
        {
            _developerText.Text = await AuditUpdateChannelDiagnosticService.BuildReportAsync();
        }
        catch (Exception ex)
        {
            _developerText.Text = "Erreur pendant le diagnostic du canal de mise à jour :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }
    private void OpenLogs()
    {
        var window = new AuditLogsWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private static TabItem CreateTab(string header, UIElement content)
    {
        return new TabItem
        {
            Header = header,
            Content = content,
            Padding = new Thickness(14, 7, 14, 7)
        };
    }

    private static TextBox CreateReadOnlyTextBox()
    {
        return new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            Background = Brushes.White
        };
    }

    private static Button CreateButton(
        string content,
        double minWidth,
        RoutedEventHandler clickHandler)
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