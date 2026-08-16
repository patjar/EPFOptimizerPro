using System.Security.Principal;

namespace EPFOptimizerPro;

public static class SystemPrivilegeService
{
    public static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}