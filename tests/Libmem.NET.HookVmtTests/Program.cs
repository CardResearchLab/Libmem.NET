using System.Runtime.InteropServices;
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

var badVtable = ExpectThrows<ArgumentOutOfRangeException>(
    () => { _ = new VmtManager(invalidAddress); },
    "VmtManager should reject the bad-address VTable sentinel.");
Check(badVtable.ParamName == "vtableAddress",
    "Bad VTable sentinel reported the wrong parameter name.");

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

// Inspection helper must cover at least the hook's instruction-aligned patch span
// without writing code or changing the existing hook ownership contract.
var restoredAlignedCode = session.Assembly.ReadAlignedCode(source.Address, hook.PatchedBytes);
Check((ulong)restoredAlignedCode.Length >= hook.PatchedBytes,
    "ReadAlignedCode failed to cover the restored HookHandle patch span.");
// The requested instruction-aligned span can be shorter than the entire source function.
// Compare only bytes actually returned, then check the full original function independently.
var comparedBytes = Math.Min(restoredAlignedCode.Length, sourceCode.Length);
Check(restoredAlignedCode.Take(comparedBytes).SequenceEqual(sourceCode.Take(comparedBytes)),
    "ReadAlignedCode returned unexpected restored instruction bytes after Unhook.");
Check(memory.Read(source.Address, sourceCode.Length).SequenceEqual(sourceCode),
    "HookHandle.Remove did not restore all original source function bytes.");
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

// Static local HookCode follows LM_UnhookCode rather than LM_UnhookCodeEx.
// The pinned local native path can report success without restoring bytes if
// the trampoline is unreadable. Preserve ownership and source permissions so
// callers can repair the trampoline and retry deterministic cleanup.
using (var localHook = NativeApi.HookCode(source.Address, destination.Address)
    ?? throw new InvalidOperationException("Could not install static local HookCode fixture."))
{
    Check(localHook.IsInstalled && !localHook.IsDisposed,
        "Static local HookCode should return an active owned handle.");
    Check(CallNoArgs(source.Address) == 2,
        "Static local HookCode did not redirect source.");
    Check(CallNoArgs(localHook.Trampoline) == 1,
        "Static local HookCode trampoline did not preserve the original behavior.");

    var sourceProtection = NativeApi.FindSegment(source.Address)?.Protection
        ?? throw new InvalidOperationException("Could not resolve source protection before local unhook failure.");
    var trampolinePointer = IntPtr.Size == 4
        ? new IntPtr(unchecked((int)localHook.Trampoline))
        : new IntPtr(unchecked((long)localHook.Trampoline));
    Check(VmtFailureNativeMethods.VirtualProtect(
            trampolinePointer, (nuint)1, VmtFailureNativeMethods.PageNoAccess,
            out var priorTrampolineProtection),
        "Could not set PAGE_NOACCESS on the local trampoline test page.");
    try
    {
        Check(memory.Read(localHook.Trampoline, checked((int)localHook.PatchedBytes)).Length == 0,
            "Local trampoline must be unreadable for the failed-unhook fixture.");
        Check(!localHook.Remove(),
            "Static HookHandle.Remove must reject an unreadable local trampoline.");
        Check(localHook.IsInstalled && !localHook.IsDisposed,
            "Failed local Remove must retain active ownership for a retry.");
        Check(CallNoArgs(source.Address) == 2,
            "Failed local Remove must not change the existing source redirection.");
        Check(NativeApi.FindSegment(source.Address)?.Protection == sourceProtection,
            "Failed local Remove must preserve the source page protection.");
    }
    finally
    {
        Check(VmtFailureNativeMethods.VirtualProtect(
                trampolinePointer, (nuint)1, priorTrampolineProtection, out _),
            "Could not restore local trampoline page protection for unhook retry.");
    }

    Check(localHook.Remove(),
        "Local HookHandle.Remove should succeed after trampoline access is restored.");
    Check(!localHook.IsInstalled,
        "Successful retry should clear local HookHandle installation state.");
    Check(CallNoArgs(source.Address) == 1,
        "Successful local Remove retry must restore source behavior.");
    Check(memory.Read(source.Address, sourceCode.Length).SequenceEqual(sourceCode),
        "Successful local Remove retry must restore the source bytes.");
    Check(localHook.Remove(), "Repeated local Remove should remain idempotent.");
}

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

var protectionBeforeUntrackedUnhook = NativeApi.FindSegment(vtablePage.Address)?.Protection
    ?? throw new InvalidOperationException("Could not query VMT page protection before untracked Unhook.");
Check(protectionBeforeUntrackedUnhook == MemoryProtection.ReadWrite,
    "VMT test page should start ReadWrite before untracked Unhook.");

