using System.Reflection;

namespace EPFOptimizerPro;

public static class ApplicationVersionProvider
{
    public static string GetDisplayVersion()
    {
        string? informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int plusIndex = informationalVersion.IndexOf('+');
            if (plusIndex > 0)
            {
                informationalVersion = informationalVersion.Substring(0, plusIndex);
            }

            return informationalVersion;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}