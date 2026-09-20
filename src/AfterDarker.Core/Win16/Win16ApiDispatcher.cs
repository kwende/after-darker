using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Adapts decoded argument words to named Windows methods and packs their results.</summary>
/// <remarks>
/// Import metadata lives in Win16Imports; behavior lives in Win16Api and its state objects.
/// No register mutation, stack cleanup, or instruction execution belongs here.
/// See docs/win16-implementations.md for this division of responsibilities.
/// </remarks>
public static class Win16ApiDispatcher
{
    /// <summary>Invoke exactly the service described by a known binding using its Pascal argument order.</summary>
    public static Win16Imports.Reply Invoke(Win16Api api, Win16Imports.ImportEntry entry,
        IReadOnlyList<ushort> argumentWords, Win16CallContext context = default)
    {
        if (entry.Implementation == Win16Imports.Handler.Unsupported)
        {
            throw new NotSupportedException($"{entry.Name} reached outside the supported service set.");
        }
        if (argumentWords.Count * sizeof(ushort) != entry.ArgumentBytes)
        {
            throw new ArgumentException($"Wrong argument count for {entry.Name}.");
        }

        var arguments = new Win16ArgumentReader(argumentWords);
        switch (entry.Implementation)
        {
            case Win16Imports.Handler.LocalAlloc:
                {
                    var flags = (LocalMemoryFlags)arguments.ReadWord();
                    ushort bytes = arguments.ReadWord();
                    ushort handle = api.LocalAlloc(context.DataSelector, flags, bytes);
                    // The Win16 reference also returns the handle in CX.
                    return new Win16Imports.Reply(handle, handle);
                }
            case Win16Imports.Handler.LocalLock:
                return new Win16Imports.Reply(PackFarPointer(api.LocalLock(context.DataSelector, arguments.ReadWord())));
            case Win16Imports.Handler.LocalUnlock:
                return new Win16Imports.Reply(api.LocalUnlock(context.DataSelector, arguments.ReadWord()));
            case Win16Imports.Handler.LocalFree:
                return new Win16Imports.Reply(api.LocalFree(context.DataSelector, arguments.ReadWord()));
            case Win16Imports.Handler.LocalInit:
                {
                    ushort dataSelector = arguments.ReadWord();
                    ushort heapStart = arguments.ReadWord();
                    ushort heapBytes = arguments.ReadWord();
                    return BooleanResult(api.LocalInit(dataSelector, heapStart, heapBytes));
                }
            case Win16Imports.Handler.GlobalLock:
                {
                    ushort blockHandle = arguments.ReadWord();
                    FarPointer16 address = api.GlobalLock(blockHandle);
                    // Win16 GlobalLock supplies both DX:AX and the selector alone in CX.
                    return new Win16Imports.Reply(PackFarPointer(address), address.Selector);
                }
            case Win16Imports.Handler.GlobalUnlock:
                return new Win16Imports.Reply(api.GlobalUnlock(arguments.ReadWord()));
            case Win16Imports.Handler.Environment:
                return new Win16Imports.Reply(PackFarPointer(api.GetDOSEnvironment()));
            case Win16Imports.Handler.Ticks:
                return new Win16Imports.Reply(api.GetTickCount());
            case Win16Imports.Handler.CreatePen:
                {
                    short style = arguments.ReadSignedWord();
                    short width = arguments.ReadSignedWord();
                    uint colorReference = arguments.ReadDoubleWord();
                    return new Win16Imports.Reply(api.CreatePen(style, width, colorReference));
                }
            case Win16Imports.Handler.CreateSolidBrush:
                return new Win16Imports.Reply(api.CreateSolidBrush(arguments.ReadDoubleWord()));
            case Win16Imports.Handler.GetWindowOrg:
                return new Win16Imports.Reply(api.GetWindowOrg(arguments.ReadWord()));
            case Win16Imports.Handler.SetWindowOrg:
                return new(api.SetWindowOrg(arguments.ReadWord(), arguments.ReadSignedWord(), arguments.ReadSignedWord()));
            case Win16Imports.Handler.SetROP2:
                return new(api.SetROP2(arguments.ReadWord(), arguments.ReadWord()));
            case Win16Imports.Handler.SetPixel:
                return new(api.SetPixel(arguments.ReadWord(), arguments.ReadSignedWord(), arguments.ReadSignedWord(), arguments.ReadDoubleWord()));
            case Win16Imports.Handler.BitBlt:
                {
                    ushort destinationHdc = arguments.ReadWord();
                    short destinationX = arguments.ReadSignedWord(), destinationY = arguments.ReadSignedWord();
                    short width = arguments.ReadSignedWord(), height = arguments.ReadSignedWord();
                    ushort sourceHdc = arguments.ReadWord();
                    short sourceX = arguments.ReadSignedWord(), sourceY = arguments.ReadSignedWord();
                    uint operation = arguments.ReadDoubleWord();
                    return BooleanResult(api.BitBlt(destinationHdc, destinationX, destinationY, width, height,
                        sourceHdc, sourceX, sourceY, operation));
                }
            case Win16Imports.Handler.OffsetRect:
            case Win16Imports.Handler.InflateRect:
                {
                    FarPointer16 rectangle = arguments.ReadFarPointer();
                    short horizontal = arguments.ReadSignedWord(), vertical = arguments.ReadSignedWord();
                    if (entry.Implementation == Win16Imports.Handler.OffsetRect) api.OffsetRect(rectangle, horizontal, vertical);
                    else api.InflateRect(rectangle, horizontal, vertical);
                    return new(0);
                }
            case Win16Imports.Handler.IntersectRect:
                return BooleanResult(api.IntersectRect(arguments.ReadFarPointer(), arguments.ReadFarPointer(), arguments.ReadFarPointer()));
            case Win16Imports.Handler.EqualRect:
                return BooleanResult(api.EqualRect(arguments.ReadFarPointer(), arguments.ReadFarPointer()));
            case Win16Imports.Handler.SelectObject:
                {
                    ushort deviceContext = arguments.ReadWord();
                    ushort objectHandle = arguments.ReadWord();
                    return new Win16Imports.Reply(api.SelectObject(deviceContext, objectHandle));
                }
            case Win16Imports.Handler.DeleteObject:
                return BooleanResult(api.DeleteObject(arguments.ReadWord()));
            case Win16Imports.Handler.MoveTo:
                {
                    ushort deviceContext = arguments.ReadWord();
                    short destinationX = arguments.ReadSignedWord();
                    short destinationY = arguments.ReadSignedWord();
                    return new Win16Imports.Reply(api.MoveTo(deviceContext, destinationX, destinationY));
                }
            case Win16Imports.Handler.LineTo:
                {
                    ushort deviceContext = arguments.ReadWord();
                    short destinationX = arguments.ReadSignedWord();
                    short destinationY = arguments.ReadSignedWord();
                    return BooleanResult(api.LineTo(deviceContext, destinationX, destinationY));
                }
            case Win16Imports.Handler.Ellipse:
            case Win16Imports.Handler.Rectangle:
                {
                    ushort deviceContext = arguments.ReadWord();
                    short left = arguments.ReadSignedWord();
                    short top = arguments.ReadSignedWord();
                    short right = arguments.ReadSignedWord();
                    short bottom = arguments.ReadSignedWord();
                    return BooleanResult(entry.Implementation == Win16Imports.Handler.Ellipse
                        ? api.Ellipse(deviceContext, left, top, right, bottom)
                        : api.Rectangle(deviceContext, left, top, right, bottom));
                }
            case Win16Imports.Handler.SetRect:
                {
                    FarPointer16 destination = arguments.ReadFarPointer();
                    short left = arguments.ReadSignedWord();
                    short top = arguments.ReadSignedWord();
                    short right = arguments.ReadSignedWord();
                    short bottom = arguments.ReadSignedWord();
                    api.SetRect(destination, left, top, right, bottom);
                    return new Win16Imports.Reply(0); // Void: the register convention ignores Value.
                }
            case Win16Imports.Handler.GetStockObject:
                return new Win16Imports.Reply(api.GetStockObject(arguments.ReadSignedWord()));
            case Win16Imports.Handler.PtInRect:
                {
                    FarPointer16 rectangleAddress = arguments.ReadFarPointer();
                    Point16 point = arguments.ReadPoint();
                    return BooleanResult(api.PtInRect(rectangleAddress, point));
                }
            case Win16Imports.Handler.FillRect:
            case Win16Imports.Handler.FrameRect:
                {
                    ushort deviceContext = arguments.ReadWord();
                    FarPointer16 rectangle = arguments.ReadFarPointer();
                    ushort brushHandle = arguments.ReadWord();
                    short result = entry.Implementation == Win16Imports.Handler.FillRect
                        ? api.FillRect(deviceContext, rectangle, brushHandle) : api.FrameRect(deviceContext, rectangle, brushHandle);
                    return new Win16Imports.Reply(unchecked((ushort)result));
                }
            case Win16Imports.Handler.InvertRect:
                {
                    ushort deviceContext = arguments.ReadWord();
                    FarPointer16 rectangle = arguments.ReadFarPointer();
                    api.InvertRect(deviceContext, rectangle);
                    return new Win16Imports.Reply(0);
                }
            default:
                throw new NotSupportedException(entry.Name);
        }
    }

    /// <summary>Represent a Win16 BOOL as a nonzero/zero word value.</summary>
    private static Win16Imports.Reply BooleanResult(bool value) => new(value ? 1u : 0u);

    /// <summary>Pack a guest far pointer for DX:AX, with the selector in the high word.</summary>
    private static uint PackFarPointer(FarPointer16 pointer) => ((uint)pointer.Selector << 16) | pointer.Offset;
}
