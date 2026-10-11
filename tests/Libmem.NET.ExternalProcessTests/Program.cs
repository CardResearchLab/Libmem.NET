using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;
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

    throw new Exception(message);
}

static string CurrentPlatform()
{
    return RuntimeInformation.ProcessArchitecture switch
    {
        RuntimeArchitecture.X86 => "x86",
        RuntimeArchitecture.X64 => "x64",
        RuntimeArchitecture.Arm64 => "arm64",
        var architecture => throw new PlatformNotSupportedException(
            $"External-process tests do not support {architecture}.")
    };
}

static string ResolveTargetPath()
{
    var configured = Environment.GetEnvironmentVariable("LIBMEM_NET_TEST_TARGET")
        ?? Environment.GetEnvironmentVariable("LIBMEM_NET_TEST_TARGET_DLL");
    if (!string.IsNullOrWhiteSpace(configured))
        return Path.GetFullPath(configured);

    var platform = CurrentPlatform();
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var output = Path.Combine(repoRoot, "Libmem.NET.TestTarget", "bin", platform, "Release", "net8.0");
    var candidates = new[]
    {
        Path.Combine(output, "Libmem.NET.TestTarget.exe"),
        Path.Combine(output, "Libmem.NET.TestTarget.dll"),
    };

    return candidates.FirstOrDefault(File.Exists)
        ?? throw new FileNotFoundException(
            $"Libmem.NET.TestTarget was not found for {platform}. Build tests/Libmem.NET.TestTarget first or set LIBMEM_NET_TEST_TARGET.");
}

static ProcessStartInfo CreateTargetStartInfo(string targetPath)
{
    var isDll = string.Equals(Path.GetExtension(targetPath), ".dll", StringComparison.OrdinalIgnoreCase);
    var fileName = targetPath;

    if (isDll)
    {
        fileName = "dotnet";
        if (RuntimeInformation.ProcessArchitecture == RuntimeArchitecture.X86)
        {
            var x86Root = Environment.GetEnvironmentVariable("DOTNET_ROOT_X86");
            if (!string.IsNullOrWhiteSpace(x86Root))
            {
                var x86Host = Path.Combine(x86Root, "dotnet.exe");
                if (File.Exists(x86Host))
                    fileName = x86Host;
            }
        }
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        CreateNoWindow = true,
    };

    if (isDll)
        startInfo.ArgumentList.Add(targetPath);

    return startInfo;
}

