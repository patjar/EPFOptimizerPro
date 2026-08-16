using System.Windows.Media;

namespace EPFOptimizerPro;

public static class UiBrushProvider
{
    public static SolidColorBrush FromHex(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }
}