namespace EPFOptimizerPro.Services;

public enum CriticalWindowsServiceExpectation
{
    AlwaysRunning,
    TriggerStart,
    Conditional
}

public sealed record CriticalWindowsServiceDefinition(
    string ServiceName,
    string DisplayLabel,
    CriticalWindowsServiceExpectation Expectation);

public static class CriticalWindowsServiceCatalog
{
    private static readonly IReadOnlyList<CriticalWindowsServiceDefinition> Definitions =
        new List<CriticalWindowsServiceDefinition>
        {
            new("EventLog", "Journal des \u00E9v\u00E9nements Windows", CriticalWindowsServiceExpectation.AlwaysRunning),
            new("Schedule", "Planificateur de t\u00E2ches", CriticalWindowsServiceExpectation.AlwaysRunning),
            new("CryptSvc", "Services de chiffrement", CriticalWindowsServiceExpectation.AlwaysRunning),
            new("wuauserv", "Windows Update", CriticalWindowsServiceExpectation.TriggerStart),
            new("BITS", "Service de transfert intelligent", CriticalWindowsServiceExpectation.TriggerStart),
            new("WinDefend", "Microsoft Defender", CriticalWindowsServiceExpectation.Conditional)
        };

    public static IReadOnlyList<CriticalWindowsServiceDefinition> GetAll()
    {
        return Definitions;
    }
}
