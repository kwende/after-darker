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
    /// <summary>Check host settings before allocating or executing the guest.</summary>
    public virtual void ValidateOptions(PlaybackOptions options) => options.Validate();
    /// <summary>Serialize the supported paths' system and module records in Win16 byte layout.</summary>
    /// <remarks>These remain narrow records; see docs/research/after-dark-sdk.md for the full SDK layout.</remarks>
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
