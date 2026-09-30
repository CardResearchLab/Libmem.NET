using LibmemCli;

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

static byte[] PointerBytes(ulong value)
{
    return IntPtr.Size == sizeof(ulong)
        ? BitConverter.GetBytes(value)
        : BitConverter.GetBytes(checked((uint)value));
}

static void Stage(string name)
{
    Console.WriteLine($"SMOKE STAGE: {name}");
}

Console.WriteLine("LibmemCli runtime smoke tests");
Stage("exceptions");

var expectedBits = (ulong)(IntPtr.Size * 8);
var expectedArchitecture = IntPtr.Size == sizeof(ulong) ? Architecture.X64 : Architecture.X86;
var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;

Check(typeof(InvalidOperationException).IsAssignableFrom(typeof(LibmemException)),
    "LibmemException must remain compatible with InvalidOperationException catches.");
var exceptionProbe = new LibmemException("LM_Test", "test");
Check(exceptionProbe.Operation == "LM_Test", "LibmemException.Operation did not preserve the native operation name.");

Stage("current-process");
var current = Libmem.CurrentProcess();
Check(current is not null, "CurrentProcess returned null.");
Check(current!.Pid == (uint)Environment.ProcessId, "CurrentProcess PID does not match the test process.");
Check(current.IsAlive(), "Current process should be alive.");

Stage("process-query");
var byPid = Libmem.GetProcess(current.Pid);
Check(byPid is not null && byPid.Pid == current.Pid, "GetProcess could not resolve the current PID.");
var byName = Libmem.FindProcess(current.Name);
Check(byName is not null, "FindProcess could not resolve the current process name.");

var nullProcessName = ExpectThrows<ArgumentNullException>(
    () => Libmem.FindProcess(null!),
    "FindProcess(null) should throw ArgumentNullException.");
Check(nullProcessName.ParamName == "name", "FindProcess(null) reported the wrong parameter name.");

var blankProcessName = ExpectThrows<ArgumentException>(
    () => Libmem.FindProcess("   "),
    "FindProcess(blank) should throw ArgumentException.");
Check(blankProcessName.ParamName == "name", "FindProcess(blank) reported the wrong parameter name.");
var commandLine = Libmem.GetCommandLine(current);
Check(commandLine.Length > 0, "GetCommandLine returned no arguments for the current process.");
Check(Libmem.GetBits() == expectedBits, "Libmem.GetBits does not match the runtime pointer size.");
Check(Libmem.GetSystemBits() >= Libmem.GetBits(), "System bitness is smaller than process bitness.");
Check(Libmem.GetArchitecture() == expectedArchitecture, "Libmem.GetArchitecture does not match the runtime architecture.");

Stage("threads");
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

Stage("session");
var session = Libmem.Attach(current);
Check(session is not null, "Attach(ProcessInfo) returned null for the current process.");
Check(session!.Pid == current.Pid, "ProcessSession PID does not match the attached process.");
Check(session.Architecture == current.Architecture, "ProcessSession architecture does not match.");
Check(session.Bits == current.Bits, "ProcessSession bitness does not match.");
Check(session.IsAlive(), "Attached ProcessSession should report the current process as alive.");
Check(session.Threads is not null, "ProcessSession.Threads returned null.");
Check(session.Scanner is not null, "ProcessSession.Scanner returned null.");
Check(session.Symbols is not null, "ProcessSession.Symbols returned null.");
Check(session.Assembly is not null, "ProcessSession.Assembly returned null.");
Check(session.Assembly.Architecture == current.Architecture,
    "AssemblyManager architecture does not match the session process.");
Check(session.Threads.Enumerate().Any(x => x.Id == currentThread.Id),
    "ThreadManager.Enumerate did not include the current thread.");
Check(session.Threads.Main is not null && session.Threads.Main.OwnerPid == current.Pid,
    "ThreadManager.Main did not resolve a thread owned by the session process.");

var nullModuleName = ExpectThrows<ArgumentNullException>(
    () => session.Modules.Find(null!),
    "ModuleManager.Find(null) should throw ArgumentNullException.");
Check(nullModuleName.ParamName == "name", "ModuleManager.Find(null) reported the wrong parameter name.");

var blankModuleName = ExpectThrows<ArgumentException>(
    () => session.Modules.Find("   "),
    "ModuleManager.Find(blank) should throw ArgumentException.");
