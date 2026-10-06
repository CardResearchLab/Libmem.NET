using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static TException ExpectThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException(message);
}

static byte[] ReturnConstant(int value, int size = 64)
{
    if (size < 16)
        throw new ArgumentOutOfRangeException(nameof(size));

    var code = Enumerable.Repeat((byte)0x90, size).ToArray();
    code[0] = 0xB8; // mov eax, imm32
    BitConverter.GetBytes(value).CopyTo(code, 1);
    code[^1] = 0xC3; // ret
    return code;
}

static ulong ReadPointer(MemoryManager memory, ulong address)
{
    var bytes = memory.Read(address, IntPtr.Size);
    Check(bytes.Length == IntPtr.Size, $"Could not read {IntPtr.Size} pointer bytes at 0x{address:X}.");
    return IntPtr.Size == sizeof(ulong)
        ? BitConverter.ToUInt64(bytes, 0)
        : BitConverter.ToUInt32(bytes, 0);
}

static void WritePointer(MemoryManager memory, ulong address, ulong value)
{
    byte[] bytes = IntPtr.Size == sizeof(ulong)
        ? BitConverter.GetBytes(value)
        : BitConverter.GetBytes(checked((uint)value));
    Check(memory.Write(address, bytes) == bytes.Length,
        $"Could not write {bytes.Length} pointer bytes at 0x{address:X}.");
}

static unsafe int CallNoArgs(ulong address)
{
    var fn = (delegate* unmanaged<int>)(void*)address;
    return fn();
}

Console.WriteLine("Libmem.NET Hook/VMT runtime tests");
var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;

try
{
    _ = new VmtManager(0);
    throw new InvalidOperationException("VmtManager(0) should reject a zero VTable address.");
}
catch (ArgumentOutOfRangeException ex)
{
    Check(ex.ParamName == "vtableAddress", "VmtManager(0) reported the wrong parameter name.");
}

