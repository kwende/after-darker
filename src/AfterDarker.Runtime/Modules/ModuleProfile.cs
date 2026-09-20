namespace AfterDarker.Runtime;

/// <summary>Only artifact-specific record layouts and proof checks belong here.
/// CPU setup, relocations, imports and the lifecycle loop are shared.</summary>
public abstract class ModuleProfile<TState>
{
    /// <summary>Display identity of the analyzed module.</summary>
    public abstract string Name { get; }
    /// <summary>Exact supported artifact hash; observations are not valid for unknown revisions.</summary>
    public abstract string Sha256 { get; }
    /// <summary>Maximum managed service exits allowed in one ordinary lifecycle call.</summary>
    public virtual int ServiceExitLimit => SegmentedGuest.DefaultServiceExitLimit;
    /// <summary>CLOSE may need extra calls to undo all outstanding drawing operations.</summary>
    public virtual int CloseServiceExitLimit => ServiceExitLimit;
    /// <summary>Reserve the remaining 64-KiB data-segment tail for bounded local-heap growth before execution.</summary>
    /// <remarks>False retains the NE's initial reservation only. This changes backing capacity, not the startup CX input.</remarks>
    public virtual bool AllowLocalHeapGrowth => false;
    /// <summary>Check host settings before allocating or executing the guest.</summary>
    public virtual void ValidateOptions(PlaybackOptions options) => options.Validate();
    /// <summary>Interpret the original BLANK reply; special host requests need an explicit profile policy.</summary>
    public virtual void ValidateBlankResult(ushort result)
    {
        if (result != 0) throw new NotSupportedException($"{Name} returned unsupported BLANK result {result}.");
    }
    /// <summary>Supply host-owned starting RGB pixels once, before guest execution; null keeps the black surface.</summary>
    /// <remarks>
    /// The session copies the returned image. This is host input, not module drawing or a per-frame reset.
    /// Fade Away supplies white; a future host image source can supply captured pixels through the same surface-loading path.
    /// </remarks>
    public virtual byte[]? CreateInitialPixels(PlaybackOptions options) => null;
    /// <summary>Serialize the supported paths' system and module records in Win16 byte layout.</summary>
    /// <remarks>Early profiles retain narrow records; Rainstorm/Fade Away use the full SDK module allocation. See docs/research/after-dark-sdk.md.</remarks>
    public abstract (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options);
    /// <summary>Decode artifact-specific globals from a detached automatic-data-segment snapshot.</summary>
    public abstract TState Observe(byte[] bytes);
    /// <summary>Read the instance token saved by the original DLL startup code.</summary>
    public abstract ushort Instance(TState state);
    /// <summary>Read the guest's PREINITIALIZE compatibility flag, distinct from incidental AX contents.</summary>
    public abstract ushort Compatibility(TState state);
    /// <summary>Check the observed globals against the inputs supplied to the module.</summary>
    public abstract void ValidateInitialized(TState state, PlaybackOptions options, ushort hostDataSelector, uint? lastReturnedTick);
}
