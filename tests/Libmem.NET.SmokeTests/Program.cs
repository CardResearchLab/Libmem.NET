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

Console.WriteLine("Libmem.NET runtime smoke tests");

var wrapperAssembly = typeof(ProcessSession).Assembly;
if (wrapperAssembly.GetName().Name != "Libmem.NET")
    throw new InvalidOperationException("The wrapper assembly identity must be Libmem.NET.");
foreach (var apiType in new[] { typeof(NativeApi), typeof(ProcessSession), typeof(ProcessInfo),
    typeof(MemoryManager), typeof(HookManager), typeof(VmtManager), typeof(InjectorManager) })
{
    if (apiType.Namespace != "Libmem.NET" || apiType.Assembly != wrapperAssembly)
        throw new InvalidOperationException($"Unexpected public API identity: {apiType.FullName}");
}

Stage("exceptions");

var expectedBits = (ulong)(IntPtr.Size * 8);
var expectedArchitecture = IntPtr.Size == sizeof(ulong) ? Architecture.X64 : Architecture.X86;
var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;

Check(typeof(InvalidOperationException).IsAssignableFrom(typeof(LibmemException)),
    "LibmemException must remain compatible with InvalidOperationException catches.");
var exceptionProbe = new LibmemException("LM_Test", "test");
Check(exceptionProbe.Operation == "LM_Test", "LibmemException.Operation did not preserve the native operation name.");

Stage("current-process");
var current = NativeApi.CurrentProcess();
Check(current is not null, "CurrentProcess returned null.");
Check(current!.Pid == (uint)Environment.ProcessId, "CurrentProcess PID does not match the test process.");
Check(current.IsAlive(), "Current process should be alive.");

Stage("process-query");
var byPid = NativeApi.GetProcess(current.Pid);
Check(byPid is not null && byPid.Pid == current.Pid, "GetProcess could not resolve the current PID.");
var byName = NativeApi.FindProcess(current.Name);
Check(byName is not null, "FindProcess could not resolve the current process name.");

var nullProcessName = ExpectThrows<ArgumentNullException>(
    () => NativeApi.FindProcess(null!),
    "FindProcess(null) should throw ArgumentNullException.");
Check(nullProcessName.ParamName == "name", "FindProcess(null) reported the wrong parameter name.");

var blankProcessName = ExpectThrows<ArgumentException>(
    () => NativeApi.FindProcess("   "),
    "FindProcess(blank) should throw ArgumentException.");
Check(blankProcessName.ParamName == "name", "FindProcess(blank) reported the wrong parameter name.");
var commandLine = NativeApi.GetCommandLine(current);
Check(commandLine.Length > 0, "GetCommandLine returned no arguments for the current process.");
Check(NativeApi.GetBits() == expectedBits, "NativeApi.GetBits does not match the runtime pointer size.");
Check(NativeApi.GetSystemBits() >= NativeApi.GetBits(), "System bitness is smaller than process bitness.");
Check(NativeApi.GetArchitecture() == expectedArchitecture, "NativeApi.GetArchitecture does not match the runtime architecture.");

Stage("threads");
var currentThread = NativeApi.CurrentThread();
Check(currentThread is not null, "CurrentThread returned null.");
Check(currentThread!.OwnerPid == current.Pid, "CurrentThread owner PID does not match the current process.");
var localThreads = NativeApi.EnumThreads();
Check(localThreads.Any(x => x.Id == currentThread.Id), "EnumThreads did not include the current thread.");
var processThreads = NativeApi.EnumThreads(current);
Check(processThreads.Any(x => x.Id == currentThread.Id), "EnumThreads(process) did not include the current thread.");
var processThread = NativeApi.GetThread(current);
Check(processThread is not null && processThread.OwnerPid == current.Pid, "GetThread(process) returned an invalid thread.");
var threadOwner = NativeApi.GetThreadProcess(currentThread);
Check(threadOwner is not null && threadOwner.Pid == current.Pid, "GetThreadProcess did not resolve the current process.");

Stage("session");
var session = NativeApi.Attach(current);
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
    () => NativeApi.DataScan([], 0, 1),
    "DataScan(empty) should throw ArgumentException.");
Check(emptyDataScan.ParamName == "data", "DataScan(empty) reported the wrong parameter name.");

