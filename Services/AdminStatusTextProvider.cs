namespace EPFOptimizerPro;

public static class AdminStatusTextProvider
{
    public const string AdminYes = "Admin : oui";
    public const string AdminNo = "Admin : non";

    public static string Format(bool isAdmin)
    {
        return isAdmin ? AdminYes : AdminNo;
    }
}