Check(blankModuleName.ParamName == "name", "ModuleManager.Find(blank) reported the wrong parameter name.");

var nullModulePath = ExpectThrows<ArgumentNullException>(
    () => session.Modules.Load(null!),
    "ModuleManager.Load(null) should throw ArgumentNullException.");
Check(nullModulePath.ParamName == "path", "ModuleManager.Load(null) reported the wrong parameter name.");

var blankModulePath = ExpectThrows<ArgumentException>(
    () => session.Modules.Load("   "),
    "ModuleManager.Load(blank) should throw ArgumentException.");
Check(blankModulePath.ParamName == "path", "ModuleManager.Load(blank) reported the wrong parameter name.");

var nullInjectionPath = ExpectThrows<ArgumentNullException>(
    () => session.Injector.InjectLibrary(null!),
    "InjectorManager.InjectLibrary(null) should throw ArgumentNullException.");
Check(nullInjectionPath.ParamName == "path", "InjectorManager.InjectLibrary(null) reported the wrong parameter name.");

var emptyDataScan = ExpectThrows<ArgumentException>(
    () => Libmem.DataScan([], 0, 1),
    "DataScan(empty) should throw ArgumentException.");
Check(emptyDataScan.ParamName == "data", "DataScan(empty) reported the wrong parameter name.");

var emptyPatternScan = ExpectThrows<ArgumentException>(
    () => Libmem.PatternScan([], "", 0, 1),
    "PatternScan(empty) should throw ArgumentException.");
Check(emptyPatternScan.ParamName == "pattern", "PatternScan(empty) reported the wrong parameter name.");

var nullPatternMask = ExpectThrows<ArgumentNullException>(
    () => Libmem.PatternScan([0x90], null!, 0, 1),
    "PatternScan(null mask) should throw ArgumentNullException.");
Check(nullPatternMask.ParamName == "mask", "PatternScan(null mask) reported the wrong parameter name.");

var nullSignature = ExpectThrows<ArgumentNullException>(
    () => Libmem.SigScan(null!, 0, 1),
    "SigScan(null) should throw ArgumentNullException.");
Check(nullSignature.ParamName == "signature", "SigScan(null) reported the wrong parameter name.");

var emptyMask = ExpectThrows<ArgumentException>(
    () => Libmem.PatternScan([0x90], "", 0, 1),
    "PatternScan(empty mask) should throw ArgumentException.");
Check(emptyMask.ParamName == "mask", "PatternScan(empty mask) reported the wrong parameter name.");

var emptySignature = ExpectThrows<ArgumentException>(
    () => Libmem.SigScan("", 0, 1),
    "SigScan(empty) should throw ArgumentException.");
Check(emptySignature.ParamName == "signature", "SigScan(empty) reported the wrong parameter name.");

var blankSignature = ExpectThrows<ArgumentException>(
    () => Libmem.SigScan("   ", 0, 1),
    "SigScan(blank) should throw ArgumentException.");
Check(blankSignature.ParamName == "signature", "SigScan(blank) reported the wrong parameter name.");

var nullSymbolName = ExpectThrows<ArgumentNullException>(
    () => Libmem.FindSymbolAddress(session.Modules.Enumerate().First(), null!, false),
    "FindSymbolAddress(null name) should throw ArgumentNullException.");
Check(nullSymbolName.ParamName == "name", "FindSymbolAddress(null name) reported the wrong parameter name.");

var nullDemangleName = ExpectThrows<ArgumentNullException>(
    () => Libmem.DemangleSymbol(null!),
    "DemangleSymbol(null) should throw ArgumentNullException.");
Check(nullDemangleName.ParamName == "name", "DemangleSymbol(null) reported the wrong parameter name.");

var nullAssemblyCode = ExpectThrows<ArgumentNullException>(
    () => Libmem.Assemble(null!),
    "Assemble(null) should throw ArgumentNullException.");
Check(nullAssemblyCode.ParamName == "code", "Assemble(null) reported the wrong parameter name.");

var nulProcessName = ExpectThrows<ArgumentException>(
    () => Libmem.FindProcess("bad\0name"),
    "FindProcess should reject embedded NUL.");
Check(nulProcessName.ParamName == "name", "FindProcess embedded NUL reported the wrong parameter name.");

