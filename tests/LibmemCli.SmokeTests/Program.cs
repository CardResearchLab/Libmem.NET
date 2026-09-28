using LibmemCli;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static byte[] PointerBytes(ulong value)
{
    return IntPtr.Size == sizeof(ulong)
        ? BitConverter.GetBytes(value)
        : BitConverter.GetBytes(checked((uint)value));
}

Console.WriteLine("LibmemCli runtime smoke tests");

var expectedBits = (ulong)(IntPtr.Size * 8);
var expectedArchitecture = IntPtr.Size == sizeof(ulong) ? Architecture.X64 : Architecture.X86;
var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;

Check(typeof(InvalidOperationException).IsAssignableFrom(typeof(LibmemException)),
    "LibmemException must remain compatible with InvalidOperationException catches.");
var exceptionProbe = new LibmemException("LM_Test", "test");
Check(exceptionProbe.Operation == "LM_Test", "LibmemException.Operation did not preserve the native operation name.");

var current = Libmem.CurrentProcess();
Check(current is not null, "CurrentProcess returned null.");
Check(current!.Pid == (uint)Environment.ProcessId, "CurrentProcess PID does not match the test process.");
Check(current.IsAlive(), "Current process should be alive.");

var byPid = Libmem.GetProcess(current.Pid);
Check(byPid is not null && byPid.Pid == current.Pid, "GetProcess could not resolve the current PID.");
var byName = Libmem.FindProcess(current.Name);
Check(byName is not null, "FindProcess could not resolve the current process name.");
var commandLine = Libmem.GetCommandLine(current);
Check(commandLine.Length > 0, "GetCommandLine returned no arguments for the current process.");
Check(Libmem.GetBits() == expectedBits, "Libmem.GetBits does not match the runtime pointer size.");
Check(Libmem.GetSystemBits() >= Libmem.GetBits(), "System bitness is smaller than process bitness.");
Check(Libmem.GetArchitecture() == expectedArchitecture, "Libmem.GetArchitecture does not match the runtime architecture.");

var currentThread = Libmem.CurrentThread();
Check(currentThread is not null, "CurrentThread returned null.");
Check(currentThread!.OwnerPid == current.Pid, "CurrentThread owner PID does not match the current process.");
var localThreads = Libmem.EnumThreads();
Check(localThreads.Any(x => x.Id == currentThread.Id), "EnumThreads did not include the current thread.");
var processThreads = Libmem.EnumThreads(current);
Check(processThreads.Any(x => x.Id == currentThread.Id), "EnumThreads(process) did not include the current thread.");
var processThread = Libmem.GetThread(current);
Check(processThread is not null && processThread.OwnerPid == current.Pid, "GetThread(process) returned an invalid thread.");
var threadOwner = Libmem.GetThreadProcess(currentThread);
Check(threadOwner is not null && threadOwner.Pid == current.Pid, "GetThreadProcess did not resolve the current process.");

var session = Libmem.Attach(current);
Check(session is not null, "Attach(ProcessInfo) returned null for the current process.");
Check(session!.Pid == current.Pid, "ProcessSession PID does not match the attached process.");
Check(session.Architecture == current.Architecture, "ProcessSession architecture does not match.");
Check(session.Bits == current.Bits, "ProcessSession bitness does not match.");
Check(session.IsAlive(), "Attached ProcessSession should report the current process as alive.");

var sessionSnapshot = session.Info;
sessionSnapshot.Pid = 0;
Check(session.Pid == current.Pid, "Mutating a returned ProcessInfo snapshot changed ProcessSession identity.");

var refreshed = session.Refresh();
Check(refreshed is not null && refreshed.Pid == current.Pid, "ProcessSession.Refresh failed for the current process.");

var detachedMemory = session.Memory;
var detachedModules = session.Modules;
var detachedHooks = session.Hooks;
var detachedInjector = session.Injector;
session.Detach();
Check(session.IsDisposed, "ProcessSession should be disposed after Detach.");

var disposedThrows = false;
try
{
    _ = session.Pid;
}
catch (ObjectDisposedException)
{
    disposedThrows = true;
}
Check(disposedThrows, "ProcessSession members should reject use after Detach.");

