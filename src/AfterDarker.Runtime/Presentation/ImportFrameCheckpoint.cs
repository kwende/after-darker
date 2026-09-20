using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>An artifact-specific point where a completed imported call leaves an image worth presenting.</summary>
/// <param name="ResumeAddress">NE code address immediately after that CALL FAR; resolved by the existing load plan.</param>
/// <param name="Import">Expected import identity, so an unrelated call cannot trigger presentation.</param>
/// <param name="MinimumDisplayTime">Explicit modern host hold, not an emulated Windows delay or historical timing claim.</param>
public sealed record ImportFrameCheckpoint(NeAddress ResumeAddress, NeImport Import, TimeSpan MinimumDisplayTime);

/// <summary>Consume borrowed RGB pixels synchronously; copy them before returning, and never reenter the guest.</summary>
/// <param name="pixels">Current software surface. The buffer is reused at the next checkpoint.</param>
/// <param name="minimumDisplayTime">Suggested live hold; deterministic tests can consume the frame immediately.</param>
public delegate void IntermediateFrameHandler(ReadOnlySpan<byte> pixels, TimeSpan minimumDisplayTime);
