using LibmemCli;

try
{
    RunSample();
}
catch (LibmemException ex)
{
    Console.Error.WriteLine($"Libmem operation failed: {ex.Operation}: {ex.Message}");
    Environment.ExitCode = 1;
}

static void RunSample()
{
    var self = Libmem.CurrentProcess()
        ?? throw new InvalidOperationException("Current process could not be resolved.");

    using var session = ProcessSession.Open(self)
        ?? throw new InvalidOperationException("The current process identity became stale before attach.");

    Console.WriteLine(
        $"Process: {session.Name}  PID={session.Pid}  Arch={session.Architecture}  Bits={session.Bits}");

    var refreshed = session.Refresh()
        ?? throw new InvalidOperationException("The current process identity could not be refreshed.");

    Console.WriteLine(
        $"Identity: PID={refreshed.Pid}  StartTime={refreshed.StartTime}");

    var modules = session.Modules.Enumerate();
    var threads = session.Threads.Enumerate();

    Console.WriteLine($"Modules: {modules.Count}");
    Console.WriteLine($"Threads: {threads.Count}");

    if (modules.Count > 0)
    {
        var first = modules[0];
        Console.WriteLine(
            $"First module: {first.Name}  Base=0x{first.Base:X}  Size=0x{first.Size:X}");
    }

    using var allocation = session.Memory.Allocate(
        4096,
        MemoryProtection.ReadWrite);

    byte[] payload =
    [
        0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
        0x43, 0x4C, 0x49, 0x2D, 0x58, 0x36, 0x34
    ];

    var written = session.Memory.Write(allocation.Address, payload);
    if (written != payload.Length)
        throw new InvalidOperationException(
            $"Short write: expected {payload.Length} bytes, wrote {written}.");

    var copy = session.Memory.Read(allocation.Address, payload.Length);
    if (!copy.SequenceEqual(payload))
        throw new InvalidOperationException("Read-back bytes did not match the written payload.");

    var signature = string.Join(" ", payload.Select(value => value.ToString("X2")));
    var hit = session.Scanner.SigScan(
        signature,
        allocation.Address,
        allocation.Size);

    if (hit != allocation.Address)
        throw new InvalidOperationException("Signature scan did not resolve the owned allocation.");

    Console.WriteLine(
        $"Owned allocation: 0x{allocation.Address:X}, size={allocation.Size}, scan=0x{hit:X}");

    var oldProtection = session.Memory.Protect(
        allocation.Address,
        allocation.Size,
        MemoryProtection.Read);

    try
    {
        var protectedRead = session.Memory.Read(allocation.Address, payload.Length);
        if (!protectedRead.SequenceEqual(payload))
            throw new InvalidOperationException("Read failed after switching the allocation to read-only.");
    }
    finally
    {
        session.Memory.Protect(
            allocation.Address,
            allocation.Size,
            oldProtection);
    }

    Console.WriteLine("Sample completed successfully.");
}
