using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public sealed class AdaptiveTaskStructuredResultStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AdaptiveTaskStructuredResult> _results =
        new(StringComparer.OrdinalIgnoreCase);

    public void Set(AdaptiveTaskStructuredResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrWhiteSpace(result.TaskName))
        {
            throw new ArgumentException(
                "Le nom de la tâche structurée ne peut pas être vide.",
                nameof(result));
        }

        lock (_sync)
        {
            _results[result.TaskName] = result;
        }
    }

    public bool TryGet(
        string taskName,
        out AdaptiveTaskStructuredResult? result)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            result = null;
            return false;
        }

        lock (_sync)
        {
            return _results.TryGetValue(taskName, out result);
        }
    }

    public bool Remove(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return false;
        }

        lock (_sync)
        {
            return _results.Remove(taskName);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _results.Clear();
        }
    }

    public IReadOnlyList<AdaptiveTaskStructuredResult> GetSnapshot()
    {
        lock (_sync)
        {
            return _results.Values
                .OrderBy(result => result.TaskName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}