Check(vmt.Unhook(2), "VmtManager.Unhook should be idempotent for an untracked in-range slot.");
Check(ReadPointer(memory, vtablePage.Address + (ulong)(2 * IntPtr.Size)) == original2,
    "Unhooking an untracked slot should not change its value.");

var protectionAfterUntrackedUnhook = NativeApi.FindSegment(vtablePage.Address)?.Protection
    ?? throw new InvalidOperationException("Could not query VMT page protection after untracked Unhook.");
Check(protectionAfterUntrackedUnhook == protectionBeforeUntrackedUnhook,
    "Unhooking an untracked VMT slot must preserve the page protection.");

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

ExpectThrows<ObjectDisposedException>(
    () => vmt.Hook(0, replacement0),
    "Disposed VmtManager.Hook should reject use before validating replacement arguments.");
ExpectThrows<ObjectDisposedException>(
    () => vmt.Unhook(0),
    "Disposed VmtManager.Unhook should reject use.");
ExpectThrows<ObjectDisposedException>(
    () => { _ = vmt.GetOriginal(0); },
    "Disposed VmtManager.GetOriginal should reject use.");
ExpectThrows<ObjectDisposedException>(
    () => vmt.Reset(),
    "Disposed VmtManager.Reset should reject use.");


// VmtManager failure-state ownership and retry lifecycle.
// Free the backing page while a hook is tracked so LM_VmtUnhook cannot change
// its protection. Reset/Dispose must fail without discarding bookkeeping; after
// remapping the same address, both operations must be retryable.
using var failurePage = memory.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Could not allocate VMT failure-state test page.");

const ulong failureOriginal = 0x24681357UL;
const ulong failureReplacement = 0xDEADBEEFUL;
var failureAddress = failurePage.Address;

WritePointer(memory, failureAddress, failureOriginal);
var failureVmt = new VmtManager(failureAddress);
failureVmt.Hook(0, failureReplacement);
Check(ReadPointer(memory, failureAddress) == failureReplacement,
    "VMT failure-state hook did not update the test slot.");

Check(failurePage.Free(), "Could not release the VMT failure-state backing page.");

var resetFailure = ExpectThrows<LibmemException>(
    () => failureVmt.Reset(),
    "VmtManager.Reset should fail when the tracked VTable page no longer exists.");
Check(resetFailure.Operation == "LM_VmtUnhook",
    "VmtManager.Reset reported the wrong native operation for restore failure.");
Check(!failureVmt.IsDisposed,
    "Failed VmtManager.Reset must leave the manager active for retry.");
Check(failureVmt.GetOriginal(0) == failureOriginal,
    "Failed VmtManager.Reset must preserve tracked original metadata.");

var remapped = VmtFailureNativeMethods.VirtualAlloc(
    new IntPtr(unchecked((long)failureAddress)),
    (nuint)4096,
    VmtFailureNativeMethods.MemCommit | VmtFailureNativeMethods.MemReserve,
    VmtFailureNativeMethods.PageReadWrite);
Check(remapped != IntPtr.Zero && unchecked((ulong)remapped.ToInt64()) == failureAddress,
    "Could not remap the VMT failure-state page at its original address.");

try
{
    WritePointer(memory, failureAddress, failureReplacement);
    failureVmt.Reset();
    Check(ReadPointer(memory, failureAddress) == failureOriginal,
        "Retried VmtManager.Reset did not restore the original slot.");
    Check(!failureVmt.IsDisposed,
        "Successful VmtManager.Reset should keep the manager reusable.");

    failureVmt.Hook(0, failureReplacement);
    Check(ReadPointer(memory, failureAddress) == failureReplacement,
        "VmtManager was not reusable after retrying Reset.");

    Check(VmtFailureNativeMethods.VirtualFree(
            remapped,
            0,
            VmtFailureNativeMethods.MemRelease),
        "Could not release the remapped VMT page for Dispose failure coverage.");
    remapped = IntPtr.Zero;

    var disposeFailure = ExpectThrows<LibmemException>(
        () => ((IDisposable)failureVmt).Dispose(),
        "VmtManager.Dispose should surface restore failure while retaining ownership.");
    Check(disposeFailure.Operation == "LM_VmtUnhook",
        "VmtManager.Dispose reported the wrong native operation for restore failure.");
    Check(!failureVmt.IsDisposed,
        "Failed VmtManager.Dispose must leave the manager active for retry.");
    Check(failureVmt.GetOriginal(0) == failureOriginal,
        "Failed VmtManager.Dispose must preserve tracked original metadata.");

    remapped = VmtFailureNativeMethods.VirtualAlloc(
        new IntPtr(unchecked((long)failureAddress)),
        (nuint)4096,
        VmtFailureNativeMethods.MemCommit | VmtFailureNativeMethods.MemReserve,
        VmtFailureNativeMethods.PageReadWrite);
    Check(remapped != IntPtr.Zero && unchecked((ulong)remapped.ToInt64()) == failureAddress,
        "Could not remap the VMT page for Dispose retry.");

    WritePointer(memory, failureAddress, failureReplacement);
    ((IDisposable)failureVmt).Dispose();
    Check(failureVmt.IsDisposed,
        "Retried VmtManager.Dispose should dispose the manager after restoration succeeds.");
    Check(ReadPointer(memory, failureAddress) == failureOriginal,
        "Retried VmtManager.Dispose did not restore the original slot.");
}
finally
{
    if (remapped != IntPtr.Zero)
        VmtFailureNativeMethods.VirtualFree(remapped, 0, VmtFailureNativeMethods.MemRelease);
}

