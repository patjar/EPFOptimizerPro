using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EPFOptimizerPro.Models;

namespace EPFOptimizerPro.Services;

public static class AuditDashboardCardFactory
{
    public static Border Create(
        AuditDashboardCardModel model,
        RoutedEventHandler detailsHandler)
    {
        Color accent = GetAccent(model.Status);

        var card = new Border
        {
            Tag = model.Id,
            Margin = new Thickness(7),
            Padding = new Thickness(0),
            Height = 178,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            Background = Brushes.White
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var accentBar = new Border
        {
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(12, 12, 0, 0)
        };
        Grid.SetRow(accentBar, 0);
        layout.Children.Add(accentBar);

        var body = new Grid
        {
            Margin = new Thickness(16, 11, 16, 10)
        };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = model.Title,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0);
        heading.Children.Add(title);

        var details = new Button
        {
            Content = "Détails",
            MinWidth = 72,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Ouvrir le contrôle détaillé"
        };
        details.Click += detailsHandler;
        Grid.SetColumn(details, 1);
        heading.Children.Add(details);

        Grid.SetRow(heading, 0);
        body.Children.Add(heading);

        var subtitle = new TextBlock
        {
            Text = model.Subtitle,
            Margin = new Thickness(0, 3, 0, 7),
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 34
        };
        Grid.SetRow(subtitle, 1);
        body.Children.Add(subtitle);

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(9, 3, 9, 3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        badge.Child = new TextBlock
        {
            Text = model.StatusText,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(accent)
        };
        Grid.SetRow(badge, 2);
        body.Children.Add(badge);

        var detail = new TextBlock
        {
            Text = model.DetailText,
            Margin = new Thickness(0, 7, 0, 5),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(detail, 3);
        body.Children.Add(detail);


        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        card.Child = layout;
        return card;
    }

    private static Color GetAccent(AuditDashboardStatus status)
    {
        return status switch
        {
            AuditDashboardStatus.Running => Color.FromRgb(37, 99, 235),
            AuditDashboardStatus.Success => Color.FromRgb(22, 163, 74),
            AuditDashboardStatus.Warning => Color.FromRgb(217, 119, 6),
            AuditDashboardStatus.Error => Color.FromRgb(220, 38, 38),
            _ => Color.FromRgb(100, 116, 139)
        };
    }
}