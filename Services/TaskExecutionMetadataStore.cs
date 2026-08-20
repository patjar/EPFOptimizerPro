using EPFOptimizerPro.Services.Models;

namespace EPFOptimizerPro.Services;

public sealed class TaskExecutionMetadataStore
{
    private sealed class MutableMetadata
    {
        public required string TaskName { get; init; }
        public TaskExecutionOrigin Origin { get; set; }
        public TaskExecutionDisposition Disposition { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? ReusedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public int ExecutionCount { get; set; }
        public int ReuseCount { get; set; }
    }

    private readonly object _sync = new();
    private readonly HashSet<string> _cycleExecuted = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _cycleReused = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _cycleStartedAt = DateTimeOffset.Now;
    private DateTimeOffset? _cycleCompletedAt;
    private readonly Dictionary<string, MutableMetadata> _items =
        new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        lock (_sync)
        {
            _items.Clear();
            BeginCycleCore();
        }
    }

    public void BeginCycle()
    {
        lock (_sync) BeginCycleCore();
    }

    public void CompleteCycle()
    {
        lock (_sync) _cycleCompletedAt = DateTimeOffset.Now;
    }

    public TaskExecutionCycleSummary GetCurrentCycleSummary()
    {
        lock (_sync)
        {
            return new TaskExecutionCycleSummary(
                _cycleExecuted.Count,
                _cycleReused.Count,
                _cycleStartedAt,
                _cycleCompletedAt);
        }
    }

    public void MarkStarted(string taskName, TaskExecutionOrigin origin)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        lock (_sync)
        {
            MutableMetadata item = GetOrCreate(taskName);
            item.Origin = origin;
            item.Disposition = TaskExecutionDisposition.Executed;
            item.StartedAt = now;
            item.CompletedAt = null;
            item.Duration = null;
            item.ExecutionCount++;
            _cycleExecuted.Add(taskName);
        }
    }

    public void MarkCompleted(string taskName)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        lock (_sync)
        {
            MutableMetadata item = GetOrCreate(taskName);
            item.CompletedAt = now;
            item.Duration = item.StartedAt.HasValue ? now - item.StartedAt.Value : null;
        }
    }

    public void MarkReused(string taskName)
    {
        lock (_sync)
        {
            MutableMetadata item = GetOrCreate(taskName);
            item.Disposition = TaskExecutionDisposition.Reused;
            item.ReusedAt = DateTimeOffset.Now;
            item.ReuseCount++;
            _cycleReused.Add(taskName);
        }
    }

    public IReadOnlyList<TaskExecutionMetadata> GetSnapshot()
    {
        lock (_sync)
        {
            return _items.Values
                .OrderBy(item => item.TaskName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new TaskExecutionMetadata(
                    item.TaskName,
                    item.Origin,
                    item.Disposition,
                    item.StartedAt,
                    item.CompletedAt,
                    item.ReusedAt,
                    item.Duration,
                    item.ExecutionCount,
                    item.ReuseCount))
                .ToList();
        }
    }

    private void BeginCycleCore()
    {
        _cycleExecuted.Clear();
        _cycleReused.Clear();
        _cycleStartedAt = DateTimeOffset.Now;
        _cycleCompletedAt = null;
    }
    private MutableMetadata GetOrCreate(string taskName)
    {
        if (!_items.TryGetValue(taskName, out MutableMetadata? item))
        {
            item = new MutableMetadata
            {
                TaskName = taskName,
                Origin = TaskExecutionOrigin.Audit,
                Disposition = TaskExecutionDisposition.Executed
            };
            _items[taskName] = item;
        }
        return item;
    }
}