static (uint Pid, string Architecture, ulong Address, ulong Size, ulong HookSource, ulong HookDestination) ParseReady(string line)
{
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2 || parts[0] != "READY")
        throw new InvalidOperationException($"Unexpected TestTarget handshake: {line}");

    var fields = parts[1..]
        .Select(part => part.Split('=', 2))
        .Where(pair => pair.Length == 2)
        .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);

    string Field(string name) =>
        fields.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException($"Handshake is missing {name}: {line}");

    static ulong HexAddress(string value, string name)
    {
        if (!value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected {name} in handshake: {value}");
        return ulong.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    uint pid = uint.Parse(Field("pid"), CultureInfo.InvariantCulture);
    string architecture = Field("arch");
    ulong address = HexAddress(Field("address"), "address");
    ulong size = ulong.Parse(Field("size"), CultureInfo.InvariantCulture);
    ulong hookSource = HexAddress(Field("hookSource"), "hookSource");
    ulong hookDestination = HexAddress(Field("hookDestination"), "hookDestination");
    return (pid, architecture, address, size, hookSource, hookDestination);
}

static string SendCommand(Process child, string command)
{
    child.StandardInput.WriteLine(command);
    child.StandardInput.Flush();
    return child.StandardOutput.ReadLine()
        ?? throw new InvalidOperationException($"TestTarget returned no response for command: {command}");
}

static int CallTarget(Process child, ulong address)
{
    var response = SendCommand(child, $"call 0x{address:X}");
    const string prefix = "RESULT ";
    if (!response.StartsWith(prefix, StringComparison.Ordinal))
        throw new InvalidOperationException($"Unexpected TestTarget call response: {response}");

    return int.Parse(response[prefix.Length..], CultureInfo.InvariantCulture);
}

static uint SetTargetProtection(Process child, ulong address, uint protection)
{
    var response = SendCommand(child, $"protect 0x{address:X} 0x{protection:X}");
    const string prefix = "PROTECT old=0x";
    if (!response.StartsWith(prefix, StringComparison.Ordinal))
        throw new InvalidOperationException($"Unexpected TestTarget protect response: {response}");

    return uint.Parse(
        response[prefix.Length..],
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);
}

static MemoryProtection ProtectionOf(ProcessInfo process, ulong address) =>
    NativeApi.FindSegment(process, address)?.Protection
    ?? throw new InvalidOperationException($"Could not resolve protection for 0x{address:X}.");

static string CreateLibraryFixture(string directory, string source, string suffix)
{
    var path = Path.Combine(directory, $"Libmem.NET.RemoteFixture.{suffix}.{Guid.NewGuid():N}.dll");
    File.Copy(source, path, overwrite: false);
    return path;
}

Console.WriteLine("Libmem.NET external-process runtime tests");

var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;
var targetPath = ResolveTargetPath();
var startInfo = CreateTargetStartInfo(targetPath);

using var child = Process.Start(startInfo)
    ?? throw new InvalidOperationException("Could not start Libmem.NET.TestTarget.");

var fixtureDirectory = Path.Combine(
    Path.GetTempPath(), "Libmem.NET.ExternalProcessTests", Guid.NewGuid().ToString("N"));

try
{
    var readyLine = child.StandardOutput.ReadLine()
        ?? throw new InvalidOperationException(
            $"TestTarget exited before handshake. stderr: {child.StandardError.ReadToEnd()}");

    var ready = ParseReady(readyLine);
    Check(ready.Pid == (uint)child.Id, "Handshake PID does not match the launched child process.");
    Check(string.Equals(ready.Architecture, RuntimeInformation.ProcessArchitecture.ToString(), StringComparison.OrdinalIgnoreCase),
        "TestTarget architecture does not match the external-process test architecture.");
    Check(ready.Address != 0 && ready.Size >= 64, "TestTarget returned an invalid allocation.");
    Check(ready.HookSource != 0 && ready.HookDestination != 0,
        "TestTarget returned an invalid Hook function address.");
    Check(ready.HookSource != ready.HookDestination,
        "TestTarget Hook source and destination must be distinct.");

    var process = NativeApi.GetProcess(ready.Pid);
    Check(process is not null, "NativeApi.GetProcess could not resolve the TestTarget process.");

    var processInfoType = typeof(ProcessInfo);
    Check(processInfoType.GetConstructor(Type.EmptyTypes) is null,
        "ProcessInfo must not expose a public parameterless constructor.");
    foreach (var propertyName in new[]
    {
        nameof(ProcessInfo.Pid),
        nameof(ProcessInfo.ParentPid),
        nameof(ProcessInfo.Architecture),
        nameof(ProcessInfo.Bits),
        nameof(ProcessInfo.StartTime),
        nameof(ProcessInfo.Name),
        nameof(ProcessInfo.Path),
    })
    {
        var property = processInfoType.GetProperty(propertyName)
            ?? throw new InvalidOperationException($"ProcessInfo.{propertyName} was not found.");
        Check(property.CanRead && !property.CanWrite,
            $"ProcessInfo.{propertyName} must remain public read-only metadata.");
    }

    var enumeratedProcess = NativeApi.EnumProcesses().FirstOrDefault(candidate => candidate.Pid == ready.Pid);
    Check(enumeratedProcess is not null, "NativeApi.EnumProcesses did not include the TestTarget process.");
    Check(process!.StartTime == enumeratedProcess!.StartTime,
        "NativeApi.GetProcess returned a target start time inconsistent with EnumProcesses.");

    using var pidSession = ProcessSession.Open(ready.Pid)
        ?? throw new InvalidOperationException("ProcessSession.Open(pid) failed for TestTarget.");
    Check(pidSession.IsAlive(), "PID-opened ProcessSession should observe TestTarget as alive.");

    using var session = ProcessSession.Open(process!)
        ?? throw new InvalidOperationException("ProcessSession.Open failed for TestTarget.");
    var exitReclaimedAllocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite)
        ?? throw new InvalidOperationException("Could not allocate target-exit ownership probe.");

    Check(session.Pid == ready.Pid, "ProcessSession attached to the wrong PID.");
    Check(session.IsAlive(), "TestTarget should be alive after attach.");

    // A stale process identity may carry a valid PID belonging to a different
    // process. Construct that identity deterministically via the internal
    // metadata/session constructors rather than relying on unpredictable PID reuse.
    // The public Attach(ProcessInfo) path correctly refuses this identity.
    var internalFlags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
    var infoConstructor = typeof(ProcessInfo).GetConstructor(
        internalFlags, binder: null,
        types: [typeof(uint), typeof(uint), typeof(global::Libmem.NET.Architecture), typeof(ulong),
                typeof(ulong), typeof(string), typeof(string)],
        modifiers: null);
    Check(infoConstructor is not null, "Expected an internal ProcessInfo metadata constructor.");
    var staleInfo = (ProcessInfo)infoConstructor!.Invoke(
        [process.Pid, process.ParentPid, process.Architecture, process.Bits,
         process.StartTime ^ 1UL, process.Name, process.Path]);
    Check(ProcessSession.Open(staleInfo) is null,
        "Public session attach must reject stale process start times.");

    var sessionConstructor = typeof(ProcessSession).GetConstructor(
        internalFlags, binder: null, types: [typeof(ProcessInfo)], modifiers: null);
    Check(sessionConstructor is not null, "Expected an internal ProcessSession constructor.");
    using var staleSession = (ProcessSession)sessionConstructor!.Invoke([staleInfo]);
    Check(!staleSession.IsAlive(),
        "An identity with the current PID but a different start time must be stale.");

    ExpectThrows<InvalidOperationException>(
        () => staleSession.Memory.Allocate(4096, MemoryProtection.ReadWrite),
        "A stale session must reject owned remote allocation.");
    ExpectThrows<InvalidOperationException>(
        () => NativeApi.AllocateMemory(staleInfo, 4096, MemoryProtection.ReadWrite),
        "Static AllocateMemory(process) must reject a stale process identity.");

    using (var staleFreeProbe = session.Memory.Allocate(4096, MemoryProtection.ReadWrite))
    {
        byte[] sentinel = [0x4C, 0x69, 0x62, 0x6D, 0x65, 0x6D, 0x32, 0x34];
        Check(session.Memory.Write(staleFreeProbe.Address, sentinel) == sentinel.Length,
            "Could not initialize stale-free protection fixture.");
        Check(!staleSession.Memory.Free(staleFreeProbe.Address, staleFreeProbe.Size),
            "A stale session must never free an allocation in the live process sharing its PID.");
        Check(!NativeApi.FreeMemory(staleInfo, staleFreeProbe.Address, staleFreeProbe.Size),
            "Static FreeMemory(process) must not free another process's allocation through a stale PID.");
        Check(session.Memory.Read(staleFreeProbe.Address, sentinel.Length).SequenceEqual(sentinel),
            "Stale Memory.Free unexpectedly changed the live process allocation.");
        Check(staleFreeProbe.Free(),
            "The valid session must retain ownership and be able to free the allocation.");
    }

    var staticAllocation = NativeApi.AllocateMemory(process!, 4096, MemoryProtection.ReadWrite);
    Check(staticAllocation != 0 && staticAllocation != invalidAddress,
        "Static AllocateMemory(process) must continue allocating into a live target.");
    try
    {
        byte[] staticSentinel = [0x41, 0x6C, 0x6C, 0x6F, 0x63];
        Check(session.Memory.Write(staticAllocation, staticSentinel) == staticSentinel.Length,
            "Could not initialize the live static allocation fixture.");
        Check(session.Memory.Read(staticAllocation, staticSentinel.Length).SequenceEqual(staticSentinel),
            "Static allocation must remain readable by the legitimate session.");
    }
    finally
    {
        Check(NativeApi.FreeMemory(process!, staticAllocation, 4096),
            "Static FreeMemory(process) must continue releasing a live target allocation.");
    }

    // Session and static mutating APIs must validate PID + creation time, not
    // merely the PID that native libmem reopens. This deterministic fake identity
    // represents a reused PID without needing OS PID reuse to happen in CI.
    using (var staleProtectProbe = session.Memory.Allocate(4096, MemoryProtection.ReadWrite))
    {
        var protectionBefore = ProtectionOf(process!, staleProtectProbe.Address);
        Check(protectionBefore == MemoryProtection.ReadWrite,
            "Expected a writable allocation before stale page-protection probes.");

        ExpectThrows<InvalidOperationException>(
            () => staleSession.Memory.Protect(
                staleProtectProbe.Address, staleProtectProbe.Size, MemoryProtection.Read),
            "MemoryManager.Protect must reject a stale process identity.");
        ExpectThrows<InvalidOperationException>(
            () => NativeApi.ProtectMemory(
                staleInfo, staleProtectProbe.Address, staleProtectProbe.Size, MemoryProtection.Read),
            "Static ProtectMemory(process) must reject a stale process identity.");

        Check(ProtectionOf(process!, staleProtectProbe.Address) == protectionBefore,
            "A stale session unexpectedly changed the live process page protection.");
    }

    ExpectThrows<InvalidOperationException>(
        () => staleSession.Hooks.Install(ready.HookSource, ready.HookDestination),
        "HookManager.Install must reject a stale process identity.");
    ExpectThrows<InvalidOperationException>(
        () => NativeApi.HookCode(staleInfo, ready.HookSource, ready.HookDestination),
        "Static HookCode(process) must reject a stale process identity.");
    Check(CallTarget(child, ready.HookSource) == 1,
        "Stale hook installation unexpectedly redirected live target code.");

    var liveModule = session.Modules.Enumerate().FirstOrDefault(module => module is not null)
        ?? throw new InvalidOperationException("TestTarget exposed no module for stale unload verification.");
    var moduleConstructor = typeof(ModuleInfo).GetConstructor(
        internalFlags, binder: null,
        types: [typeof(ulong), typeof(ulong), typeof(ulong),
                typeof(string), typeof(string), typeof(uint), typeof(ulong)],
        modifiers: null);
    Check(moduleConstructor is not null, "Expected an internal ModuleInfo provenance constructor.");
    var staleModule = (ModuleInfo)moduleConstructor!.Invoke(
        [liveModule.Base, liveModule.End, liveModule.Size, liveModule.Name, liveModule.Path,
         staleInfo.Pid, staleInfo.StartTime]);
    Check(!staleSession.Modules.Unload(staleModule),
        "ModuleManager.Unload must not unload a live module through a stale process identity.");
    Check(!NativeApi.UnloadModule(staleInfo, staleModule),
        "Static UnloadModule(process) must not unload through a stale process identity.");
    Check(!string.IsNullOrWhiteSpace(liveModule.Path),
        "TestTarget module must expose a path for stale load protection tests.");
    ExpectThrows<InvalidOperationException>(
        () => staleSession.Modules.Load(liveModule.Path),
        "ModuleManager.Load must refuse a stale PID before loading a module.");
    ExpectThrows<InvalidOperationException>(
        () => NativeApi.LoadModule(staleInfo, liveModule.Path),
        "Static LoadModule(process) must refuse a stale PID before loading a module.");
    Check(CallTarget(child, ready.HookSource) == 1,
        "Stale module unload unexpectedly affected the live target process.");

    // Real cross-process HookManager / HookHandle lifecycle.
    Check(CallTarget(child, ready.HookSource) == 1,
        "TestTarget Hook source did not return its original value.");
    Check(CallTarget(child, ready.HookDestination) == 2,
        "TestTarget Hook destination did not return its expected value.");

    var hookSourceSegment = NativeApi.FindSegment(process!, ready.HookSource)
        ?? throw new InvalidOperationException("Could not resolve TestTarget Hook source segment.");
    Check(hookSourceSegment.Protection == MemoryProtection.ExecuteRead,
        "TestTarget Hook source page must start execute-read for protection-regression coverage.");

    // Installation failure must be atomic: an unreadable source cannot produce
    // a managed handle or leave patched bytes behind.
    var originalSourceBytes = session.Memory.Read(ready.HookSource, 24);
    var alignedSource = session.Assembly.ReadAlignedCode(ready.HookSource, 1);
    Check(alignedSource.Length >= 1
          && originalSourceBytes.Length >= alignedSource.Length
          && originalSourceBytes.Take(alignedSource.Length).SequenceEqual(alignedSource),
        "ReadAlignedCode returned unexpected remote instruction bytes.");
    const uint pageNoAccess = 0x01;
    const uint pageExecuteRead = 0x20;
    const uint pageExecuteReadWrite = 0x40;

    var previousSourceProtection = SetTargetProtection(child, ready.HookSource, pageNoAccess);
    Check(previousSourceProtection == pageExecuteRead,
        "TestTarget source did not enter install-failure coverage from ExecuteRead.");
    try
    {
        var installFailure = ExpectThrows<LibmemException>(
            () => session.Hooks.Install(ready.HookSource, ready.HookDestination),
            "HookManager.Install should surface an unreadable source as a definite native failure.");
        Check(installFailure.Operation == "LM_HookCodeEx",
            "Unreadable-source install failure reported the wrong native operation.");
    }
    finally
    {
        SetTargetProtection(child, ready.HookSource, previousSourceProtection);
    }

    Check(session.Memory.Read(ready.HookSource, originalSourceBytes.Length).SequenceEqual(originalSourceBytes),
        "Failed HookManager.Install changed source bytes.");
    Check(CallTarget(child, ready.HookSource) == 1,
        "Failed HookManager.Install changed source behavior.");

    using (var remoteHook = session.Hooks.Install(ready.HookSource, ready.HookDestination))
    {
        Check(remoteHook.Source == ready.HookSource,
            "Remote HookHandle.Source does not match TestTarget source.");
        Check(remoteHook.Destination == ready.HookDestination,
            "Remote HookHandle.Destination does not match TestTarget destination.");
        Check(remoteHook.Trampoline != 0 && remoteHook.Trampoline != invalidAddress,
            "Remote HookHandle.Trampoline is invalid.");
        Check(remoteHook.PatchedBytes > 0 && remoteHook.PatchedBytes <= (ulong)originalSourceBytes.Length,
            "Remote HookHandle.PatchedBytes must describe a non-empty patch within the probed source bytes.");
        Check(remoteHook.IsInstalled && !remoteHook.IsDisposed,
            "Remote HookHandle should own an installed hook.");

        Check(CallTarget(child, ready.HookSource) == 2,
            "Remote hook did not redirect TestTarget source to destination.");
        Check(CallTarget(child, remoteHook.Trampoline) == 1,
            "Remote trampoline did not execute the original TestTarget source behavior.");

        Check(remoteHook.Remove(), "Remote HookHandle.Remove failed.");
        Check(!remoteHook.IsInstalled, "Remote HookHandle should report removed.");
        Check(CallTarget(child, ready.HookSource) == 1,
            "Remote HookHandle.Remove did not restore TestTarget source behavior.");
        Check(remoteHook.Remove(),
            "Remote HookHandle.Remove should be idempotent after successful removal.");
        Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
            "Successful HookHandle.Remove did not restore source protection.");
    }

    // A failed explicit Remove must preserve ownership and source protection so
    // the caller can fix the transient condition and retry deterministically.
    using (var retryHook = session.Hooks.Install(ready.HookSource, ready.HookDestination))
    {
        Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
            "Hook installation did not restore source protection before retry coverage.");

        var trampolineProtection = SetTargetProtection(child, retryHook.Trampoline, pageNoAccess);
        Check(trampolineProtection == pageExecuteReadWrite,
            "Hook trampoline did not start with ExecuteReadWrite protection.");
        try
        {
            Check(!retryHook.Remove(),
                "HookHandle.Remove should report failure while the trampoline is unreadable.");
            Check(retryHook.IsInstalled && !retryHook.IsDisposed,
                "Failed HookHandle.Remove must preserve managed ownership.");
            Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
                "Failed HookHandle.Remove must not change source protection.");

            // Explicit disposal must surface failed remote restoration without
            // abandoning the still-installed patch or its owned trampoline.
            var disposeFailure = ExpectThrows<LibmemException>(
                () => ((IDisposable)retryHook).Dispose(),
                "Remote HookHandle.Dispose must report an unreadable trampoline.");
            Check(disposeFailure.Operation == "LM_UnhookCodeEx",
                "Failed remote HookHandle.Dispose reported the wrong native operation.");
            Check(retryHook.IsInstalled && !retryHook.IsDisposed,
                "Failed remote HookHandle.Dispose must preserve ownership for retry.");
            Check(CallTarget(child, ready.HookSource) == 2,
                "Failed remote Dispose must leave the source redirected.");
            Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
                "Failed remote Dispose must leave the source page protection unchanged.");
        }
        finally
        {
            SetTargetProtection(child, retryHook.Trampoline, trampolineProtection);
        }

        Check(retryHook.Remove(),
            "HookHandle.Remove should succeed after the transient trampoline failure is repaired.");
        Check(!retryHook.IsInstalled,
            "Retry HookHandle should become inactive after successful removal.");
        Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
            "Retried HookHandle.Remove did not restore source protection.");
        Check(CallTarget(child, ready.HookSource) == 1,
            "Retried HookHandle.Remove did not restore source behavior.");
        ((IDisposable)retryHook).Dispose();
        ((IDisposable)retryHook).Dispose();
        Check(retryHook.IsDisposed && !retryHook.IsInstalled,
            "Remote HookHandle.Dispose after a successful Remove retry must be idempotent.");
        Check(retryHook.Remove(),
            "Remote HookHandle.Remove must remain idempotent after successful Dispose.");
    }

    // A remote HookHandle owns an independent target identity and must remain
    // removable even if the ProcessSession that installed it is detached.
    using (var hookOwnerSession = NativeApi.Attach(ready.Pid)
        ?? throw new InvalidOperationException("Could not attach remote HookHandle lifetime test session."))
    using (var detachedSessionHook = hookOwnerSession.Hooks.Install(
        ready.HookSource, ready.HookDestination))
    {
        Check(detachedSessionHook.IsInstalled && !detachedSessionHook.IsDisposed,
            "Remote HookHandle should own an active patch before session detach.");
        Check(CallTarget(child, ready.HookSource) == 2,
            "Remote HookHandle lifetime fixture failed to redirect the source.");
        hookOwnerSession.Detach();
        Check(hookOwnerSession.IsDisposed,
            "Detaching the remote hook owner session must mark it disposed.");
        Check(detachedSessionHook.IsInstalled && !detachedSessionHook.IsDisposed,
            "Remote HookHandle must survive its originating ProcessSession.Detach.");
        Check(CallTarget(child, ready.HookSource) == 2,
            "Remote session detach must not silently remove an independently owned patch.");
        ((IDisposable)detachedSessionHook).Dispose();
        Check(detachedSessionHook.IsDisposed && !detachedSessionHook.IsInstalled,
            "Remote HookHandle.Dispose must release the hook after parent session detach.");
        Check(CallTarget(child, ready.HookSource) == 1,
            "Remote HookHandle.Dispose after session detach did not restore source behavior.");
        Check(session.Memory.Read(ready.HookSource, originalSourceBytes.Length)
            .SequenceEqual(originalSourceBytes),
            "Remote HookHandle.Dispose after session detach did not restore source bytes.");
        Check(ProtectionOf(process!, ready.HookSource) == MemoryProtection.ExecuteRead,
            "Remote HookHandle.Dispose after session detach did not restore source protection.");
        Check(detachedSessionHook.Remove(),
            "Remote HookHandle.Remove after successful independent disposal must be idempotent.");
    }

    var foreignModule = NativeApi.EnumModules().FirstOrDefault(module => module is not null)
        ?? throw new InvalidOperationException("Current process exposed no module for provenance validation.");

    var foreignUnload = ExpectThrows<ArgumentException>(
        () => session.Modules.Unload(foreignModule),
        "ModuleManager.Unload should reject a ModuleInfo from another process.");
    Check(foreignUnload.ParamName == "module",
        "ModuleManager.Unload reported the wrong parameter name for a foreign ModuleInfo.");

    var staticForeignUnload = ExpectThrows<ArgumentException>(
        () => NativeApi.UnloadModule(process!, foreignModule),
        "UnloadModule(process, module) should reject a ModuleInfo from another process.");
    Check(staticForeignUnload.ParamName == "module",
        "UnloadModule(process, foreign module) reported the wrong parameter name.");

    var childModule = session.Modules.Enumerate().FirstOrDefault(module => module is not null)
        ?? throw new InvalidOperationException("TestTarget exposed no module for provenance validation.");
    var currentProcessUnload = ExpectThrows<ArgumentException>(
        () => NativeApi.UnloadModule(childModule),
        "UnloadModule(module) should reject a module captured from another process.");
    Check(currentProcessUnload.ParamName == "module",
        "UnloadModule(foreign module) reported the wrong parameter name.");

    var foreignSymbolEnumeration = ExpectThrows<ArgumentException>(
        () => session.Symbols.Enumerate(foreignModule, demangle: false),
        "SymbolManager.Enumerate should reject a ModuleInfo from another process.");
    Check(foreignSymbolEnumeration.ParamName == "module",
        "SymbolManager.Enumerate reported the wrong parameter name for a foreign ModuleInfo.");

    var foreignSymbolLookup = ExpectThrows<ArgumentException>(
        () => session.Symbols.FindAddress(foreignModule, "unused", demangle: false),
        "SymbolManager.FindAddress should reject a ModuleInfo from another process.");
    Check(foreignSymbolLookup.ParamName == "module",
        "SymbolManager.FindAddress reported the wrong parameter name for a foreign ModuleInfo.");

    var foreignTrySymbolLookup = ExpectThrows<ArgumentException>(
        () => session.Symbols.TryFindAddress(foreignModule, "unused", demangle: false, out _),
        "SymbolManager.TryFindAddress should reject a ModuleInfo from another process.");
    Check(foreignTrySymbolLookup.ParamName == "module",
        "SymbolManager.TryFindAddress reported the wrong parameter name for a foreign ModuleInfo.");

    byte[] expected =
    [
        0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
        0x54, 0x41, 0x52, 0x47, 0x45, 0x54
    ];

    var initial = session.Memory.Read(ready.Address, expected.Length);
    Check(initial.SequenceEqual(expected), "Remote Read did not match the TestTarget payload.");

    byte[] replacement = [0x41, 0x52, 0x43, 0x48, 0x2D, 0x54, 0x45, 0x53, 0x54];
    Check(session.Memory.Write(ready.Address + 32, replacement) == replacement.Length,
        "Remote Write did not write the full replacement payload.");
    Check(session.Memory.Read(ready.Address + 32, replacement.Length).SequenceEqual(replacement),
        "Remote Read after Write returned different bytes.");
    Check(session.Memory.TryWrite(ready.Address + 32, replacement, out var remoteTryWritten)
          && remoteTryWritten == replacement.Length,
        "Remote TryWrite must report an exact transfer.");
    Check(session.Memory.TryRead(ready.Address + 32, replacement.Length, out var remoteTryRead)
          && remoteTryRead.SequenceEqual(replacement),
        "Remote TryRead must report an exact transfer.");

    var signature = string.Join(" ", expected.Select(value => value.ToString("X2")));
    Check(session.Scanner.SigScan(signature, ready.Address, ready.Size) == ready.Address,
        "Remote signature scan did not resolve the TestTarget allocation.");

    var exactRemoteScanSize = (ulong)expected.Length;
    var exactRemoteMask = new string('x', expected.Length);
    Check(session.Scanner.DataScan(expected, ready.Address, exactRemoteScanSize) == ready.Address,
        "Remote DataScan missed the final candidate in an exact-size window.");
    Check(session.Scanner.PatternScan(expected, exactRemoteMask, ready.Address, exactRemoteScanSize) == ready.Address,
        "Remote PatternScan missed the final candidate in an exact-size window.");
    Check(session.Scanner.SigScan(signature, ready.Address, exactRemoteScanSize) == ready.Address,
        "Remote SigScan missed the final candidate in an exact-size window.");

    var segment = NativeApi.FindSegment(process!, ready.Address);
    Check(segment is not null
          && segment.Base <= ready.Address
          && ready.Address < segment.End,
        "Remote FindSegment did not resolve the TestTarget allocation.");

    using (var remoteAllocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite))
    {
        byte[] remotePayload = [0x52, 0x45, 0x4D, 0x4F, 0x54, 0x45, 0x2D, 0x41, 0x52, 0x43, 0x48];
        Check(session.Memory.Write(remoteAllocation.Address, remotePayload) == remotePayload.Length,
            "Remote allocation Write did not write the full payload.");
        Check(session.Memory.Read(remoteAllocation.Address, remotePayload.Length).SequenceEqual(remotePayload),
            "Remote allocation Read returned different bytes.");
        const long remoteSignedValue = -0x102030405060708L;
        session.Memory.WriteInt64(remoteAllocation.Address + 64, remoteSignedValue);
        Check(session.Memory.ReadInt64(remoteAllocation.Address + 64) == remoteSignedValue,
            "Remote typed Int64 read/write did not round trip.");
        session.Memory.WritePointer(remoteAllocation.Address + 80, remoteAllocation.Address);
        Check(session.Memory.ReadPointer(remoteAllocation.Address + 80) == remoteAllocation.Address,
            "Remote typed pointer read/write did not use the target architecture width.");

        var oldProtection = session.Memory.Protect(
            remoteAllocation.Address,
            remoteAllocation.Size,
            MemoryProtection.Read);
        try
        {
            Check(session.Memory.Read(remoteAllocation.Address, remotePayload.Length).SequenceEqual(remotePayload),
                "Remote allocation Read failed after Protect.");
        }
        finally
        {
            session.Memory.Protect(
                remoteAllocation.Address,
                remoteAllocation.Size,
                oldProtection);
        }
    }

    // Real remote loader ownership: two handles own two references to the same
    // uniquely named DLL in the independently launched target process.
    Directory.CreateDirectory(fixtureDirectory);
    var fixtureSource = Path.Combine(AppContext.BaseDirectory, "libmem.dll");
    Check(File.Exists(fixtureSource), "Could not find the architecture-matched remote DLL fixture.");
    var referenceFixture = CreateLibraryFixture(fixtureDirectory, fixtureSource, "references");
    var referenceName = Path.GetFileName(referenceFixture);
    Check(session.Modules.Find(referenceName) is null,
        "Reference-count fixture must not already be loaded in the target.");
    using (var firstReference = session.Injector.InjectLibrary(referenceFixture))
    using (var secondReference = session.Injector.InjectLibrary(referenceFixture))
    {
        Check(firstReference.IsActive && secondReference.IsActive
              && !firstReference.IsDisposed && !secondReference.IsDisposed,
            "Both remote injection handles must own active loader references.");
        Check(firstReference.Module.Base == secondReference.Module.Base,
            "Loading the same remote DLL twice must resolve the same module.");
        var firstSnapshot = firstReference.Module;
        var secondSnapshot = firstReference.Module;
        Check(!ReferenceEquals(firstSnapshot, secondSnapshot)
              && firstSnapshot.Base == secondSnapshot.Base
              && firstSnapshot.Size == secondSnapshot.Size,
            "InjectedModuleHandle.Module must return independent snapshots of the target module.");
        Check(string.Equals(firstReference.RequestedPath, Path.GetFullPath(referenceFixture),
                            StringComparison.OrdinalIgnoreCase)
              && string.Equals(firstSnapshot.Path, Path.GetFullPath(referenceFixture),
                               StringComparison.OrdinalIgnoreCase),
            "Remote injected module metadata must resolve the requested full path.");

        ((IDisposable)firstReference).Dispose();
        ((IDisposable)firstReference).Dispose();
        Check(firstReference.IsDisposed && !firstReference.IsActive && firstReference.Unload(),
            "Disposing the first remote loader reference must be idempotent.");
        Check(secondReference.IsActive && !secondReference.IsDisposed,
            "Disposing one loader reference must not invalidate another handle.");
        Check(session.Modules.Find(referenceName) is not null,
            "The remote DLL must remain loaded while the second owned reference exists.");
        Check(secondReference.Unload() && secondReference.Unload(),
            "Releasing the final remote loader reference must be idempotent.");
        Check(!secondReference.IsActive && !secondReference.IsDisposed,
            "Explicit Unload must release ownership without implicitly disposing the handle.");
        Check(session.Modules.Find(referenceName) is null,
            "The uniquely named remote DLL must disappear after its last reference is released.");
        ((IDisposable)secondReference).Dispose();
        Check(secondReference.IsDisposed && secondReference.Unload(),
            "Dispose after remote Unload must complete idempotently.");
        Check(firstReference.Module.Base == firstSnapshot.Base,
            "Disposal must retain the injected module metadata snapshot.");
    }

    // Allocation and module handles own copied target identities independently
    // of the ProcessSession that created them.
    var detachedFixture = CreateLibraryFixture(fixtureDirectory, fixtureSource, "detached");
    var detachedName = Path.GetFileName(detachedFixture);
    using (var resourceOwner = NativeApi.Attach(ready.Pid)
        ?? throw new InvalidOperationException("Could not attach detached-resource fixture session."))
    using (var detachedAllocation = resourceOwner.Memory.Allocate(4096, MemoryProtection.ReadWrite))
    using (var detachedModule = resourceOwner.Injector.InjectLibrary(detachedFixture))
    {
        byte[] ownedPayload = [0x4F, 0x57, 0x4E, 0x45, 0x44];
        Check(resourceOwner.Memory.Write(detachedAllocation.Address, ownedPayload) == ownedPayload.Length,
            "Could not initialize the independently owned allocation.");
        resourceOwner.Detach();
        Check(resourceOwner.IsDisposed
              && !detachedAllocation.IsDisposed
              && detachedModule.IsActive && !detachedModule.IsDisposed,
            "Session detach must leave independently owned allocations and modules active.");
        Check(session.Memory.Read(detachedAllocation.Address, ownedPayload.Length).SequenceEqual(ownedPayload),
            "Session detach unexpectedly invalidated the owned remote allocation.");
        Check(session.Modules.Find(detachedName) is not null,
            "Session detach unexpectedly unloaded the owned remote DLL.");
        ((IDisposable)detachedAllocation).Dispose();
        ((IDisposable)detachedAllocation).Dispose();
        Check(detachedAllocation.IsDisposed && detachedAllocation.Free(),
            "RemoteAllocation.Dispose after session detach must release ownership idempotently.");
        ((IDisposable)detachedModule).Dispose();
        ((IDisposable)detachedModule).Dispose();
        Check(detachedModule.IsDisposed && !detachedModule.IsActive && detachedModule.Unload(),
            "InjectedModuleHandle.Dispose after session detach must release ownership idempotently.");
        Check(session.Modules.Find(detachedName) is null,
            "Disposal after session detach did not release the remote DLL reference.");
    }

    // Keep live resources until target exit; dispose directly afterwards rather
    // than first normalizing their states with Free/Unload.
    using var directlyDisposedExitAllocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite);
    var exitFixture = CreateLibraryFixture(fixtureDirectory, fixtureSource, "exit");
    using var exitModule = session.Injector.InjectLibrary(exitFixture);
    var exitModuleSnapshot = exitModule.Module;
    Check(exitModule.IsActive && !exitModule.IsDisposed
          && session.Modules.Find(Path.GetFileName(exitFixture)) is not null,
        "Target-exit injection fixture must own a loaded remote module.");

    Check(SendCommand(child, "ping") == "PONG",
        "TestTarget control channel did not respond to ping.");

    using var exitReclaimedHook = session.Hooks.Install(
        ready.HookSource,
        ready.HookDestination);
    Check(exitReclaimedHook.IsInstalled,
        "Target-exit HookHandle should start installed.");
    Check(CallTarget(child, ready.HookSource) == 2,
        "Target-exit HookHandle did not redirect source before process exit.");

    var sourceProtectionBeforeFailedRemove =
        NativeApi.FindSegment(process!, ready.HookSource)?.Protection
        ?? throw new InvalidOperationException("Could not resolve Hook source protection before failure probe.");
    Check(sourceProtectionBeforeFailedRemove == MemoryProtection.ExecuteRead,
        "Hook installation should restore the source page to execute-read.");

    // Freeing a trampoline is not a deterministic unreadable-memory fixture:
    // Windows may reuse the released virtual address before the unhook probe.
    // Protect the still-allocated page with PAGE_NOACCESS instead. The target
    // owns this memory and its exit will reclaim it without an explicit unhook.
    _ = SetTargetProtection(child, exitReclaimedHook.Trampoline, pageNoAccess);
    Check(session.Memory.Read(
            exitReclaimedHook.Trampoline, checked((int)exitReclaimedHook.PatchedBytes)).Length == 0,
        "Trampoline must be unreadable before testing the failed unhook path.");
    Check(!exitReclaimedHook.Remove(),
        "HookHandle.Remove should report failure when the live target trampoline is no longer readable.");
    Check(exitReclaimedHook.IsInstalled,
        "Failed HookHandle.Remove must preserve ownership for a later cleanup attempt.");
    Check(CallTarget(child, ready.HookSource) == 2,
        "Failed HookHandle.Remove must leave the existing source redirection intact.");

    var sourceProtectionAfterFailedRemove =
        NativeApi.FindSegment(process!, ready.HookSource)?.Protection
        ?? throw new InvalidOperationException("Could not resolve Hook source protection after failure probe.");
    Check(sourceProtectionAfterFailedRemove == sourceProtectionBeforeFailedRemove,
        "Failed HookHandle.Remove must not leave the source page with modified protection.");

    child.StandardInput.WriteLine("exit");
    child.StandardInput.Flush();

    Check(child.WaitForExit(10_000), "TestTarget did not exit after the exit command.");
    Check(!session.IsAlive(), "ProcessSession should observe TestTarget exit.");
    Check(session.Refresh() is null, "ProcessSession.Refresh should return null after target exit.");
    Check(!session.IsDisposed, "Target exit must not implicitly dispose ProcessSession.");
    Check(session.Pid == process.Pid, "ProcessSession should retain the bound PID after target exit.");
    Check(session.Name == process.Name, "ProcessSession should retain the bound process name after target exit.");
    Check(session.Architecture == process.Architecture,
        "ProcessSession should retain the bound architecture after target exit.");
    Check(session.Bits == process.Bits, "ProcessSession should retain the bound bitness after target exit.");
    Check(session.Info.Pid == process.Pid && session.Info.StartTime == process.StartTime,
        "ProcessSession.Info should retain the original process identity after target exit.");

    Check(session.Memory is not null
          && session.Modules is not null
          && session.Threads is not null
          && session.Scanner is not null
          && session.Symbols is not null
          && session.Assembly is not null
          && session.Hooks is not null
          && session.Injector is not null,
        "Target exit must not detach session-bound Managers.");

    ExpectThrows<InvalidOperationException>(
        () => session.Memory.Allocate(4096, MemoryProtection.ReadWrite),
        "MemoryManager.Allocate should reject a dead target.");
    ExpectThrows<InvalidOperationException>(
        () => session.Injector.InjectLibrary("libmemcli-target-exit-probe.dll"),
        "InjectorManager.InjectLibrary should reject a dead target before file resolution.");

    Check(exitReclaimedHook.Remove(),
        "HookHandle.Remove should treat target-process exit as OS-reclaimed ownership.");
    Check(!exitReclaimedHook.IsInstalled,
        "HookHandle should become inactive after target-process exit is observed during Remove.");
    Check(exitReclaimedHook.Remove(),
        "HookHandle.Remove should remain idempotent after target-process exit.");
    ((IDisposable)exitReclaimedHook).Dispose();
    ((IDisposable)exitReclaimedHook).Dispose();
    Check(exitReclaimedHook.IsDisposed && !exitReclaimedHook.IsInstalled,
        "Remote HookHandle.Dispose after target exit must finish ownership release idempotently.");

    Check(exitReclaimedAllocation.Free(),
        "RemoteAllocation.Free should treat target-process exit as OS-reclaimed ownership.");
    Check(exitReclaimedAllocation.IsDisposed,
        "RemoteAllocation should become disposed after target-process exit is observed during Free.");
    ((IDisposable)exitReclaimedAllocation).Dispose();
    ((IDisposable)exitReclaimedAllocation).Dispose();

    ((IDisposable)directlyDisposedExitAllocation).Dispose();
    ((IDisposable)directlyDisposedExitAllocation).Dispose();
    Check(directlyDisposedExitAllocation.IsDisposed && directlyDisposedExitAllocation.Free(),
        "Direct RemoteAllocation.Dispose after target exit must finish ownership release idempotently.");
    ((IDisposable)exitModule).Dispose();
    ((IDisposable)exitModule).Dispose();
    Check(exitModule.IsDisposed && !exitModule.IsActive && exitModule.Unload(),
        "Direct InjectedModuleHandle.Dispose after target exit must finish ownership release idempotently.");
    Check(exitModule.Module.Base == exitModuleSnapshot.Base
          && string.Equals(exitModule.RequestedPath, Path.GetFullPath(exitFixture),
                           StringComparison.OrdinalIgnoreCase),
        "Target-exit disposal must retain remote module diagnostic metadata.");

    Console.WriteLine("EXTERNAL PROCESS TESTS PASS");
}
finally
{
    if (!child.HasExited)
    {
        child.Kill(entireProcessTree: true);
        child.WaitForExit(5_000);
    }

    try
    {
        if (Directory.Exists(fixtureDirectory))
            Directory.Delete(fixtureDirectory, recursive: true);
    }
    catch
    {
        // Preserve the test failure if a fixture file cannot be removed.
    }
}
