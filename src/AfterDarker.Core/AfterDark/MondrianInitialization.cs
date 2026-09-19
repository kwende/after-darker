using System.Buffers.Binary;
using AfterDarker.Core.Ne;

namespace AfterDarker.Core.AfterDark;

/// <summary>
/// Hash-specific observed layout, NOT a recovered complete After Dark SDK schema.
/// Every offset below is backed by docs/research/mondrian-static-analysis.md.
/// Names describe demonstrated use; unknown compatibility fields stay explicit.
/// </summary>
public static class MondrianInitialization
{
    public const string Sha256 = "781979DA1A6A6FDF99EEBEC4DAB67E7A645BFC8787BE1671E20F13A8CA6B1AED";
    public const ushort PreinitializeMessage = 12, InitializeMessage = 0;
    public const ushort SystemHandle = 0x101, ModuleHandle = 0x102, ReservedHdc = 0x103;
    public const int SystemBytes = 0x36, ModuleBytes = 0x0A;
    public const int SystemVersion = 0x14, SystemModuleHandle = 0x20;
    public const int Compatibility42 = 0x2C, Compatibility48 = 0x34;
    public const int ModuleWidth = 2, ModuleHeight = 4, ModuleSpeed = 6, ModuleClear = 8;

    public sealed record Options(short Width = 640, short Height = 480, ushort Speed = 50, bool Clear = true)
    {
        public void Validate()
        {
            if (Width <= 0 || Height <= 0 || Speed is not (0 or 25 or 50 or 75 or 100))
                throw new ArgumentException("Mondrian requires positive signed dimensions and speed 0/25/50/75/100.");
        }
        public ushort ExpectedThreshold => Speed switch { 0 => 140, 25 => 70, 50 => 30, _ => 0 };
    }

    public static (byte[] System, byte[] Module) CreateRecords(Options options)
    {
        options.Validate();
        byte[] system = AfterDarkHostContract.CreateSystemRecord(), module = new byte[ModuleBytes];
        Put(module, ModuleWidth, (ushort)options.Width);
        Put(module, ModuleHeight, (ushort)options.Height);
        Put(module, ModuleSpeed, options.Speed);
        Put(module, ModuleClear, options.Clear ? (ushort)1 : (ushort)0);
        return (system, module);
    }

    // These are observations of the guest's data, never values for the host to
    // write. They let us prove MODULE read our records and ran its own code.
    public const int ClearState = 0x10, SpeedCounter = 0x12, SpeedThreshold = 0x14;
    public const int Compatible = 0x16, RandomSeed = 0x62, LastTick = 0x346;
    public const int ConvertedTime = 0x368, RectangleCount = 0x3A8, Instance = 0x3AA;
    public const int SystemPointer = 0x3A4, ModulePointer = 0x9EC;

    public sealed record State(ushort Compatibility, ushort Clear, ushort Counter, ushort Threshold,
        uint Seed, uint Time, uint Tick, ushort Rectangles, ushort InstanceHandle,
        FarPointer16 System, FarPointer16 Module);

    public static State Observe(byte[] data) => new(Word(data, Compatible), Word(data, ClearState),
        Word(data, SpeedCounter), Word(data, SpeedThreshold), Dword(data, RandomSeed),
        Dword(data, ConvertedTime), Dword(data, LastTick), Word(data, RectangleCount), Word(data, Instance),
        Pointer(data, SystemPointer), Pointer(data, ModulePointer));

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint Dword(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 Pointer(byte[] bytes, int offset) => new(Word(bytes, offset + 2), Word(bytes, offset));
    private static void Put(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
