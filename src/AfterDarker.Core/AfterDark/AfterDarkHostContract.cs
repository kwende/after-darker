using System.Buffers.Binary;

namespace AfterDarker.Core.AfterDark;

/// <summary>The shared fields verified in both supported modules' dispatchers.
/// This is a narrow host record, not a complete recovered After Dark SDK.</summary>
public static class AfterDarkHostContract
{
    public const ushort PreinitializeMessage = 12, InitializeMessage = 0;
    public const ushort SystemHandle = 0x101, ModuleHandle = 0x102, ReservedHdc = 0x103;
    public const int SystemBytes = 0x36, ColorDepth = 0x0A, SystemVersion = 0x14, SystemModuleHandle = 0x20;
    public const int Compatibility42 = 0x2C, Compatibility48 = 0x34;
    public static byte[] CreateSystemRecord()
    {
        byte[] system = new byte[SystemBytes];
        Put(SystemVersion, 200); Put(SystemModuleHandle, ModuleHandle);
        Put(Compatibility42, 0x42); Put(Compatibility48, 0x48);
        return system;
        void Put(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(system.AsSpan(offset), value);
    }
}
