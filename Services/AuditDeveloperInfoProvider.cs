using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace EPFOptimizerPro.Services;

public static class AuditDeveloperInfoProvider
{
    public static string Build()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string assemblyVersion = assembly.GetName().Version?.ToString() ?? "Inconnue";
        string informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? assemblyVersion;

        string executablePath = Environment.ProcessPath ?? assembly.Location;
        string configuration = IsDebugBuild() ? "Debug" : "Release";
        string commonData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro");
        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EPFOptimizerPro");

        var builder = new StringBuilder();
        builder.AppendLine("INFORMATIONS DE L'APPLICATION");
        builder.AppendLine();
        builder.AppendLine($"Version produit       : {informationalVersion}");
        builder.AppendLine($"Version assembly      : {assemblyVersion}");
        builder.AppendLine($"Configuration         : {configuration}");
        builder.AppendLine($"Architecture processus: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"Architecture systeme  : {RuntimeInformation.OSArchitecture}");
        builder.AppendLine($"Runtime .NET          : {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"Systeme               : {RuntimeInformation.OSDescription}");
        builder.AppendLine();
        builder.AppendLine("CHEMINS TECHNIQUES");
        builder.AppendLine();
        builder.AppendLine($"Executable            : {executablePath}");
        builder.AppendLine($"Dossier application   : {AppContext.BaseDirectory}");
        builder.AppendLine($"ProgramData           : {commonData}");
        builder.AppendLine($"LocalAppData          : {localData}");
        builder.AppendLine();
        builder.AppendLine("CANAL DE MISE A JOUR");
        builder.AppendLine();
        builder.AppendLine("Canal                 : Stable");
        builder.AppendLine("Drafts                : ignores");
        builder.AppendLine("Prereleases           : ignorees");
        builder.AppendLine("Preview/Beta/Alpha/RC : ignores");
        builder.AppendLine("Diagnostic detaille   : consulter le journal temps reel apres une verification.");
        return builder.ToString().TrimEnd();
    }

    public static string GetPreferredLogFolder()
    {
        string commonData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EPFOptimizerPro");
        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EPFOptimizerPro");

        if (Directory.Exists(commonData)) return commonData;
        if (Directory.Exists(localData)) return localData;
        return AppContext.BaseDirectory;
    }

    public static void OpenLogFolder()
    {
        string folder = GetPreferredLogFolder();
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}