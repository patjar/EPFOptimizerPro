using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public static class AdaptiveTaskStructuredResultParserRouter
{
    public static bool TryParse(
        string output,
        out AdaptiveTaskStructuredResult? result,
        out string? displayMessage)
    {
        result = null;
        displayMessage = null;

        if (SystemDiskStructuredResultParser.TryParse(output, out result) &&
            result is not null)
        {
            displayMessage = SystemDiskStructuredResultParser.BuildDisplayMessage(result);
            return true;
        }

        if (PendingRestartStructuredResultParser.TryParse(output, out result) &&
            result is not null)
        {
            displayMessage = PendingRestartStructuredResultParser.BuildDisplayMessage(result);
            return true;
        }

        if (DismCheckHealthStructuredResultParser.TryParse(output, out result) &&
            result is not null)
        {
            displayMessage = DismCheckHealthStructuredResultParser.BuildDisplayMessage(result);
            return true;
        }

        return false;
    }
}
