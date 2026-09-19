using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Typed Mondrian facade keeps the educational lessons and their observations intact.
/// Loading, execution, import dispatch and lifetime now live in AfterDarkSession.</summary>
public sealed class MondrianSession : AfterDarkSession<MondrianInitialization.State>
{
    // Stable addresses used by the five-segment tutorial and synthetic conformance probes.
    public new const ushort Caller = 0x30, Gateway = 0x38, Stack = 0x40, HostData = 0x48;
    public new const uint CallerBase = 0x60000, GatewayBase = 0x70000, StackBase = 0x80000, HostDataBase = 0x90000;
    public MondrianSession(byte[] file, MondrianInitialization.Options? options = null,
        bool enableDrawing = true, TextWriter? output = null, bool trace = false, int instructionLimit = 50_000,
        DiagnosticOptions? diagnostics = null, SessionTiming? timing = null)
        : base(file, new MondrianProfile(), PlaybackOptions.From(options ?? new()), enableDrawing, output, trace,
            instructionLimit, diagnostics, timing) { }
}
