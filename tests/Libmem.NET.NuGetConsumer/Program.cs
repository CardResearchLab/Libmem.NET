using Libmem.Net;

Console.WriteLine("Libmem.NET NuGet consumer test");

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

var symbolType = typeof(SymbolInfo);
if (symbolType.GetConstructors().Length != 0)
    throw new InvalidOperationException("SymbolInfo must not expose a public constructor.");

foreach (var propertyName in new[] { "Address", "Name" })
{
    var property = symbolType.GetProperty(propertyName)
        ?? throw new InvalidOperationException($"SymbolInfo.{propertyName} is missing.");

    if (property.CanWrite)
        throw new InvalidOperationException($"SymbolInfo.{propertyName} must be read-only.");
}

var segmentType = typeof(SegmentInfo);
if (segmentType.GetConstructors().Length != 0)
    throw new InvalidOperationException("SegmentInfo must not expose a public constructor.");

foreach (var propertyName in new[] { "Base", "End", "Size", "Protection" })
{
    var property = segmentType.GetProperty(propertyName)
        ?? throw new InvalidOperationException($"SegmentInfo.{propertyName} is missing.");

    if (property.CanWrite)
        throw new InvalidOperationException($"SegmentInfo.{propertyName} must be read-only.");
}

var instructionType = typeof(InstructionInfo);
if (instructionType.GetConstructors().Length != 0)
    throw new InvalidOperationException("InstructionInfo must not expose a public constructor.");

foreach (var propertyName in new[] { "Address", "Size", "Bytes", "Mnemonic", "OperandString" })
{
    var property = instructionType.GetProperty(propertyName)
        ?? throw new InvalidOperationException($"InstructionInfo.{propertyName} is missing.");

    if (property.CanWrite)
        throw new InvalidOperationException($"InstructionInfo.{propertyName} must be read-only.");
}

var assembledInstruction = Libmem.Assemble("nop")
    ?? throw new InvalidOperationException("Could not assemble an instruction for immutability validation.");

var originalBytes = assembledInstruction.Bytes;
if (originalBytes.Length == 0)
    throw new InvalidOperationException("InstructionInfo.Bytes returned no bytes.");

var expectedFirstByte = originalBytes[0];
originalBytes[0] ^= 0xFF;

if (assembledInstruction.Bytes[0] != expectedFirstByte)
    throw new InvalidOperationException("InstructionInfo.Bytes must return a defensive copy.");

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