var nulModuleName = ExpectThrows<ArgumentException>(
    () => session.Modules.Find("bad\0module"),
    "ModuleManager.Find should reject embedded NUL.");
Check(nulModuleName.ParamName == "name", "ModuleManager.Find embedded NUL reported the wrong parameter name.");

var nulModulePath = ExpectThrows<ArgumentException>(
    () => session.Modules.Load("bad\0path.dll"),
    "ModuleManager.Load should reject embedded NUL.");
Check(nulModulePath.ParamName == "path", "ModuleManager.Load embedded NUL reported the wrong parameter name.");

var nulMask = ExpectThrows<ArgumentException>(
    () => Libmem.PatternScan([0x90], "x\0", 0, 1),
    "PatternScan should reject embedded NUL in mask.");
Check(nulMask.ParamName == "mask", "PatternScan embedded NUL reported the wrong parameter name.");

var nulSignature = ExpectThrows<ArgumentException>(
    () => Libmem.SigScan("90\0", 0, 1),
    "SigScan should reject embedded NUL in signature.");
Check(nulSignature.ParamName == "signature", "SigScan embedded NUL reported the wrong parameter name.");

var symbolProbeModule = session.Modules.Enumerate().First();
var nulSymbolName = ExpectThrows<ArgumentException>(
    () => Libmem.FindSymbolAddress(symbolProbeModule, "bad\0symbol", false),
    "FindSymbolAddress should reject embedded NUL.");
Check(nulSymbolName.ParamName == "name", "FindSymbolAddress embedded NUL reported the wrong parameter name.");

var nulDemangleName = ExpectThrows<ArgumentException>(
    () => Libmem.DemangleSymbol("bad\0symbol"),
    "DemangleSymbol should reject embedded NUL.");
Check(nulDemangleName.ParamName == "name", "DemangleSymbol embedded NUL reported the wrong parameter name.");

var nulAssemblyCode = ExpectThrows<ArgumentException>(
    () => Libmem.Assemble("nop\0ret"),
    "Assemble should reject embedded NUL.");
Check(nulAssemblyCode.ParamName == "code", "Assemble embedded NUL reported the wrong parameter name.");

using (var openedSession = ProcessSession.Open(current)
       ?? throw new InvalidOperationException("ProcessSession.Open(ProcessInfo) failed for the current process."))
{
    Check(openedSession.Pid == current.Pid,
        "ProcessSession.Open(ProcessInfo) returned the wrong process.");
}

var sessionSnapshot = session.Info;
Check(sessionSnapshot.Pid == current.Pid && sessionSnapshot.StartTime == current.StartTime,
    "ProcessSession.Info returned the wrong immutable process identity.");

var refreshed = session.Refresh();
Check(refreshed is not null && refreshed.Pid == current.Pid, "ProcessSession.Refresh failed for the current process.");

Stage("session-detach");
var detachedMemory = session.Memory;
var detachedModules = session.Modules;
var detachedThreads = session.Threads;
var detachedScanner = session.Scanner;
var detachedSymbols = session.Symbols;
var detachedAssembly = session.Assembly;
var detachedHooks = session.Hooks;
var detachedInjector = session.Injector;
session.Detach();
session.Detach();
((IDisposable)session).Dispose();
Check(session.IsDisposed, "ProcessSession should remain disposed after repeated Detach/Dispose calls.");

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

var detachedThreadManagerThrows = false;
try
{
    _ = detachedThreads.Enumerate();
}
catch (ObjectDisposedException)
{
    detachedThreadManagerThrows = true;
}
Check(detachedThreadManagerThrows, "ThreadManager should reject operations after its ProcessSession is detached.");

var detachedScanManagerThrows = false;
try
{
    _ = detachedScanner.SigScan("90", 0, 1);
}
catch (ObjectDisposedException)
{
    detachedScanManagerThrows = true;
}
Check(detachedScanManagerThrows, "ScanManager should reject operations after its ProcessSession is detached.");

var detachedSymbolManagerThrows = false;
try
{
    _ = detachedSymbols.Demangle("test");
}
catch (ObjectDisposedException)
{
    detachedSymbolManagerThrows = true;
}
Check(detachedSymbolManagerThrows, "SymbolManager should reject operations after its ProcessSession is detached.");