// Verify partial VMT reset when an earlier tracked entry can be restored,
// but a later tracked slot lies on a decommitted page. Reset must retain only
// the still-active entry, and a second attempt must finish after recommit.
var pageSize = Environment.SystemPageSize;
using (var partialPage = memory.Allocate(checked((ulong)pageSize * 2), MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Could not allocate two-page partial VMT reset fixture."))
{
    var distantIndex = checked((ulong)pageSize / (ulong)IntPtr.Size);
    var distantAddress = partialPage.Address + (ulong)pageSize;
    const ulong nearOriginal = 0x13572468UL;
    const ulong farOriginal = 0x24681357UL;
    const ulong nearReplacement = 0xABCDEF01UL;
    const ulong farReplacement = 0xDEADBEEFUL;

    WritePointer(memory, partialPage.Address, nearOriginal);
    WritePointer(memory, distantAddress, farOriginal);
    using var partialVmt = new VmtManager(partialPage.Address);
    // Native entries are prepended: hook distant first, near last, ensuring
    // the near slot is restored before the distant decommitted slot fails.
    partialVmt.Hook(distantIndex, farReplacement);
    partialVmt.Hook(0, nearReplacement);
    Check(ReadPointer(memory, partialPage.Address) == nearReplacement,
        "Could not install near VMT partial-reset fixture.");
    Check(ReadPointer(memory, distantAddress) == farReplacement,
        "Could not install distant VMT partial-reset fixture.");

    var distantPointer = new IntPtr(unchecked((long)distantAddress));
    Check(VmtFailureNativeMethods.VirtualFree(
            distantPointer, (nuint)pageSize, VmtFailureNativeMethods.MemDecommit),
        "Could not decommit distant VMT slot page for partial-reset failure.");

    try
    {
        var partialResetFailure = ExpectThrows<LibmemException>(
            () => partialVmt.Reset(),
            "VmtManager.Reset should fail only after restoring the accessible tracked entry.");
        Check(partialResetFailure.Operation == "LM_VmtUnhook",
            "Partial VMT reset reported the wrong native operation.");
        Check(!partialVmt.IsDisposed,
            "Partial VMT reset failure must retain ownership.");
        Check(ReadPointer(memory, partialPage.Address) == nearOriginal,
            "Partial VMT reset failed to restore the accessible slot first.");
        Check(partialVmt.GetOriginal(0) == nearOriginal,
            "Partial VMT reset must allow querying the restored untracked slot.");
        Check(partialVmt.GetOriginal(distantIndex) == farOriginal,
            "Partial VMT reset must retain the inaccessible tracked slot metadata.");
    }
    finally
    {
        var recommitted = VmtFailureNativeMethods.VirtualAlloc(
            distantPointer, (nuint)pageSize,
            VmtFailureNativeMethods.MemCommit,
            VmtFailureNativeMethods.PageReadWrite);
        Check(recommitted == distantPointer,
            "Could not recommit distant VMT slot page for retry.");
    }

    // MEM_DECOMMIT discards page contents; reproduce the still-installed slot
    // so native unhook can restore its recorded original on a safe retry.
    WritePointer(memory, distantAddress, farReplacement);
    partialVmt.Reset();
    Check(ReadPointer(memory, partialPage.Address) == nearOriginal,
        "Retrying partial VMT reset changed an already-restored slot.");
    Check(ReadPointer(memory, distantAddress) == farOriginal,
        "Retrying partial VMT reset did not restore the remaining tracked slot.");
    partialVmt.Reset();
    Check(!partialVmt.IsDisposed,
        "Successful VMT reset retry must keep the manager reusable.");
}

Console.WriteLine("HOOK/VMT RUNTIME TESTS PASS");


internal static class VmtFailureNativeMethods
{
    internal const uint MemCommit = 0x1000;
    internal const uint MemReserve = 0x2000;
    internal const uint MemDecommit = 0x4000;
    internal const uint MemRelease = 0x8000;
    internal const uint PageNoAccess = 0x01;
    internal const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualProtect(
        IntPtr address, nuint size, uint newProtection, out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAlloc(
        IntPtr address,
        nuint size,
        uint allocationType,
        uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFree(
        IntPtr address,
        nuint size,
        uint freeType);
}
