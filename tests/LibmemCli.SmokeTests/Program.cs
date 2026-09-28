using LibmemCli;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Console.WriteLine("LibmemCli runtime smoke tests");

var current = Libmem.CurrentProcess();
Check(current is not null, "CurrentProcess returned null.");
Check(current!.Pid == (uint)Environment.ProcessId, "CurrentProcess PID does not match the test process.");
Check(current.IsAlive(), "Current process should be alive.");

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

using var pidSession = Libmem.Attach((uint)Environment.ProcessId);
Check(pidSession is not null && pidSession.Pid == current.Pid, "Attach(pid) failed for the current process.");

var processes = Libmem.EnumProcesses();
Check(processes.Any(p => p.Pid == current.Pid), "EnumProcesses did not include the current process.");

var modules = Libmem.EnumModules();
Check(modules.Count > 0, "EnumModules returned no modules.");
Check(modules.Any(m => m.Base != 0 && m.Size != 0), "EnumModules returned no usable module.");

const ulong allocationSize = 4096;
var address = Libmem.AllocateMemory(allocationSize, MemoryProtection.ReadWrite);
Check(address != 0 && address != ulong.MaxValue, "AllocateMemory failed.");

try
{
    byte[] payload = [0x48, 0x45, 0x41, 0x52, 0x54, 0x48, 0x53, 0x54];
    var written = Libmem.WriteMemory(address, payload);
    Check(written == payload.Length, $"WriteMemory wrote {written} of {payload.Length} bytes.");

    var read = Libmem.ReadMemory(address, payload.Length);
    Check(read.SequenceEqual(payload), "ReadMemory did not return the bytes that were written.");

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

    var machineCode = Libmem.Assemble("nop; ret", Architecture.X64, 0x1000);
    Check(machineCode is { Length: > 0 }, "Assemble returned no machine code.");

    var instructions = Libmem.Disassemble(machineCode!, Architecture.X64, 2, 0x1000);
    Check(instructions.Count > 0, "Disassemble returned no instructions.");
    Check(instructions[0].Mnemonic.Length > 0, "Disassembled instruction has no mnemonic.");
}
finally
{
    Check(Libmem.FreeMemory(address, allocationSize), "FreeMemory failed.");
}

Console.WriteLine("SMOKE TESTS PASS");