using var session = NativeApi.Attach((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not attach to the current process.");

var memory = session.Memory;

// Managed HookManager argument/state contracts.
var zeroSource = ExpectThrows<ArgumentOutOfRangeException>(
    () => session.Hooks.Install(0, 1),
    "HookManager.Install should reject a zero source address.");
Check(zeroSource.ParamName == "source", "Zero source reported the wrong parameter name.");

var zeroDestination = ExpectThrows<ArgumentOutOfRangeException>(
    () => session.Hooks.Install(1, 0),
    "HookManager.Install should reject a zero destination address.");
Check(zeroDestination.ParamName == "destination", "Zero destination reported the wrong parameter name.");

var badSource = ExpectThrows<ArgumentOutOfRangeException>(
    () => session.Hooks.Install(invalidAddress, 1),
    "HookManager.Install should reject the bad-address source sentinel.");
Check(badSource.ParamName == "source", "Bad source reported the wrong parameter name.");

var badDestination = ExpectThrows<ArgumentOutOfRangeException>(
    () => session.Hooks.Install(1, invalidAddress),
    "HookManager.Install should reject the bad-address destination sentinel.");
Check(badDestination.ParamName == "destination", "Bad destination reported the wrong parameter name.");

// HookManager + HookHandle + trampoline lifecycle.
using var source = memory.Allocate(4096, MemoryProtection.ExecuteReadWrite)
    ?? throw new InvalidOperationException("Could not allocate source code page.");
using var destination = memory.Allocate(4096, MemoryProtection.ExecuteReadWrite)
    ?? throw new InvalidOperationException("Could not allocate destination code page.");

var sourceCode = ReturnConstant(1);
var destinationCode = ReturnConstant(2);
Check(memory.Write(source.Address, sourceCode) == sourceCode.Length, "Could not write source machine code.");
Check(memory.Write(destination.Address, destinationCode) == destinationCode.Length, "Could not write destination machine code.");
Check(CallNoArgs(source.Address) == 1, "Source function did not return its original value before hooking.");
Check(CallNoArgs(destination.Address) == 2, "Destination function did not return its expected value.");

using var hook = session.Hooks.Install(source.Address, destination.Address);

Check(hook.Source == source.Address, "HookHandle.Source does not match the hooked source.");
Check(hook.Destination == destination.Address, "HookHandle.Destination does not match the hook target.");
Check(hook.Trampoline != 0 && hook.Trampoline != invalidAddress, "HookHandle.Trampoline is invalid.");
Check(hook.PatchedBytes > 0, "HookHandle.PatchedBytes must be greater than zero.");
Check(hook.IsInstalled, "HookHandle should report installed after HookManager.Install.");
Check(!hook.IsDisposed, "HookHandle should not be disposed immediately after installation.");
Check(CallNoArgs(source.Address) == 2, "Hooked source did not redirect to the destination.");
Check(CallNoArgs(hook.Trampoline) == 1, "Trampoline did not execute the original source behavior.");

Check(hook.Remove(), "HookHandle.Remove failed.");
Check(!hook.IsInstalled, "HookHandle should report uninstalled after Remove.");
Check(CallNoArgs(source.Address) == 1, "Source behavior was not restored after Remove.");
Check(hook.Remove(), "HookHandle.Remove should be idempotent after a successful removal.");

var disposeHook = session.Hooks.Install(source.Address, destination.Address);
Check(disposeHook.IsInstalled, "Dispose coverage hook should start installed.");
Check(disposeHook.Destination == destination.Address, "Dispose coverage hook lost its destination metadata.");
Check(CallNoArgs(source.Address) == 2, "Dispose coverage hook did not redirect source.");
((IDisposable)disposeHook).Dispose();
((IDisposable)disposeHook).Dispose();
Check(disposeHook.IsDisposed, "HookHandle should report disposed after repeated Dispose calls.");
Check(!disposeHook.IsInstalled, "HookHandle should report uninstalled after repeated Dispose calls.");
Check(disposeHook.Remove(), "HookHandle.Remove should remain idempotent after successful Dispose.");
Check(CallNoArgs(source.Address) == 1, "HookHandle.Dispose did not restore source behavior.");

var disposedSession = NativeApi.Attach((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not create disposed-session HookManager coverage.");
var disposedHooks = disposedSession.Hooks;
disposedSession.Dispose();
ExpectThrows<ObjectDisposedException>(
    () => disposedHooks.Install(0, 0),
    "Disposed HookManager should reject use before validating hook addresses.");

// VmtManager lifecycle on an isolated page owned by this test process.
using var vtablePage = memory.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Could not allocate VMT test page.");

const ulong original0 = 0x11112222UL;
const ulong original1 = 0x55556666UL;
const ulong original2 = 0x13572468UL;
const ulong replacement0 = 0x9999AAAAUL;
const ulong replacement1 = 0xDDDDEEEEUL;
const ulong replacement0Second = 0xABCDEF01UL;

WritePointer(memory, vtablePage.Address, original0);
WritePointer(memory, vtablePage.Address + (ulong)IntPtr.Size, original1);
WritePointer(memory, vtablePage.Address + (ulong)(2 * IntPtr.Size), original2);

var vmt = new VmtManager(vtablePage.Address);
Check(!vmt.IsDisposed, "VmtManager should start undisposed.");
Check(vmt.GetOriginal(0) == original0, "VmtManager.GetOriginal returned the wrong initial slot value.");

var zeroReplacement = ExpectThrows<ArgumentOutOfRangeException>(
    () => vmt.Hook(0, 0),
    "VmtManager.Hook should reject a zero replacement address.");
Check(zeroReplacement.ParamName == "replacementAddress", "Zero VMT replacement reported the wrong parameter name.");

var badReplacement = ExpectThrows<ArgumentOutOfRangeException>(
    () => vmt.Hook(0, invalidAddress),
    "VmtManager.Hook should reject the bad-address replacement sentinel.");
Check(badReplacement.ParamName == "replacementAddress", "Bad VMT replacement reported the wrong parameter name.");

Check(vmt.Unhook(2), "VmtManager.Unhook should be idempotent for an untracked in-range slot.");
Check(ReadPointer(memory, vtablePage.Address + (ulong)(2 * IntPtr.Size)) == original2,
    "Unhooking an untracked slot should not change its value.");

vmt.Hook(0, replacement0);
Check(ReadPointer(memory, vtablePage.Address) == replacement0, "VmtManager.Hook did not update slot 0.");
Check(vmt.GetOriginal(0) == original0, "VmtManager did not preserve the original slot 0 value.");

vmt.Hook(0, replacement0Second);
Check(ReadPointer(memory, vtablePage.Address) == replacement0Second,
    "Repeated VmtManager.Hook did not update slot 0 to the latest replacement.");
Check(vmt.GetOriginal(0) == original0,
    "Repeated VmtManager.Hook must preserve the first original slot 0 value.");

Check(vmt.Unhook(0), "VmtManager.Unhook failed for slot 0.");
Check(ReadPointer(memory, vtablePage.Address) == original0, "VmtManager.Unhook did not restore slot 0.");

vmt.Hook(0, replacement0);
vmt.Hook(1, replacement1);
Check(ReadPointer(memory, vtablePage.Address) == replacement0, "VmtManager.Hook did not update slot 0 before Reset.");
Check(ReadPointer(memory, vtablePage.Address + (ulong)IntPtr.Size) == replacement1, "VmtManager.Hook did not update slot 1 before Reset.");
vmt.Reset();
Check(ReadPointer(memory, vtablePage.Address) == original0, "VmtManager.Reset did not restore slot 0.");
Check(ReadPointer(memory, vtablePage.Address + (ulong)IntPtr.Size) == original1, "VmtManager.Reset did not restore slot 1.");

vmt.Hook(1, replacement1);
Check(ReadPointer(memory, vtablePage.Address + (ulong)IntPtr.Size) == replacement1,
    "VmtManager should remain reusable after Reset.");
Check(vmt.GetOriginal(1) == original1, "VmtManager lost the original slot value after Reset/reuse.");
Check(vmt.Unhook(1), "VmtManager.Unhook failed after Reset/reuse.");
Check(ReadPointer(memory, vtablePage.Address + (ulong)IntPtr.Size) == original1,
    "VmtManager.Unhook did not restore slot 1 after Reset/reuse.");

vmt.Hook(0, replacement0);
((IDisposable)vmt).Dispose();
((IDisposable)vmt).Dispose();
Check(vmt.IsDisposed, "VmtManager should report disposed after repeated Dispose calls.");
Check(ReadPointer(memory, vtablePage.Address) == original0, "VmtManager.Dispose did not restore an active hook.");

var disposedVmtThrows = false;
try
{
    _ = vmt.GetOriginal(0);
}
catch (ObjectDisposedException)
{
    disposedVmtThrows = true;
}
Check(disposedVmtThrows, "VmtManager should reject operations after Dispose.");

Console.WriteLine("HOOK/VMT RUNTIME TESTS PASS");
