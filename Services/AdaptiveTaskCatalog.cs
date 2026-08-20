using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class AdaptiveTaskCatalog
{
    private static readonly IReadOnlyList<AdaptiveTaskDefinition> Definitions =
        new List<AdaptiveTaskDefinition>
        {
            new(
                "Audit",
                "Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsBuildNumber | Format-List | Out-String",
                30,
                true,
                true),
            new(
                "Updates",
                "$session = New-Object -ComObject Microsoft.Update.Session; $searcher = $session.CreateUpdateSearcher(); $result = $searcher.Search('IsInstalled=0 and IsHidden=0'); 'Mises à jour disponibles : ' + $result.Updates.Count",
                240,
                true,
                true),
            new(
                "Temp User",
                "Get-ChildItem -Path $env:TEMP -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue",
                90,
                false,
                true),
            new(
                "Temp Win",
                "Get-ChildItem -Path 'C:\\Windows\\Temp' -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue",
                90,
                false,
                true),
            new(
                "Corbeille",
                "Clear-RecycleBin -Force -ErrorAction SilentlyContinue",
                60,
                false,
                true),
            new(
                "DNS",
                "ipconfig /flushdns",
                30,
                false,
                true),
            new(
                "Volumes",
                "Get-Volume | Where-Object DriveLetter | ForEach-Object { Optimize-Volume -DriveLetter $_.DriveLetter }",
                600,
                false,
                true),
            new(
                "SFC",
                "sfc /scannow",
                1200,
                false,
                true)
        };

    public static IReadOnlyList<AdaptiveTaskDefinition> GetDefinitions(bool optimize)
    {
        return Definitions
            .Where(definition => optimize
                ? definition.AvailableInOptimize
                : definition.AvailableInAudit)
            .ToList();
    }
}