var detachedManagerThrows = false;
try
{
    _ = detachedMemory.Read(0, 1);
}
catch (ObjectDisposedException)
{
    detachedManagerThrows = true;
}
Check(detachedManagerThrows, "MemoryManager should reject operations after its ProcessSession is detached.");

var detachedModuleManagerThrows = false;
try
{
    _ = detachedModules.Enumerate();
}
catch (ObjectDisposedException)
{
    detachedModuleManagerThrows = true;
}
Check(detachedModuleManagerThrows, "ModuleManager should reject operations after its ProcessSession is detached.");

var detachedHookManagerThrows = false;
try
{
    _ = detachedHooks.Install(0, 0);
}
catch (ObjectDisposedException)
{
    detachedHookManagerThrows = true;
}
Check(detachedHookManagerThrows, "HookManager should reject operations after its ProcessSession is detached.");

var detachedInjectorThrows = false;
try
{
    _ = detachedInjector.InjectLibrary("not-a-real-library.dll");
}
catch (ObjectDisposedException)
{
    detachedInjectorThrows = true;
}
Check(detachedInjectorThrows, "InjectorManager should reject operations after its ProcessSession is detached.");

using var pidSession = Libmem.Attach((uint)Environment.ProcessId);
Check(pidSession is not null && pidSession.Pid == current.Pid, "Attach(pid) failed for the current process.");

Check(pidSession!.Hooks is not null, "ProcessSession.Hooks returned null.");
Check(pidSession.Injector is not null, "ProcessSession.Injector returned null.");

var moduleManager = pidSession.Modules;
var sessionModules = moduleManager.Enumerate();
Check(sessionModules.Count > 0, "ModuleManager.Enumerate returned no modules.");
var namedModule = sessionModules.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Name));
Check(namedModule is not null, "ModuleManager.Enumerate returned no named module.");
var foundModule = moduleManager.Find(namedModule!.Name);
Check(foundModule is not null, "ModuleManager.Find could not find a module returned by Enumerate.");
Check(foundModule!.Base == namedModule.Base, "ModuleManager.Find returned a different module base.");

var staticModules = Libmem.EnumModules(current);
Check(staticModules.Count > 0, "EnumModules(process) returned no modules.");
var staticFoundModule = Libmem.FindModule(current, namedModule.Name);
Check(staticFoundModule is not null, "FindModule(process, name) could not find a known module.");

var kernel32 = Libmem.FindModule("kernel32.dll");
Check(kernel32 is not null, "kernel32.dll was not found in the Windows test process.");
var kernel32Symbols = Libmem.EnumSymbols(kernel32!, demangle: false);
Check(kernel32Symbols.Count > 0, "EnumSymbols(kernel32.dll) returned no exports.");
var getCurrentProcessId = Libmem.FindSymbolAddress(kernel32!, "GetCurrentProcessId", demangle: false);
Check(getCurrentProcessId != 0 && getCurrentProcessId != invalidAddress,
    "FindSymbolAddress could not resolve GetCurrentProcessId.");

var memory = pidSession!.Memory;
var ownedAllocation = memory.Allocate(4096, MemoryProtection.ReadWrite);
Check(ownedAllocation is not null, "MemoryManager.Allocate returned null.");
Check(ownedAllocation!.Address != 0 && ownedAllocation.Address != invalidAddress, "RemoteAllocation has an invalid address.");
Check(ownedAllocation.Size == 4096, "RemoteAllocation did not preserve its requested size.");

var localSegment = Libmem.FindSegment(ownedAllocation.Address);
Check(localSegment is not null
      && localSegment.Base <= ownedAllocation.Address
      && ownedAllocation.Address < localSegment.End,
    "FindSegment could not resolve the owned allocation.");
var remoteSegment = Libmem.FindSegment(current, ownedAllocation.Address);
Check(remoteSegment is not null
      && remoteSegment.Base <= ownedAllocation.Address
      && ownedAllocation.Address < remoteSegment.End,
    "FindSegment(process, address) could not resolve the owned allocation.");
Check(Libmem.EnumSegments().Any(x => x.Base <= ownedAllocation.Address && ownedAllocation.Address < x.End),
    "EnumSegments did not include the owned allocation.");
Check(Libmem.EnumSegments(current).Any(x => x.Base <= ownedAllocation.Address && ownedAllocation.Address < x.End),
    "EnumSegments(process) did not include the owned allocation.");

