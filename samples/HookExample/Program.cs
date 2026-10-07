using System.Runtime.InteropServices;
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

try
{
    Run();
}
catch (LibmemException ex)
{
    Console.Error.WriteLine($"libmem failure: {ex.Operation}: {ex.Message}");
    Environment.ExitCode = 1;
}

static void Run()
{
    var self = NativeApi.CurrentProcess()
        ?? throw new InvalidOperationException("Current process could not be resolved.");

    using var session = ProcessSession.Open(self)
        ?? throw new InvalidOperationException("Current process identity became stale before attach.");

    if (session.Architecture != Architecture.X64)
        throw new PlatformNotSupportedException("This Hook example is intentionally Windows x64-only.");

    using var source = session.Memory.Allocate(4096, MemoryProtection.ExecuteReadWrite);
    using var destination = session.Memory.Allocate(4096, MemoryProtection.ExecuteReadWrite);

    // mov eax, imm32; ret
    byte[] returnsOne = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
    byte[] returnsTwo = [0xB8, 0x02, 0x00, 0x00, 0x00, 0xC3];

    WriteExact(session, source.Address, returnsOne);
    WriteExact(session, destination.Address, returnsTwo);

    var sourceCall = FunctionAt(source.Address);
    if (sourceCall() != 1)
        throw new InvalidOperationException("Source function did not return its original value.");

    using var hook = session.Hooks.Install(source.Address, destination.Address);

    Console.WriteLine(
        $"Installed: source=0x{hook.Source:X}, destination=0x{hook.Destination:X}, " +
        $"trampoline=0x{hook.Trampoline:X}, patched={hook.PatchedBytes}");

    if (sourceCall() != 2)
        throw new InvalidOperationException("Hook did not redirect the source function.");

    var originalCall = FunctionAt(hook.Trampoline);
    if (originalCall() != 1)
        throw new InvalidOperationException("Trampoline did not preserve the original function.");

    if (!hook.Remove())
        throw new InvalidOperationException("Hook removal failed; the handle still owns the installed hook.");

    if (sourceCall() != 1)
        throw new InvalidOperationException("Source function was not restored after HookHandle.Remove.");

    Console.WriteLine("Hook lifecycle completed successfully.");
}

static void WriteExact(ProcessSession session, ulong address, byte[] bytes)
{
    var written = session.Memory.Write(address, bytes);
    if (written != bytes.Length)
        throw new InvalidOperationException(
            $"Short write at 0x{address:X}: expected {bytes.Length}, wrote {written}.");
}

static NativeFunction FunctionAt(ulong address) =>
    Marshal.GetDelegateForFunctionPointer<NativeFunction>(
        new IntPtr(unchecked((long)address)));

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NativeFunction();
