using System.Buffers.Binary;

namespace AfterDarker.Core.AfterDark;

/// <summary>Shared host values currently exercised by both supported module dispatchers.</summary>
/// <remarks>
/// SDK field names are now known, but the allocations below still cover only the executed paths.
/// See docs/research/after-dark-sdk.md before expanding records or naming the unexplained tail words.
/// This readability pass deliberately preserves the proven record bytes.
/// </remarks>
public static class AfterDarkHostContract
{
    /// <summary>SDK lifecycle message sent before INITIALIZE.</summary>
    public const ushort PreinitializeMessage = 12;
    /// <summary>SDK lifecycle message requesting animation initialization.</summary>
    public const ushort InitializeMessage = 0;
    /// <summary>Guest handle resolved by GlobalLock to our system record.</summary>
    public const ushort SystemHandle = 0x101;
    /// <summary>Guest handle resolved by GlobalLock to our module record.</summary>
    public const ushort ModuleHandle = 0x102;
    /// <summary>Guest HDC identity registered against the software drawing surface.</summary>
    public const ushort ReservedHdc = 0x103;
    /// <summary>Current allocation covering observed accesses, including an unexplained private tail.</summary>
    public const int SystemBytes = 0x36;
    /// <summary>Offset of the SDK iBitsPerPixel field.</summary>
    public const int ColorDepth = 0x0A;
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
