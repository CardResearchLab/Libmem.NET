using System.Diagnostics;
using System.Globalization;
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

    throw new Exception(message);
}

static string ResolveTargetDll()
{
    var configured = Environment.GetEnvironmentVariable("LIBMEMCLI_TEST_TARGET_DLL");
    if (!string.IsNullOrWhiteSpace(configured))
        return Path.GetFullPath(configured);

    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var candidates = new[]
    {
        Path.Combine(repoRoot, "LibmemCli.TestTarget", "bin", "x64", "Release", "net8.0", "LibmemCli.TestTarget.dll"),
        Path.Combine(repoRoot, "LibmemCli.TestTarget", "bin", "Release", "net8.0", "LibmemCli.TestTarget.dll"),
    };

    return candidates.FirstOrDefault(File.Exists)
        ?? throw new FileNotFoundException(
            "LibmemCli.TestTarget.dll was not found. Build tests/LibmemCli.TestTarget first or set LIBMEMCLI_TEST_TARGET_DLL.");
}

static (uint Pid, ulong Address, ulong Size) ParseReady(string line)
{
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 4 || parts[0] != "READY")
        throw new InvalidOperationException($"Unexpected TestTarget handshake: {line}");

    uint pid = uint.Parse(parts[1].Split('=', 2)[1], CultureInfo.InvariantCulture);

    var addressText = parts[2].Split('=', 2)[1];
    if (!addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Unexpected address in handshake: {addressText}");

    ulong address = ulong.Parse(addressText[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    ulong size = ulong.Parse(parts[3].Split('=', 2)[1], CultureInfo.InvariantCulture);
    return (pid, address, size);
}

Console.WriteLine("LibmemCli external-process runtime tests");

var targetDll = ResolveTargetDll();
var startInfo = new ProcessStartInfo
{
    FileName = "dotnet",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    RedirectStandardInput = true,
    CreateNoWindow = true,
};
startInfo.ArgumentList.Add(targetDll);

using var child = Process.Start(startInfo)
    ?? throw new InvalidOperationException("Could not start LibmemCli.TestTarget.");

try
{
    var readyLine = child.StandardOutput.ReadLine()
        ?? throw new InvalidOperationException(
            $"TestTarget exited before handshake. stderr: {child.StandardError.ReadToEnd()}");

    var ready = ParseReady(readyLine);
    Check(ready.Pid == (uint)child.Id, "Handshake PID does not match the launched child process.");
    Check(ready.Address != 0 && ready.Size >= 64, "TestTarget returned an invalid allocation.");

    var process = Libmem.GetProcess(ready.Pid);
    Check(process is not null, "Libmem.GetProcess could not resolve the TestTarget process.");

    var enumeratedProcess = Libmem.EnumProcesses().FirstOrDefault(candidate => candidate.Pid == ready.Pid);
    Check(enumeratedProcess is not null, "Libmem.EnumProcesses did not include the TestTarget process.");
    Check(process!.StartTime == enumeratedProcess!.StartTime,
        "Libmem.GetProcess returned a target start time inconsistent with EnumProcesses.");

    using var pidSession = ProcessSession.Open(ready.Pid)
        ?? throw new InvalidOperationException("ProcessSession.Open(pid) failed for TestTarget.");
    Check(pidSession.IsAlive(), "PID-opened ProcessSession should observe TestTarget as alive.");

    using var session = ProcessSession.Open(process!)
        ?? throw new InvalidOperationException("ProcessSession.Open failed for TestTarget.");
    var exitReclaimedAllocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite)
        ?? throw new InvalidOperationException("Could not allocate target-exit ownership probe.");

    Check(session.Pid == ready.Pid, "ProcessSession attached to the wrong PID.");
    Check(session.IsAlive(), "TestTarget should be alive after attach.");

    byte[] expected =
    [
        0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
        0x54, 0x41, 0x52, 0x47, 0x45, 0x54
    ];

    var initial = session.Memory.Read(ready.Address, expected.Length);
    Check(initial.SequenceEqual(expected), "Remote Read did not match the TestTarget payload.");

    byte[] replacement = [0x58, 0x36, 0x34, 0x2D, 0x54, 0x45, 0x53, 0x54];
    Check(session.Memory.Write(ready.Address + 32, replacement) == replacement.Length,
        "Remote Write did not write the full replacement payload.");
    Check(session.Memory.Read(ready.Address + 32, replacement.Length).SequenceEqual(replacement),
        "Remote Read after Write returned different bytes.");

    var signature = string.Join(" ", expected.Select(value => value.ToString("X2")));
    Check(session.Scanner.SigScan(signature, ready.Address, ready.Size) == ready.Address,
        "Remote signature scan did not resolve the TestTarget allocation.");

    var segment = Libmem.FindSegment(process!, ready.Address);
    Check(segment is not null
          && segment.Base <= ready.Address
          && ready.Address < segment.End,
        "Remote FindSegment did not resolve the TestTarget allocation.");

    using (var remoteAllocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite))
    {
        byte[] remotePayload = [0x52, 0x45, 0x4D, 0x4F, 0x54, 0x45, 0x2D, 0x58, 0x36, 0x34];
        Check(session.Memory.Write(remoteAllocation.Address, remotePayload) == remotePayload.Length,
            "Remote allocation Write did not write the full payload.");
        Check(session.Memory.Read(remoteAllocation.Address, remotePayload.Length).SequenceEqual(remotePayload),
            "Remote allocation Read returned different bytes.");

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

    child.StandardInput.WriteLine("ping");
    child.StandardInput.Flush();
    Check(child.StandardOutput.ReadLine() == "PONG", "TestTarget control channel did not respond to ping.");

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
