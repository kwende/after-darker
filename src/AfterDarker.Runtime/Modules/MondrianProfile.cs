using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Mondrian-specific controls and proof checks; execution is shared by all profiles.</summary>
/// <remarks>See docs/research/mondrian-static-analysis.md for the observed guest globals.</remarks>
internal sealed class MondrianProfile : ModuleProfile<MondrianInitialization.State>
{
    public override string Name => "Mondrian";
    public override string Sha256 => MondrianInitialization.Sha256;
    public override int CloseServiceExitLimit => 208; // 200 inversions + blanking + lock/unlock services
    public override (byte[], byte[]) CreateRecords(PlaybackOptions options) => MondrianInitialization.CreateRecords(options.ForMondrian());
    public override MondrianInitialization.State Observe(byte[] bytes) => MondrianInitialization.Observe(bytes);
    public override ushort Instance(MondrianInitialization.State state) => state.InstanceHandle;
    public override ushort Compatibility(MondrianInitialization.State state) => state.Compatibility;
    public override void ValidateInitialized(MondrianInitialization.State state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        var expectedSystem = new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset);
        var expectedModule = new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        bool controlsMatch = state.Clear == (options.Clear ? 1 : 0) &&
            state.Threshold == options.ForMondrian().ExpectedThreshold;
        bool animationStartsEmpty = state.Counter == 0 && state.Rectangles == 0;
        bool clockMatches = state.Tick == lastReturnedTick && state.Seed == (ushort)state.Time;
        bool recordPointersMatch = state.System == expectedSystem && state.Module == expectedModule;

        if (!controlsMatch || !animationStartsEmpty || !clockMatches || !recordPointersMatch)
        {
            throw new InvalidOperationException("Mondrian initialization disagrees with its host contract.");
        }
    }
}