var detachedAssemblyManagerThrows = false;
try
{
    _ = detachedAssembly.Assemble("nop", 0);
}
catch (ObjectDisposedException)
{
    detachedAssemblyManagerThrows = true;
}
Check(detachedAssemblyManagerThrows, "AssemblyManager should reject operations after its ProcessSession is detached.");

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

Stage("modules");
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

var moduleLoadFailureMapped = false;
try
{
    _ = moduleManager.Load(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"libmemcli-missing-{Guid.NewGuid():N}.dll"));
}
catch (LibmemException ex) when (ex.Operation == "LM_LoadModuleEx")
{
    moduleLoadFailureMapped = true;
}
Check(moduleLoadFailureMapped,
    "ModuleManager.Load should map a definite native load failure to LibmemException.");

Stage("symbols");
ModuleInfo? symbolModule = null;
SymbolInfo? exportedSymbol = null;

foreach (var candidate in sessionModules)
{
    if (candidate is null)
        continue;

    try
    {
        exportedSymbol = pidSession.Symbols.Enumerate(candidate, demangle: false)
            .FirstOrDefault(symbol => symbol is not null
                                      && !string.IsNullOrWhiteSpace(symbol.Name)
                                      && symbol.Address != 0
                                      && symbol.Address != invalidAddress);
    }
    catch (LibmemException)
    {
        // Some runtime modules intentionally expose no enumerable PE symbols.
        continue;
    }

    if (exportedSymbol is not null)
    {
        symbolModule = candidate;
        break;
    }
}

Check(symbolModule is not null && exportedSymbol is not null,
    "No loaded module exposed a usable symbol for symbol API validation.");

var resolvedSymbol = pidSession.Symbols.FindAddress(symbolModule!, exportedSymbol!.Name, demangle: false);
Check(resolvedSymbol == exportedSymbol.Address,
    "SymbolManager.FindAddress disagreed with Enumerate for the selected loaded module.");

// v0.x compatibility for the existing static symbol facade.
Check(Libmem.FindSymbolAddress(symbolModule!, exportedSymbol.Name, demangle: false) == exportedSymbol.Address,
    "Static FindSymbolAddress compatibility API disagreed with SymbolManager.");

Stage("memory-segments");
var memory = pidSession!.Memory;
var scanner = pidSession.Scanner;
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

Stage("memory-read-write-scan");
var invalidManagerProtection = ExpectThrows<ArgumentOutOfRangeException>(
    () => memory.Protect(ownedAllocation.Address, ownedAllocation.Size, (MemoryProtection)0x80),
    "MemoryManager.Protect should reject unsupported protection flags.");
Check(invalidManagerProtection.ParamName == "protection",
    "MemoryManager.Protect reported the wrong parameter name for invalid protection.");

var invalidStaticProtection = ExpectThrows<ArgumentOutOfRangeException>(
    () => Libmem.AllocateMemory(4096, (MemoryProtection)0x80),
    "AllocateMemory should reject unsupported protection flags.");
Check(invalidStaticProtection.ParamName == "prot",
    "AllocateMemory reported the wrong parameter name for invalid protection.");

Check(memory.Set(ownedAllocation.Address, 0xA5, 16) == 16, "MemoryManager.Set failed.");
Check(memory.Read(ownedAllocation.Address, 16).All(x => x == 0xA5),
    "MemoryManager.Set did not fill the requested bytes.");

byte[] ownedPayload = [0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D];
var ownedWritten = memory.Write(ownedAllocation.Address, ownedPayload);
Check(ownedWritten == ownedPayload.Length, "MemoryManager.Write failed.");
var ownedRead = memory.Read(ownedAllocation.Address, ownedPayload.Length);
Check(ownedRead.SequenceEqual(ownedPayload), "MemoryManager.Read returned different data.");