var emptyPatternScan = ExpectThrows<ArgumentException>(
    () => NativeApi.PatternScan([], "", 0, 1),
    "PatternScan(empty) should throw ArgumentException.");
Check(emptyPatternScan.ParamName == "pattern", "PatternScan(empty) reported the wrong parameter name.");

var emptyRemotePatternScan = ExpectThrows<ArgumentException>(
    () => NativeApi.PatternScan(current, [], "", 0, 1),
    "PatternScan(process, empty) should throw ArgumentException.");
Check(emptyRemotePatternScan.ParamName == "pattern",
    "PatternScan(process, empty) reported the wrong parameter name.");

var nullPatternMask = ExpectThrows<ArgumentNullException>(
    () => NativeApi.PatternScan([0x90], null!, 0, 1),
    "PatternScan(null mask) should throw ArgumentNullException.");
Check(nullPatternMask.ParamName == "mask", "PatternScan(null mask) reported the wrong parameter name.");

var nullSignature = ExpectThrows<ArgumentNullException>(
    () => NativeApi.SigScan(null!, 0, 1),
    "SigScan(null) should throw ArgumentNullException.");
Check(nullSignature.ParamName == "signature", "SigScan(null) reported the wrong parameter name.");

var emptyMask = ExpectThrows<ArgumentException>(
    () => NativeApi.PatternScan([0x90], "", 0, 1),
    "PatternScan(empty mask) should throw ArgumentException.");
Check(emptyMask.ParamName == "mask", "PatternScan(empty mask) reported the wrong parameter name.");

var emptyRemoteMask = ExpectThrows<ArgumentException>(
    () => NativeApi.PatternScan(current, [0x90], "", 0, 1),
    "PatternScan(process, empty mask) should throw ArgumentException.");
Check(emptyRemoteMask.ParamName == "mask",
    "PatternScan(process, empty mask) reported the wrong parameter name.");

var emptySignature = ExpectThrows<ArgumentException>(
    () => NativeApi.SigScan("", 0, 1),
    "SigScan(empty) should throw ArgumentException.");
Check(emptySignature.ParamName == "signature", "SigScan(empty) reported the wrong parameter name.");

var blankSignature = ExpectThrows<ArgumentException>(
    () => NativeApi.SigScan("   ", 0, 1),
    "SigScan(blank) should throw ArgumentException.");
Check(blankSignature.ParamName == "signature", "SigScan(blank) reported the wrong parameter name.");

var nullSymbolName = ExpectThrows<ArgumentNullException>(
    () => NativeApi.FindSymbolAddress(session.Modules.Enumerate().First(), null!, false),
    "FindSymbolAddress(null name) should throw ArgumentNullException.");
Check(nullSymbolName.ParamName == "name", "FindSymbolAddress(null name) reported the wrong parameter name.");

var nullDemangleName = ExpectThrows<ArgumentNullException>(
    () => NativeApi.DemangleSymbol(null!),
    "DemangleSymbol(null) should throw ArgumentNullException.");
Check(nullDemangleName.ParamName == "name", "DemangleSymbol(null) reported the wrong parameter name.");

var nullAssemblyCode = ExpectThrows<ArgumentNullException>(
    () => NativeApi.Assemble(null!),
    "Assemble(null) should throw ArgumentNullException.");
Check(nullAssemblyCode.ParamName == "code", "Assemble(null) reported the wrong parameter name.");

var nulProcessName = ExpectThrows<ArgumentException>(
    () => NativeApi.FindProcess("bad\0name"),
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
    () => NativeApi.PatternScan([0x90], "x\0", 0, 1),
    "PatternScan should reject embedded NUL in mask.");
Check(nulMask.ParamName == "mask", "PatternScan embedded NUL reported the wrong parameter name.");

var nulSignature = ExpectThrows<ArgumentException>(
    () => NativeApi.SigScan("90\0", 0, 1),
    "SigScan should reject embedded NUL in signature.");
Check(nulSignature.ParamName == "signature", "SigScan embedded NUL reported the wrong parameter name.");

var symbolProbeModule = session.Modules.Enumerate().First();
var nulSymbolName = ExpectThrows<ArgumentException>(
    () => NativeApi.FindSymbolAddress(symbolProbeModule, "bad\0symbol", false),
    "FindSymbolAddress should reject embedded NUL.");
