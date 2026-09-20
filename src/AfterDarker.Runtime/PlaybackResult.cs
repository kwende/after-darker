namespace AfterDarker.Runtime;

/// <summary>Evidence from one completed lifecycle call.</summary>
/// <param name="Name">Lifecycle name such as INITIALIZE, DRAWFRAME, or WEP.</param>
/// <param name="StoredAx">Result written to memory by our guest caller after the DLL returned.</param>
/// <param name="Registers">CPU snapshot at the end of that caller.</param>
public sealed record PlaybackPhase(string Name, ushort StoredAx, SegmentedGuest.CpuState Registers);

/// <summary>Module-independent playback diagnostics suitable for a host or test assertion.</summary>
/// <param name="ModuleName">Verified module identity.</param>
/// <param name="Phases">Retained recent lifecycle calls, not necessarily every call.</param>
/// <param name="Instructions">Lifetime instruction total, saturating at long.MaxValue.</param>
/// <param name="OutstandingLocks">Resident global-block locks still held by the guest.</param>
/// <param name="RetainedCalls">Number of import traces retained under the diagnostic policy.</param>
/// <param name="LivePens">Owned pens still allocated, excluding stock objects.</param>
/// <param name="PeakPens">Maximum simultaneous owned pens during this session.</param>
/// <param name="ImportCalls">Lifetime call counts keyed by symbolic import name.</param>
public sealed record PlaybackResult(string ModuleName, IReadOnlyList<PlaybackPhase> Phases, long Instructions,
    int OutstandingLocks, int RetainedCalls, int LivePens, int PeakPens, IReadOnlyDictionary<string, long> ImportCalls);
