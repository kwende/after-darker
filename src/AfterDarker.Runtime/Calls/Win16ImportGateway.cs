using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Connects a trapped imported far call to its C# implementation and back to the guest.</summary>
/// <remarks>
/// Dispatch runs after EmuStop has returned control to managed code, never inside a native hook.
/// This is the shared path for the live player, lessons 08/09 and synthetic conformance tests.
/// Read docs/runtime-code-map.md to follow the stack and register responsibilities.
/// </remarks>
/// <param name="guest">Paused guest at a synthetic import address.</param>
/// <param name="bindings">Canonical identities bound to this guest's gateway selector.</param>
/// <param name="services">Stateful Windows implementations; they do not manipulate CPU registers.</param>
/// <param name="stack">Frame decoder and return mechanism for this guest's caller stack.</param>
public sealed class Win16ImportGateway(SegmentedGuest guest,
    IReadOnlyList<Win16Imports.ImportEntry> bindings, Win16Api services, Win16Stack stack)
{
    /// <summary>Resolve, decode, invoke, write results, and return; the execution loop resumes afterward.</summary>
    public Win16CallTrace Dispatch()
    {
        SegmentedGuest.CpuState before = guest.Snapshot();
        Win16Imports.ImportEntry binding = ResolveBinding(before);
        Win16CallFrame frame = stack.ReadCallFrame(binding.ArgumentBytes!.Value, binding.Name);
        Win16Imports.Reply reply = InvokeService(binding, frame, before);

        Win16RegisterConvention.WriteReturnRegisters(guest, binding.ReturnLayout!.Value, reply);
        stack.ReturnToCaller(frame);

        return new Win16CallTrace(guest.Phase, binding, frame.Arguments, reply.Value, before, guest.Snapshot());
    }

    /// <summary>Fail symbolically before interpreting an unknown or unsupported API's stack.</summary>
    private Win16Imports.ImportEntry ResolveBinding(SegmentedGuest.CpuState registers)
    {
        Win16Imports.ImportEntry binding = bindings.SingleOrDefault(candidate => candidate.Address == registers.Pc)
            ?? throw new NotSupportedException($"Unknown import gateway {registers.Pc} during {guest.Phase}.");

        if (binding.Implementation == Win16Imports.Handler.Unsupported)
        {
            throw new NotSupportedException($"{guest.Phase}: {binding.Name} reached at {registers.Pc}; service is not enabled.");
        }
        if (binding.ArgumentBytes is null || binding.ReturnLayout is null)
        {
            throw new InvalidOperationException($"{binding.Name} has no complete calling convention.");
        }
        return binding;
    }

    /// <summary>Add import identity and guest location to service failures without attempting a return.</summary>
    private Win16Imports.Reply InvokeService(Win16Imports.ImportEntry binding,
        Win16CallFrame frame, SegmentedGuest.CpuState before)
    {
        try
        {
            // Local* APIs select their heap implicitly through the caller's DS.
            // Do not substitute the DLL selector: wrong-DS calls must fail visibly.
            return Win16ApiDispatcher.Invoke(services, binding, frame.Arguments, new(before.Ds));
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"{guest.Phase}: {binding.Name} at {before.Pc}: {error.Message}", error);
        }
    }
}
