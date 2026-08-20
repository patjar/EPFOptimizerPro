namespace EPFOptimizerPro.Models;

public enum AuditDashboardStatus
{
    NotRun,
    Running,
    Success,
    Warning,
    Error
}

public sealed record AuditDashboardCardModel(
    string Id,
    string Title,
    string Subtitle,
    AuditDashboardStatus Status,
    string StatusText,
    string DetailText);