Check(nulSymbolName.ParamName == "name", "FindSymbolAddress embedded NUL reported the wrong parameter name.");

var nulDemangleName = ExpectThrows<ArgumentException>(
    () => NativeApi.DemangleSymbol("bad\0symbol"),
    "DemangleSymbol should reject embedded NUL.");
Check(nulDemangleName.ParamName == "name", "DemangleSymbol embedded NUL reported the wrong parameter name.");

var nulAssemblyCode = ExpectThrows<ArgumentException>(
    () => NativeApi.Assemble("nop\0ret"),
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
using var pidSession = NativeApi.Attach((uint)Environment.ProcessId);
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

var staticModules = NativeApi.EnumModules(current);
Check(staticModules.Count > 0, "EnumModules(process) returned no modules.");
var staticFoundModule = NativeApi.FindModule(current, namedModule.Name);
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

var missingStaticModulePath = System.IO.Path.Combine(
    System.IO.Path.GetTempPath(),
    $"libmemcli-static-missing-{Guid.NewGuid():N}.dll");
Check(NativeApi.LoadModule(current, missingStaticModulePath) is null,
    "Static LoadModule(process) should preserve null on native load failure.");

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
Check(pidSession.Symbols.TryFindAddress(symbolModule!, exportedSymbol!.Name, demangle: false, out var tryResolvedSymbol)
      && tryResolvedSymbol == resolvedSymbol,
    "SymbolManager.TryFindAddress did not resolve an existing symbol.");

// v0.x compatibility for the existing static symbol facade.
Check(NativeApi.FindSymbolAddress(symbolModule!, exportedSymbol.Name, demangle: false) == exportedSymbol.Address,
    "Static FindSymbolAddress compatibility API disagreed with SymbolManager.");

var missingSymbolName = $"__libmemcli_missing_symbol_{Guid.NewGuid():N}";
Check(pidSession.Symbols.FindAddress(symbolModule!, missingSymbolName, demangle: false) == invalidAddress,
    "SymbolManager.FindAddress miss should preserve the native bad-address sentinel.");
Check(!pidSession.Symbols.TryFindAddress(symbolModule!, missingSymbolName, demangle: false, out var missingSymbolAddress)
      && missingSymbolAddress == 0,
    "SymbolManager.TryFindAddress should return false/zero for a normal symbol miss.");

var nullTrySymbolModule = ExpectThrows<ArgumentNullException>(
    () => pidSession.Symbols.TryFindAddress(null!, "unused", demangle: false, out _),
    "SymbolManager.TryFindAddress should reject a null module.");
Check(nullTrySymbolModule.ParamName == "module",
    "TryFindAddress(null module) reported the wrong parameter name.");
var nullTrySymbolName = ExpectThrows<ArgumentNullException>(
    () => pidSession.Symbols.TryFindAddress(symbolModule!, null!, demangle: false, out _),
    "SymbolManager.TryFindAddress should reject a null name.");
Check(nullTrySymbolName.ParamName == "name",
    "TryFindAddress(null name) reported the wrong parameter name.");
Check(NativeApi.FindSymbolAddress(symbolModule!, missingSymbolName, demangle: false) == invalidAddress,
    "Static FindSymbolAddress miss should preserve the native bad-address sentinel.");

Stage("memory-segments");
var memory = pidSession!.Memory;
var scanner = pidSession.Scanner;
var ownedAllocation = memory.Allocate(4096, MemoryProtection.ReadWrite);
Check(ownedAllocation is not null, "MemoryManager.Allocate returned null.");
Check(ownedAllocation!.Address != 0 && ownedAllocation.Address != invalidAddress, "RemoteAllocation has an invalid address.");
Check(ownedAllocation.Size == 4096, "RemoteAllocation did not preserve its requested size.");

var zeroOwnedAllocation = ExpectThrows<ArgumentOutOfRangeException>(
    () => memory.Allocate(0, MemoryProtection.ReadWrite),
    "MemoryManager.Allocate(0) should reject a zero-sized owned allocation.");
Check(zeroOwnedAllocation.ParamName == "size",
    "MemoryManager.Allocate(0) reported the wrong parameter name.");

var localSegment = NativeApi.FindSegment(ownedAllocation.Address);
Check(localSegment is not null
      && localSegment.Base <= ownedAllocation.Address
      && ownedAllocation.Address < localSegment.End,
    "FindSegment could not resolve the owned allocation.");
var remoteSegment = NativeApi.FindSegment(current, ownedAllocation.Address);
Check(remoteSegment is not null
      && remoteSegment.Base <= ownedAllocation.Address
      && ownedAllocation.Address < remoteSegment.End,
    "FindSegment(process, address) could not resolve the owned allocation.");
Check(NativeApi.FindSegment(invalidAddress) is null,
    "FindSegment miss should return null.");
Check(NativeApi.FindSegment(current, invalidAddress) is null,
    "FindSegment(process) miss should return null.");
Check(NativeApi.EnumSegments().Any(x => x.Base <= ownedAllocation.Address && ownedAllocation.Address < x.End),
    "EnumSegments did not include the owned allocation.");
Check(NativeApi.EnumSegments(current).Any(x => x.Base <= ownedAllocation.Address && ownedAllocation.Address < x.End),
    "EnumSegments(process) did not include the owned allocation.");

Stage("memory-read-write-scan");
var invalidManagerProtection = ExpectThrows<ArgumentOutOfRangeException>(
    () => memory.Protect(ownedAllocation.Address, ownedAllocation.Size, (MemoryProtection)0x80),
    "MemoryManager.Protect should reject unsupported protection flags.");
Check(invalidManagerProtection.ParamName == "protection",
    "MemoryManager.Protect reported the wrong parameter name for invalid protection.");

var invalidStaticProtection = ExpectThrows<ArgumentOutOfRangeException>(
    () => NativeApi.AllocateMemory(4096, (MemoryProtection)0x80),
    "AllocateMemory should reject unsupported protection flags.");
Check(invalidStaticProtection.ParamName == "prot",
    "AllocateMemory reported the wrong parameter name for invalid protection.");

Check(memory.Read(ownedAllocation.Address, 0).Length == 0,
    "MemoryManager.Read(count=0) should return an empty array.");
Check(memory.Write(ownedAllocation.Address, []) == 0,
    "MemoryManager.Write(empty) should be a zero-byte no-op.");
Check(memory.Set(ownedAllocation.Address, 0xA5, 0) == 0,
    "MemoryManager.Set(size=0) should be a zero-byte no-op.");
var zeroProtectOld = memory.Protect(ownedAllocation.Address, 0, MemoryProtection.ReadWrite);
_ = memory.Protect(ownedAllocation.Address, 0, zeroProtectOld);

Check(memory.Set(ownedAllocation.Address, 0xA5, 16) == 16, "MemoryManager.Set failed.");
Check(memory.Read(ownedAllocation.Address, 16).All(x => x == 0xA5),
    "MemoryManager.Set did not fill the requested bytes.");

byte[] ownedPayload = [0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D];
var ownedWritten = memory.Write(ownedAllocation.Address, ownedPayload);
Check(ownedWritten == ownedPayload.Length, "MemoryManager.Write failed.");
var ownedRead = memory.Read(ownedAllocation.Address, ownedPayload.Length);
Check(ownedRead.SequenceEqual(ownedPayload), "MemoryManager.Read returned different data.");

// Typed reads/writes share Read/Write's partial-I/O contract but require a complete value.
const long signedValue = -0x102030405060708L;
memory.WriteInt64(ownedAllocation.Address + 64, signedValue);
Check(memory.ReadInt64(ownedAllocation.Address + 64) == signedValue,
    "MemoryManager.ReadInt64 / WriteInt64 round trip failed.");
const ulong pointerValue = 0x12345678UL;
memory.WritePointer(ownedAllocation.Address + 80, pointerValue);
Check(memory.ReadPointer(ownedAllocation.Address + 80) == pointerValue,
    "MemoryManager.ReadPointer / WritePointer round trip failed.");
Check(memory.Read(ownedAllocation.Address + 80, IntPtr.Size).SequenceEqual(PointerBytes(pointerValue)),
    "MemoryManager.WritePointer ignored the target architecture width.");
if (IntPtr.Size == sizeof(uint))
{
    var pointerOverflow = ExpectThrows<ArgumentOutOfRangeException>(
        () => memory.WritePointer(ownedAllocation.Address + 80, (ulong)uint.MaxValue + 1),
        "WritePointer must reject values that cannot fit a 32-bit process.");
    Check(pointerOverflow.ParamName == "value",
        "WritePointer overflow reported the wrong parameter name.");
}

Check(scanner.DataScan(ownedPayload, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.DataScan failed.");
var ownedMask = new string('x', ownedPayload.Length);
Check(scanner.PatternScan(ownedPayload, ownedMask, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.PatternScan failed.");
var ownedSignature = string.Join(" ", ownedPayload.Select(b => b.ToString("X2")));
Check(scanner.SigScan(ownedSignature, ownedAllocation.Address, ownedAllocation.Size) == ownedAllocation.Address,
    "ScanManager.SigScan failed.");

// The pinned libmem scan loop omits the final legal candidate. Exact-window
// scans therefore exercise the wrapper compatibility fallback directly.
var exactScanSize = (ulong)ownedPayload.Length;
Check(NativeApi.DataScan(ownedPayload, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Static DataScan should match when the scan window exactly equals the pattern length.");
Check(NativeApi.PatternScan(ownedPayload, ownedMask, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Static PatternScan should match when the scan window exactly equals the pattern length.");
Check(NativeApi.SigScan(ownedSignature, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Static SigScan should match when the scan window exactly equals the signature length.");
Check(scanner.DataScan(ownedPayload, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Remote DataScan should recover the pinned final-candidate omission.");
Check(scanner.PatternScan(ownedPayload, ownedMask, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Remote PatternScan should recover the pinned final-candidate omission.");
Check(scanner.SigScan(ownedSignature, ownedAllocation.Address, exactScanSize) == ownedAllocation.Address,
    "Remote SigScan should recover the pinned final-candidate omission.");

var wrappingScan = ExpectThrows<ArgumentOutOfRangeException>(
    () => NativeApi.DataScan([0x90], invalidAddress - 1, 4),
    "Scan ranges that wrap the architecture-width address space should be rejected.");
Check(wrappingScan.ParamName == "scanSize",
    "Wrapping scan range reported the wrong parameter name.");

var noScanRange = scanner.SigScan(ownedSignature, ownedAllocation.Address, 0);
Check(noScanRange == invalidAddress,
    "A valid non-empty signature with zero scan size should remain the architecture-width native miss sentinel.");

var missingPayload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
var missingScan = scanner.DataScan(missingPayload, ownedAllocation.Address, ownedAllocation.Size);
Check(missingScan == invalidAddress,
    "A valid non-empty scan with no match should remain the architecture-width native bad-address sentinel.");

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
    Check(NativeApi.DeepPointer(pointerLayer0.Address, offsets) == expectedDeepPointer,
        "NativeApi.DeepPointer returned an unexpected address.");
    Check(NativeApi.DeepPointer(current, pointerLayer0.Address, offsets) == expectedDeepPointer,
        "NativeApi.DeepPointer(process) returned an unexpected address.");
    Check(scanner.DeepPointer(pointerLayer0.Address, []) == invalidAddress,
        "ScanManager.DeepPointer(empty offsets) should preserve the native bad-address sentinel.");
    Check(NativeApi.DeepPointer(pointerLayer0.Address, []) == invalidAddress,
        "Static DeepPointer(empty offsets) should preserve the native bad-address sentinel.");
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
var processes = NativeApi.EnumProcesses();
Check(processes.Any(p => p.Pid == current.Pid), "EnumProcesses did not include the current process.");

var modules = NativeApi.EnumModules();
Check(modules.Count > 0, "EnumModules returned no modules.");
Check(modules.Any(m => m.Base != 0 && m.Size != 0), "EnumModules returned no usable module.");

Stage("static-memory");
var zeroSizedNativeAllocation = NativeApi.AllocateMemory(0, MemoryProtection.ReadWrite);
Check(zeroSizedNativeAllocation != 0 && zeroSizedNativeAllocation != invalidAddress,
    "AllocateMemory(0) should preserve the pinned Windows libmem page-allocation contract.");
Check(NativeApi.FreeMemory(zeroSizedNativeAllocation, 0),
    "FreeMemory should release the zero-size-request allocation.");

const ulong allocationSize = 4096;
var address = NativeApi.AllocateMemory(allocationSize, MemoryProtection.ReadWrite);
Check(address != 0 && address != invalidAddress, "AllocateMemory failed.");

try
{
    byte[] payload = [0x48, 0x45, 0x41, 0x52, 0x54, 0x48, 0x53, 0x54];
    var written = NativeApi.WriteMemory(address, payload);
    Check(written == payload.Length, $"WriteMemory wrote {written} of {payload.Length} bytes.");

    var read = NativeApi.ReadMemory(address, payload.Length);
    Check(read.SequenceEqual(payload), "ReadMemory did not return the bytes that were written.");

    Check(NativeApi.ReadMemory(address, 0).Length == 0,
        "ReadMemory(count=0) should return an empty array.");
    Check(NativeApi.WriteMemory(address, []) == 0,
        "WriteMemory(empty) should be a zero-byte no-op.");
    Check(NativeApi.SetMemory(address, 0x5A, 0) == 0,
        "SetMemory(size=0) should be a zero-byte no-op.");
    var zeroStaticProtectOld = NativeApi.ProtectMemory(address, 0, MemoryProtection.ReadWrite);
    _ = NativeApi.ProtectMemory(address, 0, zeroStaticProtectOld);

    Check(NativeApi.SetMemory(address + 32, 0x5A, 8) == 8, "SetMemory failed.");
    Check(NativeApi.ReadMemory(address + 32, 8).All(x => x == 0x5A), "SetMemory did not fill local memory.");
    Check(NativeApi.SetMemory(current, address + 48, 0x6B, 8) == 8, "SetMemory(process) failed.");
    Check(NativeApi.ReadMemory(current, address + 48, 8).All(x => x == 0x6B),
        "SetMemory(process) did not fill target memory.");

    var dataMatch = NativeApi.DataScan(payload, address, allocationSize);
    Check(dataMatch == address, "DataScan did not find the payload at the allocation base.");

    var mask = new string('x', payload.Length);
    var patternMatch = NativeApi.PatternScan(payload, mask, address, allocationSize);
    Check(patternMatch == address, "PatternScan did not find the payload at the allocation base.");

    var signature = string.Join(" ", payload.Select(b => b.ToString("X2")));
    var signatureMatch = NativeApi.SigScan(signature, address, allocationSize);
    Check(signatureMatch == address, "SigScan did not find the payload at the allocation base.");

    var oldProtection = NativeApi.ProtectMemory(address, allocationSize, MemoryProtection.Read);
    try
    {
        var protectedRead = NativeApi.ReadMemory(address, payload.Length);
        Check(protectedRead.SequenceEqual(payload), "ReadMemory failed after changing the allocation to read-only.");
    }
    finally
    {
        NativeApi.ProtectMemory(address, allocationSize, oldProtection);
    }

    Stage("assembly-disassembly");
    var invalidArchitecture = (Architecture)uint.MaxValue;
    var invalidAssembleArchitecture = ExpectThrows<ArgumentOutOfRangeException>(
        () => NativeApi.Assemble("nop", invalidArchitecture, 0x1000),
        "Assemble should reject an undefined architecture.");
    Check(invalidAssembleArchitecture.ParamName == "architecture",
        "Assemble reported the wrong parameter name for invalid architecture.");

    var invalidDisassembleArchitecture = ExpectThrows<ArgumentOutOfRangeException>(
        () => NativeApi.Disassemble([0x90], invalidArchitecture, 1, 0x1000),
        "Disassemble should reject an undefined architecture.");
    Check(invalidDisassembleArchitecture.ParamName == "architecture",
        "Disassemble reported the wrong parameter name for invalid architecture.");

    var assembly = pidSession.Assembly;
    Check(assembly.Disassemble([], 0, 0).Count == 0,
        "AssemblyManager.Disassemble(empty) should return an empty list.");
    Check(assembly.CodeLength(address, 0) == 0,
        "AssemblyManager.CodeLength(minimumLength=0) should return 0.");
    Check(NativeApi.CodeLength(address, 0) == 0,
        "Static CodeLength(minimumLength=0) should return 0.");

    var singleInstruction = NativeApi.Assemble("nop");
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

    Check(NativeApi.WriteMemory(address, machineCode!) == machineCode!.Length,
        "Could not place assembled code in the local allocation.");

    var remoteInstructions = assembly.Disassemble(address, (ulong)machineCode.Length, 2, address);
    Check(remoteInstructions.Count > 0,
        "AssemblyManager.Disassemble(address) returned no target-process instructions.");

    var remoteCodeLength = assembly.CodeLength(address, 1);
    Check(remoteCodeLength >= 1, "AssemblyManager.CodeLength failed for target memory.");
    Check(assembly.ReadAlignedCode(address, 0).Length == 0,
        "ReadAlignedCode with zero minimum should return an empty array.");
    var alignedCode = assembly.ReadAlignedCode(address, 1);
    Check(alignedCode.Length == (int)remoteCodeLength
          && alignedCode[0] == machineCode[0],
        "ReadAlignedCode did not return complete instructions from the target.");

    var oversizedAlignedRead = ExpectThrows<ArgumentOutOfRangeException>(
        () => assembly.ReadAlignedCode(address, (ulong)int.MaxValue + 1UL),
        "ReadAlignedCode should reject requests beyond managed array capacity.");
    Check(oversizedAlignedRead.ParamName == "minimumLength",
        "ReadAlignedCode oversized request reported the wrong parameter name.");

    // v0.x compatibility for existing static assembly/disassembly APIs.
    var localCodeLength = NativeApi.CodeLength(address, 1);
    Check(localCodeLength == remoteCodeLength, "Static CodeLength disagreed with AssemblyManager.");
}
finally
{
    Check(NativeApi.FreeMemory(address, allocationSize), "FreeMemory failed.");
}

Stage("assembly-page-boundary");
{
    // A complete one-byte instruction at the final readable page byte must not
    // require libmem to read a full decoder window into the following no-access page.
    var pageSize = (ulong)Environment.SystemPageSize;
    var guardedSize = pageSize * 2UL;
    var guardedAddress = NativeApi.AllocateMemory(guardedSize, MemoryProtection.ReadWrite);
    Check(guardedAddress != 0 && guardedAddress != invalidAddress,
        "Could not allocate guard-page instruction fixture.");
    try
    {
        var lastReadableByte = guardedAddress + pageSize - 1UL;
        Check(NativeApi.WriteMemory(lastReadableByte, [0x90]) == 1,
            "Could not write the final readable NOP fixture.");
        var oldGuardProtection = NativeApi.ProtectMemory(
            guardedAddress + pageSize, pageSize, MemoryProtection.None);
        try
        {
            var lastInstruction = pidSession.Assembly.ReadAlignedCode(lastReadableByte, 1);
            Check(lastInstruction.SequenceEqual(new byte[] { 0x90 }),
                "ReadAlignedCode must decode a complete NOP at the page boundary.");

            _ = ExpectThrows<LibmemException>(
                () => pidSession.Assembly.ReadAlignedCode(lastReadableByte, 2),
                "ReadAlignedCode must reject a requested span extending into a no-access page.");
        }
        finally
        {
            _ = NativeApi.ProtectMemory(guardedAddress + pageSize, pageSize, oldGuardProtection);
        }
    }
    finally
    {
        Check(NativeApi.FreeMemory(guardedAddress, guardedSize),
            "Could not free guard-page instruction fixture.");
    }
}

Stage("x86-overflow");
if (IntPtr.Size == sizeof(uint))
{
    var addressOverflowThrows = false;
    try
    {
        _ = NativeApi.ReadMemory((ulong)uint.MaxValue + 1UL, 1);
    }
    catch (ArgumentOutOfRangeException)
    {
        addressOverflowThrows = true;
    }
    Check(addressOverflowThrows, "x86 address conversion should reject values above UInt32.MaxValue.");

    var sizeOverflowThrows = false;
    try
    {
        _ = NativeApi.AllocateMemory((ulong)uint.MaxValue + 1UL, MemoryProtection.ReadWrite);
    }
    catch (ArgumentOutOfRangeException)
    {
        sizeOverflowThrows = true;
    }
    Check(sizeOverflowThrows, "x86 size conversion should reject values above UInt32.MaxValue.");
}

Console.WriteLine("SMOKE TESTS PASS");
