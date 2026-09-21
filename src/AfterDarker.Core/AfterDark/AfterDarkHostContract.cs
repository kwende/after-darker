using System.Buffers.Binary;

namespace AfterDarker.Core.AfterDark;

/// <summary>Shared host values exercised by supported original module dispatchers.</summary>
/// <remarks>
/// SDK field names are now known, but the allocations below still cover only the executed paths.
/// See docs/research/after-dark-sdk.md before expanding records or naming the unexplained tail words.
/// Profiles add required fields explicitly while preserving the proven compatibility words.
/// </remarks>
public static class AfterDarkHostContract
{
    /// <summary>SDK lifecycle message sent before INITIALIZE.</summary>
    public const ushort PreinitializeMessage = 12;
    /// <summary>SDK lifecycle message requesting animation initialization.</summary>
    public const ushort InitializeMessage = 0;
    /// <summary>SDK HSV_PAL reply from BLANK, requesting a smooth hue/saturation palette in the original indexed host.</summary>
    public const ushort HueSaturationPaletteRequest = 10;
    /// <summary>SDK RGB_PAL reply: prepare the original host's RGB palette.</summary>
    public const ushort RgbPaletteRequest = 11;
    /// <summary>SDK GREY_PAL reply: prepare the original host's grayscale palette.</summary>
    public const ushort GrayscalePaletteRequest = 12;
    /// <summary>Guest handle resolved by GlobalLock to our system record.</summary>
    public const ushort SystemHandle = 0x101;
    /// <summary>Guest handle resolved by GlobalLock to our module record.</summary>
    public const ushort ModuleHandle = 0x102;
    /// <summary>Guest HDC identity registered against the software drawing surface.</summary>
    public const ushort ReservedHdc = 0x103;
    /// <summary>Current allocation covering observed accesses, including an unexplained private tail.</summary>
    public const int SystemBytes = 0x36;
    /// <summary>Offset of AD_SYSTEM.ptScreenSize.x, the desktop width in pixels.</summary>
    public const int ScreenWidth = 0x06;
    /// <summary>Offset of AD_SYSTEM.ptScreenSize.y, the desktop height in pixels.</summary>
    public const int ScreenHeight = 0x08;
    /// <summary>Offset of the SDK iBitsPerPixel field.</summary>
    public const int ColorDepth = 0x0A;
    /// <summary>Offset of AD_SYSTEM.ptAspect.x, the SDK's relative horizontal pixel aspect.</summary>
    public const int PixelAspectX = 0x0C;
    /// <summary>Offset of AD_SYSTEM.ptAspect.y, the SDK's relative vertical pixel aspect.</summary>
    public const int PixelAspectY = 0x0E;
    /// <summary>Offset of the SDK iADVersion field.</summary>
    public const int SystemVersion = 0x14;
    /// <summary>Offset of the SDK hModuleInfo field, containing a handle rather than a pointer.</summary>
    public const int SystemModuleHandle = 0x20;
    /// <summary>Observed compatibility word beyond public AD_SYSTEM; 0x42 passes the tested check.</summary>
    public const int Compatibility42 = 0x2C;
    /// <summary>Observed compatibility word beyond public AD_SYSTEM; 0x48 passes the tested check.</summary>
    public const int Compatibility48 = 0x34;

    /// <summary>Create the shared zero-filled system record and populate the currently required words.</summary>
    public static byte[] CreateSystemRecord()
    {
        byte[] systemRecord = new byte[SystemBytes];
        WriteField(SystemVersion, 200);
        WriteField(SystemModuleHandle, ModuleHandle);
        WriteField(Compatibility42, 0x42);
        WriteField(Compatibility48, 0x48);
        return systemRecord;

        // Win16 words are always two little-endian bytes, regardless of the host's C# layout.
        void WriteField(int byteOffset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(systemRecord.AsSpan(byteOffset), value);
        }
    }
}
