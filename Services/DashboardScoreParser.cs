using System.Text.RegularExpressions;

namespace EPFOptimizerPro;

public static class DashboardScoreParser
{
    public static int ExtractFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        Match averageMatch = Regex.Match(
            text,
            @"Score\s+moyen\s*:\s*(\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (averageMatch.Success && int.TryParse(averageMatch.Groups[1].Value, out int averageScore))
        {
            return Clamp(averageScore);
        }

        MatchCollection scoreMatches = Regex.Matches(
            text,
            @"\b(\d{1,3})\s*/\s*100\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (scoreMatches.Count > 0)
        {
            int total = 0;
            int count = 0;

            foreach (Match match in scoreMatches)
            {
                if (int.TryParse(match.Groups[1].Value, out int value))
                {
                    total += Clamp(value);
                    count++;
                }
            }

            if (count > 0)
            {
                return Clamp((int)Math.Round(total / (double)count, MidpointRounding.AwayFromZero));
            }
        }

        Match healthMatch = Regex.Match(
            text,
            @"Sant[eÃ©]\s+IA\s*:\s*(\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (healthMatch.Success && int.TryParse(healthMatch.Groups[1].Value, out int healthScore))
        {
            return Clamp(healthScore);
        }

        return 0;
    }

    public static int Clamp(int score)
    {
        if (score < 0) return 0;
        if (score > 100) return 100;
        return score;
    }
}