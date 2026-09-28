using LibmemCli;

static void Check(bool condition, string message)
{
    if (!condition)
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

static ulong ReadUInt64(MemoryManager memory, ulong address)
{
    var bytes = memory.Read(address, sizeof(ulong));
    Check(bytes.Length == sizeof(ulong), $"Could not read 8 bytes at 0x{address:X}.");
    return BitConverter.ToUInt64(bytes, 0);
}

static void WriteUInt64(MemoryManager memory, ulong address, ulong value)
{
    var bytes = BitConverter.GetBytes(value);
    Check(memory.Write(address, bytes) == bytes.Length, $"Could not write 8 bytes at 0x{address:X}.");
}

static unsafe int CallNoArgs(ulong address)
{
    var fn = (delegate* unmanaged<int>)(void*)address;
    return fn();
}

Console.WriteLine("LibmemCli Hook/VMT runtime tests");

using var session = Libmem.Attach((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not attach to the current process.");

var memory = session.Memory;

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
Check(hook.Trampoline != 0 && hook.Trampoline != ulong.MaxValue, "HookHandle.Trampoline is invalid.");
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
Check(disposeHook.IsDisposed, "HookHandle should report disposed after successful Dispose.");
Check(!disposeHook.IsInstalled, "HookHandle should report uninstalled after successful Dispose.");
Check(CallNoArgs(source.Address) == 1, "HookHandle.Dispose did not restore source behavior.");

// VmtManager lifecycle on an isolated page owned by this test process.
using var vtablePage = memory.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Could not allocate VMT test page.");

const ulong original0 = 0x1111222233334444UL;
const ulong original1 = 0x5555666677778888UL;
const ulong replacement0 = 0x9999AAAABBBBCCCCUL;
const ulong replacement1 = 0xDDDDEEEEFFFF0001UL;

WriteUInt64(memory, vtablePage.Address, original0);
WriteUInt64(memory, vtablePage.Address + sizeof(ulong), original1);

var vmt = new VmtManager(vtablePage.Address);
Check(!vmt.IsDisposed, "VmtManager should start undisposed.");
Check(vmt.GetOriginal(0) == original0, "VmtManager.GetOriginal returned the wrong initial slot value.");

vmt.Hook(0, replacement0);
Check(ReadUInt64(memory, vtablePage.Address) == replacement0, "VmtManager.Hook did not update slot 0.");
Check(vmt.GetOriginal(0) == original0, "VmtManager did not preserve the original slot 0 value.");
Check(vmt.Unhook(0), "VmtManager.Unhook failed for slot 0.");
Check(ReadUInt64(memory, vtablePage.Address) == original0, "VmtManager.Unhook did not restore slot 0.");

vmt.Hook(0, replacement0);
vmt.Hook(1, replacement1);
Check(ReadUInt64(memory, vtablePage.Address) == replacement0, "VmtManager.Hook did not update slot 0 before Reset.");
Check(ReadUInt64(memory, vtablePage.Address + sizeof(ulong)) == replacement1, "VmtManager.Hook did not update slot 1 before Reset.");
vmt.Reset();
Check(ReadUInt64(memory, vtablePage.Address) == original0, "VmtManager.Reset did not restore slot 0.");
Check(ReadUInt64(memory, vtablePage.Address + sizeof(ulong)) == original1, "VmtManager.Reset did not restore slot 1.");

vmt.Hook(0, replacement0);
((IDisposable)vmt).Dispose();
Check(vmt.IsDisposed, "VmtManager should report disposed after Dispose.");
Check(ReadUInt64(memory, vtablePage.Address) == original0, "VmtManager.Dispose did not restore an active hook.");

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
