using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using EPFOptimizerPro.Services;
using EPFOptimizerPro.Models;

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
    private readonly TabControl _tabs;
    private readonly Dictionary<string, AuditDashboardCardModel> _dashboardModels = new();
    private UniformGrid? _dashboardCards;
    private bool _isDashboardRunning;
    private Border? _dashboardGlobalBanner;
    private TextBlock? _dashboardGlobalTitle;
    private TextBlock? _dashboardGlobalSummary;

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
        Width = 980;
        Height = 700;
        MinWidth = 840;
        MinHeight = 560;
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

        _tabs = new TabControl();
        _tabs.Items.Add(CreateTab("Tableau de bord", BuildDashboardPanel()));
        _tabs.Items.Add(CreateTab("Résumé", _summaryText));
        _tabs.Items.Add(CreateTab("Problèmes", BuildProblemsPanel()));
        _tabs.Items.Add(CreateTab("Développeur", BuildDeveloperPanel()));
        _tabs.Items.Add(CreateTab("Code mort [Expérimental]", BuildDeadCodePanel()));
        root.Children.Add(_tabs);

        Content = root;
        RefreshContent();
    }

    private UIElement BuildDashboardPanel()
    {
        var page = new DockPanel
        {
            Margin = new Thickness(4)
        };

        var intro = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 9)
        };
        intro.Children.Add(new TextBlock
        {
            Text = "Tableau de bord des contrôles",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        });
        intro.Children.Add(new TextBlock
        {
            Text = "V8.2 : lancez tous les contrôles en lecture seule ou utilisez Détails pour un contrôle ciblé.",
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            TextWrapping = TextWrapping.Wrap
        });
        intro.Children.Add(BuildDashboardGlobalBanner());
        var dashboardActions = new WrapPanel
        {
            Margin = new Thickness(0, 10, 0, 0)
        };
        dashboardActions.Children.Add(CreateButton(
            "Tout vérifier",
            150,
            async (_, _) => await RunAllDashboardChecksAsync()));
        intro.Children.Add(dashboardActions);

        DockPanel.SetDock(intro, Dock.Top);
        page.Children.Add(intro);

        InitializeDashboardModels();
        _dashboardCards = new UniformGrid
        {
            Columns = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            MaxWidth = 960
        };
        RefreshDashboardCards();

        var cardsHost = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        cardsHost.Children.Add(_dashboardCards);
        page.Children.Add(cardsHost);
        return page;
    }

    private async Task RunAllDashboardChecksAsync()
    {
        if (_isDashboardRunning) return;
        _isDashboardRunning = true;
        UpdateDashboardGlobalVerdict();

        try
        {
            IReadOnlyList<AuditProblemSummary> problems =
                AuditProblemsFilterService.GetErrors(_completedTasks);
            SetDashboardModel(AuditDashboardStatusInterpreter.FromSystemAudit(problems.Count));

            SetDashboardModel(AuditDashboardStatusInterpreter.Running(
                "updates", "Canal stable", "Release GitHub et mises à jour"));
            try
            {
                string updateReport = await AuditUpdateChannelDiagnosticService.BuildReportAsync();
                _developerText.Text = updateReport;
                SetDashboardModel(AuditDashboardStatusInterpreter.FromUpdateChannel(updateReport));
            }
            catch (Exception ex)
            {
                SetDashboardModel(AuditDashboardStatusInterpreter.Failed(
                    "updates", "Canal stable", "Release GitHub et mises à jour", ex.Message));
            }

            SetDashboardModel(AuditDashboardStatusInterpreter.Running(
                "versions", "Versions", "Projet, assembly, EXE et MSI"));
            try
            {
                string versionsReport = AuditVersionConsistencyService.BuildReport();
                _developerText.Text = versionsReport;
                SetDashboardModel(AuditDashboardStatusInterpreter.FromVersions(versionsReport));
            }
            catch (Exception ex)
            {
                SetDashboardModel(AuditDashboardStatusInterpreter.Failed(
                    "versions", "Versions", "Projet, assembly, EXE et MSI", ex.Message));
            }

            SetDashboardModel(AuditDashboardStatusInterpreter.Running(
                "msi", "MSI et signature", "Authenticode et préparation publication"));
            try
            {
                string msiReport = await AuditMsiSignatureService.BuildReportAsync();
                _developerText.Text = msiReport;
                SetDashboardModel(AuditDashboardStatusInterpreter.FromMsi(msiReport));
            }
            catch (Exception ex)
            {
                SetDashboardModel(AuditDashboardStatusInterpreter.Failed(
                    "msi", "MSI et signature", "Authenticode et préparation publication", ex.Message));
            }

            SetDashboardModel(AuditDashboardStatusInterpreter.Running(
                "git", "Dépôt Git", "Branche, synchronisation et working tree"));
            try
            {
                string gitReport = AuditGitRepositoryHealthService.BuildReport();
                _developerText.Text = gitReport;
                SetDashboardModel(AuditDashboardStatusInterpreter.FromGit(gitReport));
            }
            catch (Exception ex)
            {
                SetDashboardModel(AuditDashboardStatusInterpreter.Failed(
                    "git", "Dépôt Git", "Branche, synchronisation et working tree", ex.Message));
            }

            SetDashboardModel(AuditDashboardStatusInterpreter.Running(
                "deadcode", "Code mort", "Analyse conservatrice en lecture seule"));
            try
            {
                string deadCodeReport = AuditDeadCodeScannerService.Scan(FindProjectRoot());
                _deadCodeText.Text = deadCodeReport;
                SetDashboardModel(AuditDashboardStatusInterpreter.FromDeadCode(deadCodeReport));
            }
            catch (Exception ex)
            {
                SetDashboardModel(AuditDashboardStatusInterpreter.Failed(
                    "deadcode", "Code mort", "Analyse conservatrice en lecture seule", ex.Message));
            }
        }
        finally
        {
            _isDashboardRunning = false;
            UpdateDashboardGlobalVerdict();
        }
    }
    private UIElement BuildDashboardGlobalBanner()
    {
        _dashboardGlobalTitle = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold
        };
        _dashboardGlobalSummary = new TextBlock
        {
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel();
        content.Children.Add(_dashboardGlobalTitle);
        content.Children.Add(_dashboardGlobalSummary);

        _dashboardGlobalBanner = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            Child = content
        };
        UpdateDashboardGlobalVerdict();
        return _dashboardGlobalBanner;
    }

    private void UpdateDashboardGlobalVerdict()
    {
        if (_dashboardGlobalBanner is null ||
            _dashboardGlobalTitle is null ||
            _dashboardGlobalSummary is null) return;

        AuditDashboardGlobalVerdict verdict = AuditDashboardGlobalVerdictService.Build(
            _dashboardModels.Values,
            _isDashboardRunning);

        var accent = new SolidColorBrush(verdict.Accent);
        _dashboardGlobalBanner.Background = new SolidColorBrush(verdict.Background);
        _dashboardGlobalBanner.BorderBrush = accent;
        _dashboardGlobalTitle.Foreground = accent;
        _dashboardGlobalSummary.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        _dashboardGlobalTitle.Text = verdict.Title;
        _dashboardGlobalSummary.Text = verdict.Summary;
    }
    private void InitializeDashboardModels()
    {
        if (_dashboardModels.Count > 0) return;

        _dashboardModels["system"] = AuditDashboardStatusInterpreter.NotRun(
            "system", "Audit système", "Résumé et problèmes détectés");
        _dashboardModels["updates"] = AuditDashboardStatusInterpreter.NotRun(
            "updates", "Canal stable", "Release GitHub et mises à jour");
        _dashboardModels["versions"] = AuditDashboardStatusInterpreter.NotRun(
            "versions", "Versions", "Projet, assembly, EXE et MSI");
        _dashboardModels["msi"] = AuditDashboardStatusInterpreter.NotRun(
            "msi", "MSI et signature", "Authenticode et préparation publication");
        _dashboardModels["git"] = AuditDashboardStatusInterpreter.NotRun(
            "git", "Dépôt Git", "Branche, synchronisation et working tree");
        _dashboardModels["deadcode"] = AuditDashboardStatusInterpreter.NotRun(
            "deadcode", "Code mort", "Analyse conservatrice en lecture seule");
    }

    private void SetDashboardModel(AuditDashboardCardModel model)
    {
        _dashboardModels[model.Id] = model;
        RefreshDashboardCards();
    }

    private void RefreshDashboardCards()
    {
        if (_dashboardCards is null) return;
        _dashboardCards.Children.Clear();
        AddDashboardCard("system", 1);
        AddDashboardCard("updates", 3);
        AddDashboardCard("versions", 3);
        AddDashboardCard("msi", 3);
        AddDashboardCard("git", 3);
        AddDashboardCard("deadcode", 4);
        UpdateDashboardGlobalVerdict();
    }

    private void AddDashboardCard(string id, int targetTabIndex)
    {
        if (_dashboardCards is null || !_dashboardModels.TryGetValue(id, out AuditDashboardCardModel? model)) return;
        _dashboardCards.Children.Add(AuditDashboardCardFactory.Create(
            model,
            (_, _) => _tabs.SelectedIndex = targetTabIndex));
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
        actions.Children.Add(CreateButton("Exporter le rapport complet", 220, async (_, _) => await ExportFullAuditReportAsync()));
        actions.Children.Add(CreateButton("Vérifier les liens et lanceurs", 220, (_, _) => OpenApplicationLinksAudit()));
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
        SetDashboardModel(AuditDashboardStatusInterpreter.FromDeadCode(_deadCodeText.Text));
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
        SetDashboardModel(AuditDashboardStatusInterpreter.FromSystemAudit(problems.Count));
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

    private void OpenApplicationLinksAudit()
    {
        var window = new ApplicationLinksAuditWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }
    private async Task ExportFullAuditReportAsync()
    {
        _developerText.Text = "Export du rapport complet en cours...";

        try
        {
            string path = await AuditFullReportExporter.ExportAsync(
                _name, _status, _progress, _message, _completedTasks);

            _developerText.Text =
                "Rapport d'audit complet exporte." + Environment.NewLine + Environment.NewLine +
                "Chemin : " + path + Environment.NewLine + Environment.NewLine +
                "Le rapport regroupe le resume, les problemes, les informations developpeur, " +
                "le code mort, le canal de mise a jour, les versions, le MSI et l'etat Git.";

            MessageBoxResult result = MessageBox.Show(
                "Le rapport a ete cree :" + Environment.NewLine + Environment.NewLine + path +
                Environment.NewLine + Environment.NewLine + "Ouvrir le rapport maintenant ?",
                "Rapport d'audit EPFOptimizerPro",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                AuditFullReportExporter.OpenReport(path);
            }
        }
        catch (Exception ex)
        {
            _developerText.Text = "Erreur pendant l'export du rapport complet :" +
                Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }
    private void CheckGitRepositoryHealth()
    {
        try
        {
            _developerText.Text = AuditGitRepositoryHealthService.BuildReport();
            SetDashboardModel(AuditDashboardStatusInterpreter.FromGit(_developerText.Text));
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
            SetDashboardModel(AuditDashboardStatusInterpreter.FromMsi(_developerText.Text));
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
            SetDashboardModel(AuditDashboardStatusInterpreter.FromVersions(_developerText.Text));
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
            SetDashboardModel(AuditDashboardStatusInterpreter.FromUpdateChannel(_developerText.Text));
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