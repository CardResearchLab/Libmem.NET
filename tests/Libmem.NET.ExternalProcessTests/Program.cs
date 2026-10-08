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

Console.WriteLine("Libmem.NET external-process runtime tests");

var invalidAddress = IntPtr.Size == sizeof(ulong) ? ulong.MaxValue : uint.MaxValue;
var targetPath = ResolveTargetPath();
var startInfo = CreateTargetStartInfo(targetPath);

using var child = Process.Start(startInfo)
    ?? throw new InvalidOperationException("Could not start Libmem.NET.TestTarget.");

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

    Check(session.Memory.Free(exitReclaimedHook.Trampoline, exitReclaimedHook.PatchedBytes),
        "Could not invalidate the remote trampoline for the unhook failure probe.");
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

    Check(exitReclaimedAllocation.Free(),
        "RemoteAllocation.Free should treat target-process exit as OS-reclaimed ownership.");
    Check(exitReclaimedAllocation.IsDisposed,
        "RemoteAllocation should become disposed after target-process exit is observed during Free.");
    ((IDisposable)exitReclaimedAllocation).Dispose();
    ((IDisposable)exitReclaimedAllocation).Dispose();

    Console.WriteLine("EXTERNAL PROCESS TESTS PASS");
}
finally
{
    if (!child.HasExited)
    {
        child.Kill(entireProcessTree: true);
        child.WaitForExit(5_000);
    }
}
