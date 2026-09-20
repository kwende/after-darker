using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

// Nested types retain the tutorial-facing API; this file describes the diagnostic snapshots.
public partial class AfterDarkSession<TState>
{
    /// <summary>One completed lifecycle invocation, including guest-written return value and observed globals.</summary>
    /// <param name="Name">Lifecycle label.</param>
    /// <param name="Registers">CPU state after the guest caller stored AX.</param>
    /// <param name="StoredAx">Actual guest store, checked against the returned AX register.</param>
    /// <param name="State">Module-specific global state decoded from the DLL data segment.</param>
    public sealed record PhaseResult(string Name, SegmentedGuest.CpuState Registers, ushort StoredAx,
        TState State);
    /// <summary>Detached execution evidence; obtaining this snapshot does not execute guest code.</summary>
    /// <param name="Plan">Prepared segments and relocation evidence.</param>
    /// <param name="BeforeExecution">Globals before the first guest instruction.</param>
    /// <param name="Phases">Retained lifecycle observations, bounded by diagnostic policy.</param>
    /// <param name="Calls">Retained import-call traces, bounded by diagnostic policy.</param>
    /// <param name="Interrupts">Retained software-interrupt observations.</param>
    /// <param name="Heap">Validated LocalInit reservation, not a general heap allocator.</param>
    /// <param name="OutstandingLocks">Resident global-block locks still held.</param>
    /// <param name="Instructions">Lifetime instruction count.</param>
    /// <param name="ProtectedMode">Whether CR0 still has the protection-enable bit set.</param>
    /// <param name="Diagnostics">Retention policy and complete lifetime counters.</param>
    public sealed record Result(NeLoadPlan Plan, TState BeforeExecution,
        IReadOnlyList<PhaseResult> Phases, IReadOnlyList<Win16CallTrace> Calls,
        IReadOnlyList<SegmentedGuest.InterruptVisit> Interrupts, LocalHeapReservation Heap,
        int OutstandingLocks, long Instructions, bool ProtectedMode, DiagnosticSummary Diagnostics);
    /// <summary>Distinguishes retained history length from lifetime totals.</summary>
    /// <param name="HistoryCapacity">Maximum entries per history; null means full recording.</param>
    /// <param name="TotalCalls">Lifetime import calls.</param>
    /// <param name="TotalPhases">Lifetime lifecycle calls.</param>
    /// <param name="TotalInterrupts">Lifetime software interrupts.</param>
    /// <param name="ImportCalls">Lifetime counters keyed by symbolic import identity.</param>
    public sealed record DiagnosticSummary(int? HistoryCapacity, long TotalCalls, long TotalPhases,
        long TotalInterrupts, IReadOnlyDictionary<string, long> ImportCalls);

}
