using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Build the recovered SDK records for profiles using four controls and a true-color surface.</summary>
/// <remarks>See docs/research/after-dark-sdk.md. Control meanings still belong to each module profile.</remarks>
internal static class StandardModuleRecords
{
    private const int ModuleByteCount = 0x22, WidthOffset = 2, HeightOffset = 4;
    private const int ControlValuesOffset = 6, ControlIdsOffset = 0x0E, ControlCount = 4;

    /// <summary>Write dimensions, four control words and their IDs; leave unused palette/sound fields zero.</summary>
    public static (byte[] System, byte[] Module) Create(PlaybackOptions options, params ushort[] controls)
    {
        if (controls.Length != ControlCount) throw new ArgumentException("AD_MODULE has four control words.", nameof(controls));
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[ModuleByteCount];
        WriteWord(module, WidthOffset, (ushort)options.Width);
        WriteWord(module, HeightOffset, (ushort)options.Height);
        for (int control = 0; control < ControlCount; control++)
        {
            WriteWord(module, ControlValuesOffset + control * 2, controls[control]);
            WriteWord(module, ControlIdsOffset + control * 2, (ushort)(control + 1));
        }
        return (system, module);
    }

    private static void WriteWord(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
