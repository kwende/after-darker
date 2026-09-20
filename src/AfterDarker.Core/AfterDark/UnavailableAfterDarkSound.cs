using AfterDarker.Core.Ne;

namespace AfterDarker.Core.AfterDark;

/// <summary>Explicit no-audio implementation of the recovered AD_SND interface.</summary>
/// <remarks>
/// No device is available, so opening and playing fail and resource loading returns
/// a null HSOUND. There are no allocated sound objects to free or asynchronous jobs
/// to wait for. Even unconditional callers receive the same consistent failure path.
/// Parameters are marshaled but resource pointers are not dereferenced on failure.
/// See docs/research/gravity-execution.md and the locally recovered AD_SND.H.
/// </remarks>
public static class UnavailableAfterDarkSound
{
    /// <summary>No audio device can be opened.</summary>
    public static bool Open() => false;
    /// <summary>No asynchronous playback capability is advertised.</summary>
    public static bool HasAsyncPlayback() => false;
    /// <summary>Return a null sound handle without loading or decoding the requested resource.</summary>
    public static ushort LoadResource(ushort instance, FarPointer16 resourceName) => 0;
    /// <summary>No sound object exists whose playback mode could be changed.</summary>
    public static bool SetMode(ushort sound, short flags) => false;
    /// <summary>No playback is started, including for a null sound handle.</summary>
    public static bool Play(ushort sound) => false;
    /// <summary>No owned sound handle exists to release.</summary>
    public static bool Free(ushort sound) => false;
    /// <summary>No open device or queued playback exists to close or flush.</summary>
    public static bool Close(short flushMode) => false;
}