Check(memory.Set(ownedAllocation.Address, 0xA5, 16) == 16, "MemoryManager.Set failed.");
Check(memory.Read(ownedAllocation.Address, 16).All(x => x == 0xA5),
    "MemoryManager.Set did not fill the requested bytes.");

byte[] ownedPayload = [0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D];
var ownedWritten = memory.Write(ownedAllocation.Address, ownedPayload);
Check(ownedWritten == ownedPayload.Length, "MemoryManager.Write failed.");
var ownedRead = memory.Read(ownedAllocation.Address, ownedPayload.Length);
Check(ownedRead.SequenceEqual(ownedPayload), "MemoryManager.Read returned different data.");

Check(memory.DataScan(ownedPayload, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "MemoryManager.DataScan failed.");
var ownedMask = new string('x', ownedPayload.Length);
Check(memory.PatternScan(ownedPayload, ownedMask, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "MemoryManager.PatternScan failed.");
var ownedSignature = string.Join(" ", ownedPayload.Select(b => b.ToString("X2")));
Check(memory.SigScan(ownedSignature, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "MemoryManager.SigScan failed.");

using (var pointerLayer0 = memory.Allocate(4096, MemoryProtection.ReadWrite)
       ?? throw new InvalidOperationException("Could not allocate pointer layer 0."))
using (var pointerLayer1 = memory.Allocate(4096, MemoryProtection.ReadWrite)
       ?? throw new InvalidOperationException("Could not allocate pointer layer 1."))
using (var pointerLayer2 = memory.Allocate(4096, MemoryProtection.ReadWrite)
       ?? throw new InvalidOperationException("Could not allocate pointer layer 2."))
{
    Check(memory.Write(pointerLayer0.Address, PointerBytes(pointerLayer1.Address)) == IntPtr.Size,
        "Could not write pointer layer 0.");
    Check(memory.Write(pointerLayer1.Address + 0xA0, PointerBytes(pointerLayer2.Address)) == IntPtr.Size,
        "Could not write pointer layer 1.");

    ulong[] offsets = [0xA0, 0x10];
    var expectedDeepPointer = pointerLayer2.Address + 0x10;
    Check(memory.DeepPointer(pointerLayer0.Address, offsets) == expectedDeepPointer,
        "MemoryManager.DeepPointer returned an unexpected address.");
    Check(Libmem.DeepPointer(pointerLayer0.Address, offsets) == expectedDeepPointer,
        "Libmem.DeepPointer returned an unexpected address.");
    Check(Libmem.DeepPointer(current, pointerLayer0.Address, offsets) == expectedDeepPointer,
        "Libmem.DeepPointer(process) returned an unexpected address.");
}

var ownedOldProtection = memory.Protect(ownedAllocation.Address, ownedAllocation.Size, MemoryProtection.Read);
try
{
    Check(memory.Read(ownedAllocation.Address, ownedPayload.Length).SequenceEqual(ownedPayload),
        "MemoryManager.Read failed after Protect.");
}
finally
{
    memory.Protect(ownedAllocation.Address, ownedAllocation.Size, ownedOldProtection);
}

Check(ownedAllocation.Free(), "RemoteAllocation.Free failed.");
Check(ownedAllocation.IsDisposed, "RemoteAllocation should be disposed after Free.");
Check(ownedAllocation.Free(), "RemoteAllocation.Free should be idempotent.");

var disposeAllocation = memory.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("MemoryManager.Allocate returned null for Dispose coverage.");
((IDisposable)disposeAllocation).Dispose();
Check(disposeAllocation.IsDisposed, "RemoteAllocation should report disposed after successful Dispose.");
Check(disposeAllocation.Free(), "RemoteAllocation.Free should remain idempotent after Dispose.");

var processes = Libmem.EnumProcesses();
Check(processes.Any(p => p.Pid == current.Pid), "EnumProcesses did not include the current process.");

var modules = Libmem.EnumModules();
Check(modules.Count > 0, "EnumModules returned no modules.");
Check(modules.Any(m => m.Base != 0 && m.Size != 0), "EnumModules returned no usable module.");

const ulong allocationSize = 4096;
var address = Libmem.AllocateMemory(allocationSize, MemoryProtection.ReadWrite);
Check(address != 0 && address != invalidAddress, "AllocateMemory failed.");

try
{
    byte[] payload = [0x48, 0x45, 0x41, 0x52, 0x54, 0x48, 0x53, 0x54];
    var written = Libmem.WriteMemory(address, payload);
    Check(written == payload.Length, $"WriteMemory wrote {written} of {payload.Length} bytes.");

    var read = Libmem.ReadMemory(address, payload.Length);
    Check(read.SequenceEqual(payload), "ReadMemory did not return the bytes that were written.");

    Check(Libmem.SetMemory(address + 32, 0x5A, 8) == 8, "SetMemory failed.");
    Check(Libmem.ReadMemory(address + 32, 8).All(x => x == 0x5A), "SetMemory did not fill local memory.");
    Check(Libmem.SetMemory(current, address + 48, 0x6B, 8) == 8, "SetMemory(process) failed.");
    Check(Libmem.ReadMemory(current, address + 48, 8).All(x => x == 0x6B),
        "SetMemory(process) did not fill target memory.");

    var dataMatch = Libmem.DataScan(payload, address, allocationSize);
    Check(dataMatch == address, "DataScan did not find the payload at the allocation base.");

    var mask = new string('x', payload.Length);
    var patternMatch = Libmem.PatternScan(payload, mask, address, allocationSize);
    Check(patternMatch == address, "PatternScan did not find the payload at the allocation base.");

    var signature = string.Join(" ", payload.Select(b => b.ToString("X2")));
    var signatureMatch = Libmem.SigScan(signature, address, allocationSize);
    Check(signatureMatch == address, "SigScan did not find the payload at the allocation base.");

    var oldProtection = Libmem.ProtectMemory(address, allocationSize, MemoryProtection.Read);
    try
    {
        var protectedRead = Libmem.ReadMemory(address, payload.Length);
        Check(protectedRead.SequenceEqual(payload), "ReadMemory failed after changing the allocation to read-only.");
    }
    finally
    {
        Libmem.ProtectMemory(address, allocationSize, oldProtection);
    }

    var singleInstruction = Libmem.Assemble("nop");
    Check(singleInstruction is not null && singleInstruction.Size > 0,
        "Single-instruction Assemble returned no instruction.");

    var machineCode = Libmem.Assemble("nop; ret", expectedArchitecture, 0x1000);
    Check(machineCode is { Length: > 0 }, "Assemble returned no machine code.");

    var instructions = Libmem.Disassemble(machineCode!, Architecture.X64, 2, 0x1000);
    Check(instructions.Count > 0, "Disassemble returned no instructions.");
    Check(instructions[0].Mnemonic.Length > 0, "Disassembled instruction has no mnemonic.");

    Check(Libmem.WriteMemory(address, machineCode!) == machineCode!.Length,
        "Could not place assembled code in the local allocation.");
    var directInstruction = Libmem.Disassemble(address);
    Check(directInstruction is not null && directInstruction.Mnemonic.Length > 0,
        "Direct Disassemble returned no instruction.");
    var localCodeLength = Libmem.CodeLength(address, 1);
    Check(localCodeLength >= 1, "CodeLength failed for local memory.");
    var remoteCodeLength = Libmem.CodeLength(current, address, 1);
    Check(remoteCodeLength == localCodeLength, "CodeLength(process) disagreed with the local result.");
}
finally
{
    Check(Libmem.FreeMemory(address, allocationSize), "FreeMemory failed.");
}

if (IntPtr.Size == sizeof(uint))
{
    var addressOverflowThrows = false;
    try
    {
        _ = Libmem.ReadMemory((ulong)uint.MaxValue + 1UL, 1);
    }
    catch (ArgumentOutOfRangeException)
    {
        addressOverflowThrows = true;
    }
    Check(addressOverflowThrows, "x86 address conversion should reject values above UInt32.MaxValue.");

    var sizeOverflowThrows = false;
    try
    {
        _ = Libmem.AllocateMemory((ulong)uint.MaxValue + 1UL, MemoryProtection.ReadWrite);
    }
    catch (ArgumentOutOfRangeException)
    {
        sizeOverflowThrows = true;
    }
    Check(sizeOverflowThrows, "x86 size conversion should reject values above UInt32.MaxValue.");
}

Console.WriteLine("SMOKE TESTS PASS");