Check(scanner.DataScan(ownedPayload, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.DataScan failed.");
var ownedMask = new string('x', ownedPayload.Length);
Check(scanner.PatternScan(ownedPayload, ownedMask, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.PatternScan failed.");
var ownedSignature = string.Join(" ", ownedPayload.Select(b => b.ToString("X2")));
Check(scanner.SigScan(ownedSignature, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.SigScan failed.");

var noScanRange = scanner.SigScan(ownedSignature, ownedAllocation.Address, 0);
Check(noScanRange == ulong.MaxValue,
    "A valid non-empty signature with zero scan size should remain a normal miss sentinel.");

var missingPayload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
var missingScan = scanner.DataScan(missingPayload, ownedAllocation.Address, ownedAllocation.Size);
Check(missingScan == ulong.MaxValue,
    "A valid non-empty scan with no match should remain the native bad-address sentinel.");

Stage("deep-pointer");
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
    Check(scanner.DeepPointer(pointerLayer0.Address, offsets) == expectedDeepPointer,
        "ScanManager.DeepPointer returned an unexpected address.");
    Check(Libmem.DeepPointer(pointerLayer0.Address, offsets) == expectedDeepPointer,
        "Libmem.DeepPointer returned an unexpected address.");
    Check(Libmem.DeepPointer(current, pointerLayer0.Address, offsets) == expectedDeepPointer,
        "Libmem.DeepPointer(process) returned an unexpected address.");
}

Stage("ownership-protection");
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
((IDisposable)disposeAllocation).Dispose();
Check(disposeAllocation.IsDisposed, "RemoteAllocation should report disposed after repeated Dispose calls.");
Check(disposeAllocation.Free(), "RemoteAllocation.Free should remain idempotent after Dispose.");

Stage("static-enumeration");
var processes = Libmem.EnumProcesses();
Check(processes.Any(p => p.Pid == current.Pid), "EnumProcesses did not include the current process.");

var modules = Libmem.EnumModules();
Check(modules.Count > 0, "EnumModules returned no modules.");
Check(modules.Any(m => m.Base != 0 && m.Size != 0), "EnumModules returned no usable module.");

Stage("static-memory");
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

    Stage("assembly-disassembly");
    var invalidArchitecture = (Architecture)uint.MaxValue;
    var invalidAssembleArchitecture = ExpectThrows<ArgumentOutOfRangeException>(
        () => Libmem.Assemble("nop", invalidArchitecture, 0x1000),
        "Assemble should reject an undefined architecture.");
    Check(invalidAssembleArchitecture.ParamName == "architecture",
        "Assemble reported the wrong parameter name for invalid architecture.");

    var invalidDisassembleArchitecture = ExpectThrows<ArgumentOutOfRangeException>(
        () => Libmem.Disassemble([0x90], invalidArchitecture, 1, 0x1000),
        "Disassemble should reject an undefined architecture.");
    Check(invalidDisassembleArchitecture.ParamName == "architecture",
        "Disassemble reported the wrong parameter name for invalid architecture.");

    var assembly = pidSession.Assembly;
    var singleInstruction = Libmem.Assemble("nop");
    Check(singleInstruction is not null && singleInstruction.Size > 0,
        "Single-instruction Assemble compatibility API returned no instruction.");

    var machineCode = assembly.Assemble("nop; ret", 0x1000);
    Check(machineCode is { Length: > 0 }, "AssemblyManager.Assemble returned no machine code.");

    var assemblyFailureMapped = false;
    try
    {
        _ = assembly.Assemble("definitely_not_a_valid_instruction %%%", 0x1000);
    }
    catch (LibmemException ex) when (ex.Operation == "LM_AssembleEx")
    {
        assemblyFailureMapped = true;
    }
    Check(assemblyFailureMapped,
        "AssemblyManager.Assemble should map a definite native assembly failure to LibmemException.");

    var instructions = assembly.Disassemble(machineCode!, 2, 0x1000);
    Check(instructions.Count > 0, "AssemblyManager.Disassemble(byte[]) returned no instructions.");
    Check(instructions[0].Mnemonic.Length > 0, "Disassembled instruction has no mnemonic.");

    Check(Libmem.WriteMemory(address, machineCode!) == machineCode!.Length,
        "Could not place assembled code in the local allocation.");

    var remoteInstructions = assembly.Disassemble(address, (ulong)machineCode.Length, 2, address);
    Check(remoteInstructions.Count > 0,
        "AssemblyManager.Disassemble(address) returned no target-process instructions.");

    var remoteCodeLength = assembly.CodeLength(address, 1);
    Check(remoteCodeLength >= 1, "AssemblyManager.CodeLength failed for target memory.");

    // v0.x compatibility for existing static assembly/disassembly APIs.
    var localCodeLength = Libmem.CodeLength(address, 1);
    Check(localCodeLength == remoteCodeLength, "Static CodeLength disagreed with AssemblyManager.");
}
finally
{
    Check(Libmem.FreeMemory(address, allocationSize), "FreeMemory failed.");
}

Stage("x86-overflow");
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
