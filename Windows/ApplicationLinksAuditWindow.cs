using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro.Windows;

public sealed class ApplicationLinksAuditWindow : Window
{
    private readonly ListBox _resultsList;
    private readonly TextBlock _summary;
    private readonly TextBox _details;
    private readonly Button _actionButton;
    private IReadOnlyList<ApplicationLinkCheckResult> _results = Array.Empty<ApplicationLinkCheckResult>();

    public ApplicationLinksAuditWindow()
    {
        Title = "Liens et lanceurs de l'application";
        Width = 980;
        Height = 680;
        MinWidth = 820;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { Margin = new Thickness(18) };
        var title = new TextBlock
        {
            Text = "Liens et lanceurs de l'application",
            FontSize = 23,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        _summary = new TextBlock
        {
            Text = "Cliquez sur Vérifier tout. Aucune cible ne sera ouverte.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(_summary, Dock.Top);
        root.Children.Add(_summary);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.Children.Add(CreateButton("Vérifier tout", 130, async (_, _) => await RefreshAsync()));
        _actionButton = CreateButton("Action indisponible", 220, async (_, _) => await ExecuteSelectedActionAsync());
        _actionButton.IsEnabled = false;
        actions.Children.Add(_actionButton);
        actions.Children.Add(CreateButton("Copier le rapport", 160, (_, _) => CopyReport()));
        actions.Children.Add(CreateButton("Fermer", 110, (_, _) => Close()));
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _resultsList = new ListBox
        {
            DisplayMemberPath = nameof(ApplicationLinkCheckResult.DisplayName)
        };
        _resultsList.SelectionChanged += (_, _) => ShowSelected();
        Grid.SetColumn(_resultsList, 0);
        grid.Children.Add(_resultsList);

        _details = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            Padding = new Thickness(12)
        };
        Grid.SetColumn(_details, 2);
        grid.Children.Add(_details);
        root.Children.Add(grid);
        Content = root;
    }

    private async Task RefreshAsync()
    {
        _summary.Text = "Vérification en cours...";
        _details.Text = string.Empty;
        _actionButton.IsEnabled = false;

        try
        {
            _results = await ApplicationLinksAuditService.CheckAllAsync();
            _resultsList.ItemsSource = _results;
            UpdateSummary();
            if (_results.Count > 0) _resultsList.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _summary.Text = "La vérification a échoué.";
            _details.Text = ex.Message;
        }
    }

    private void UpdateSummary()
    {
        int ok = _results.Count(result => result.Status == ApplicationLinkStatus.Ok);
        int warning = _results.Count(result => result.Status == ApplicationLinkStatus.Warning);
        int error = _results.Count(result => result.Status == ApplicationLinkStatus.Error);
        int ignored = _results.Count(result => result.Status == ApplicationLinkStatus.Ignored);
        _summary.Text =
            $"{_results.Count} cible(s) : {ok} OK, {warning} attention, {error} erreur(s), {ignored} ignorée(s). Aucune cible ouverte.";
    }

    private void ShowSelected()
    {
        if (_resultsList.SelectedItem is not ApplicationLinkCheckResult result)
        {
            _actionButton.Content = "Action indisponible";
            _actionButton.IsEnabled = false;
            return;
        }

        _actionButton.Content = result.ActionLabel;
        _actionButton.IsEnabled = result.Target.Action != ApplicationLinkAction.None;
        _details.Text =
            $"État   : {result.StatusLabel}" + Environment.NewLine +
            $"Nom    : {result.Target.Name}" + Environment.NewLine +
            $"Type   : {result.Target.Kind}" + Environment.NewLine +
            $"Cible  : {result.Target.Target}" + Environment.NewLine +
            $"Source : {result.Target.Source}" + Environment.NewLine +
            $"Action : {result.ActionLabel}" + Environment.NewLine +
            $"Vérifié le : {result.CheckedAt:dd/MM/yyyy HH:mm:ss}" + Environment.NewLine + Environment.NewLine +
            result.Details;
    }

    private async Task ExecuteSelectedActionAsync()
    {
        if (_resultsList.SelectedItem is not ApplicationLinkCheckResult result) return;

        if (result.Target.Action == ApplicationLinkAction.RetestHttp)
        {
            _actionButton.IsEnabled = false;
            _details.Text = "Requête HTTP interne en cours...";
            ApplicationLinkCheckResult refreshed = await ApplicationLinksAuditService.RetestAsync(result.Target);
            int index = _results.ToList().IndexOf(result);
            var updated = _results.ToList();
            if (index >= 0) updated[index] = refreshed;
            _results = updated;
            _resultsList.ItemsSource = null;
            _resultsList.ItemsSource = _results;
            _resultsList.SelectedIndex = Math.Max(0, index);
            UpdateSummary();
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            result.ActionLabel + " ?" + Environment.NewLine + Environment.NewLine + result.Target.Target,
            "Liens et lanceurs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            ApplicationLinksAuditService.ExecuteAction(result.Target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Test de la cible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyReport()
    {
        if (_results.Count > 0)
        {
            Clipboard.SetText(ApplicationLinksAuditService.FormatReport(_results));
        }
    }

    private static Button CreateButton(string content, double width, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = content,
            MinWidth = width,
            Margin = new Thickness(0, 0, 10, 6),
            Padding = new Thickness(12, 8, 12, 8)
        };
        button.Click += handler;
        return button;
    }
}