using LibmemCli;

Console.WriteLine("LibmemCli NuGet consumer test");

var moduleType = typeof(ModuleInfo);
if (moduleType.GetConstructors().Length != 0)
    throw new InvalidOperationException("ModuleInfo must not expose a public constructor.");

foreach (var propertyName in new[] { "Base", "End", "Size", "Name", "Path" })
{
    var property = moduleType.GetProperty(propertyName)
        ?? throw new InvalidOperationException($"ModuleInfo.{propertyName} is missing.");

    if (property.CanWrite)
        throw new InvalidOperationException($"ModuleInfo.{propertyName} must be read-only.");
}

var threadType = typeof(ThreadInfo);
if (threadType.GetConstructors().Length != 0)
    throw new InvalidOperationException("ThreadInfo must not expose a public constructor.");

foreach (var propertyName in new[] { "Id", "OwnerPid" })
{
    var property = threadType.GetProperty(propertyName)
        ?? throw new InvalidOperationException($"ThreadInfo.{propertyName} is missing.");

    if (property.CanWrite)
        throw new InvalidOperationException($"ThreadInfo.{propertyName} must be read-only.");
}

var process = Libmem.CurrentProcess()
    ?? throw new InvalidOperationException("Current process could not be resolved through the NuGet package.");

using var session = ProcessSession.Open(process)
    ?? throw new InvalidOperationException("ProcessSession.Open failed through the NuGet package.");

using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);

byte[] payload = [0x4E, 0x55, 0x47, 0x45, 0x54, 0x2D, 0x58, 0x36, 0x34];

if (session.Memory.Write(allocation.Address, payload) != payload.Length)
    throw new InvalidOperationException("NuGet consumer short write.");

var copy = session.Memory.Read(allocation.Address, payload.Length);
if (!copy.SequenceEqual(payload))
    throw new InvalidOperationException("NuGet consumer read-back mismatch.");

Console.WriteLine(
    $"NUGET CONSUMER PASS pid={session.Pid} allocation=0x{allocation.Address:X}");
