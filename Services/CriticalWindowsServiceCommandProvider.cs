using System.Text;

namespace EPFOptimizerPro.Services;

public static class CriticalWindowsServiceCommandProvider
{
    public const string StructuredMarker = "EPF_STRUCTURED_CRITICAL_SERVICES";

    public static string BuildCommand()
    {
        var builder = new StringBuilder();
        builder.Append("'");
        builder.Append(StructuredMarker);
        builder.Append("'; ");

        foreach (CriticalWindowsServiceDefinition definition in CriticalWindowsServiceCatalog.GetAll())
        {
            string serviceName = EscapePowerShellSingleQuotedString(definition.ServiceName);
            string expectation = definition.Expectation.ToString();

            builder.Append("$service = Get-CimInstance Win32_Service -Filter \"Name='");
            builder.Append(serviceName);
            builder.Append("'\" -ErrorAction SilentlyContinue; ");
            builder.Append("if ($null -eq $service) { '");
            builder.Append(serviceName);
            builder.Append("|Missing|||" + expectation + "' } else { '");
            builder.Append(serviceName);
            builder.Append("|Present|' + $service.State + '|' + $service.StartMode + '|");
            builder.Append(expectation);
            builder.Append("' }; ");
        }

        return builder.ToString();
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
