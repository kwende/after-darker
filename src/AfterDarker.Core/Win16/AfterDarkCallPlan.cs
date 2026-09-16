using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

public enum AfterDarkMessage : ushort
{
    Initialize = 0,
    Blank = 1,
    DrawFrame = 2,
    Close = 3,
    Preinitialize = 12,
}

public sealed record AfterDarkInvocation(AfterDarkMessage Message, NeAddress EntryPoint, bool Repeat);

/// <summary>
/// A proposed invocation plan based on the documented After Dark SDK contract.
/// NE metadata supplies addresses; it does not prove this contract or its ABI.
/// This describes the dispatcher only, not the distinct DLL-startup convention.
/// </summary>
public sealed record AfterDarkCallPlan(NeAddress? LibraryStartup, ushort DispatcherOrdinal,
    NeAddress Dispatcher, IReadOnlyList<AfterDarkInvocation> Invocations)
{
    public const int ArgumentBytes = 6;

    public static AfterDarkCallPlan? FromImage(NeImage image)
    {
        NeEntry? entry = image.FindExport("MODULE");
        if (!image.Header.IsLibrary || entry?.Address is not NeAddress address ||
            image.Segments[address.SegmentNumber - 1].IsData)
            return null;

        AfterDarkMessage[] order = [AfterDarkMessage.Preinitialize, AfterDarkMessage.Initialize,
            AfterDarkMessage.Blank, AfterDarkMessage.DrawFrame, AfterDarkMessage.Close];
        return new(image.Header.Startup, entry.Ordinal, address, Array.AsReadOnly(order
            .Select(message => new AfterDarkInvocation(message, address, message == AfterDarkMessage.DrawFrame)).ToArray()));
    }
}
