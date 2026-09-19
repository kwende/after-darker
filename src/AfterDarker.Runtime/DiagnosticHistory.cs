namespace AfterDarker.Runtime;

/// <summary>Recent history is safe for a long-lived session. Full recording is an explicit bounded-experiment choice.</summary>
public sealed record DiagnosticOptions(int? HistoryCapacity = 256)
{
    public static DiagnosticOptions Recent { get; } = new();
    public static DiagnosticOptions Full { get; } = new(HistoryCapacity: null);
    public void Validate()
    {
        if (HistoryCapacity is < 0 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(HistoryCapacity), "Use 0..100000 recent records, or null for explicit full recording.");
    }
}

/// <summary>
/// One owner's diagnostic history. A bounded ring overwrites its oldest entry;
/// snapshots are chronological copies. Capacity zero keeps only the total.
/// </summary>
public sealed class DiagnosticHistory<T>
{
    private readonly T[] ring;
    private readonly List<T>? full;
    private int next, count;
    public long TotalCount { get; private set; }
    public int Count => full?.Count ?? count;

    public DiagnosticHistory(DiagnosticOptions options)
    {
        options.Validate();
        ring = options.HistoryCapacity is int capacity ? new T[capacity] : [];
        if (options.HistoryCapacity is null) full = [];
    }

    public void Add(T item)
    {
        // Counters saturate instead of wrapping. Their values never govern
        // CPU safety budgets; those have separate per-invocation counters.
        if (TotalCount < long.MaxValue) TotalCount++;
        if (full is not null) { full.Add(item); return; }
        if (ring.Length == 0) return;
        ring[next] = item; // Release the overwritten reference; no growing queue.
        next = (next + 1) % ring.Length;
        if (count < ring.Length) count++;
    }

    public IReadOnlyList<T> Snapshot()
    {
        if (full is not null) return Array.AsReadOnly(full.ToArray());
        T[] result = new T[count];
        int oldest = count == ring.Length ? next : 0;
        for (int i = 0; i < count; i++) result[i] = ring[(oldest + i) % ring.Length];
        return Array.AsReadOnly(result);
    }
}
