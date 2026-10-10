using System.Runtime.InteropServices;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

Console.WriteLine($"Libmem.NET NuGet consumer test ({RuntimeInformation.ProcessArchitecture})");

if (RuntimeInformation.ProcessArchitecture is not (RuntimeArchitecture.X64 or RuntimeArchitecture.X86))
    throw new PlatformNotSupportedException($"Unexpected NuGet consumer architecture: {RuntimeInformation.ProcessArchitecture}.");

var wrapperAssembly = typeof(ProcessSession).Assembly;
if (wrapperAssembly.GetName().Name != "Libmem.NET")
    throw new InvalidOperationException("The wrapper assembly identity must be Libmem.NET.");
foreach (var apiType in new[] { typeof(NativeApi), typeof(ProcessSession), typeof(ProcessInfo),
    typeof(MemoryManager), typeof(HookManager), typeof(VmtManager), typeof(InjectorManager) })
{
    if (apiType.Namespace != "Libmem.NET" || apiType.Assembly != wrapperAssembly)
        throw new InvalidOperationException($"Unexpected public API identity: {apiType.FullName}");
}


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

var assembledInstruction = NativeApi.Assemble("nop")
    ?? throw new InvalidOperationException("Could not assemble an instruction for immutability validation.");

var originalBytes = assembledInstruction.Bytes;
if (originalBytes.Length == 0)
    throw new InvalidOperationException("InstructionInfo.Bytes returned no bytes.");

var expectedFirstByte = originalBytes[0];
originalBytes[0] ^= 0xFF;

if (assembledInstruction.Bytes[0] != expectedFirstByte)
    throw new InvalidOperationException("InstructionInfo.Bytes must return a defensive copy.");

var process = NativeApi.CurrentProcess()
    ?? throw new InvalidOperationException("Current process could not be resolved through the NuGet package.");

using var session = ProcessSession.Open(process)
    ?? throw new InvalidOperationException("ProcessSession.Open failed through the NuGet package.");

#if LIBMEM_NET_TEST_UNRELEASED_APIS
// Verify the 2.4 additive APIs on official published 2.5.0 and local feature candidates.
if (session.Assembly.ReadAlignedCode(0, 0).Length != 0)
    throw new InvalidOperationException("Zero-length instruction inspection should return no bytes.");

try
{
    _ = session.Symbols.TryFindAddress(null!, "unused", false, out _);
    throw new InvalidOperationException("TryFindAddress must reject a null module.");
}
catch (ArgumentNullException ex) when (ex.ParamName == "module")
{
    // Expected managed argument contract.
}
#endif

using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);

byte[] payload = [0x4E, 0x55, 0x47, 0x45, 0x54, 0x2D, 0x41, 0x52, 0x43, 0x48];

if (session.Memory.Write(allocation.Address, payload) != payload.Length)
    throw new InvalidOperationException("NuGet consumer short write.");

var copy = session.Memory.Read(allocation.Address, payload.Length);
if (!copy.SequenceEqual(payload))
    throw new InvalidOperationException("NuGet consumer read-back mismatch.");

#if LIBMEM_NET_TEST_V25_SCAN_APIS
// Verify all five 2.5 Try methods for published 2.5.0 and local 2.5 development packages.
var scanner = session.Scanner;
string mask = new('x', payload.Length);
string signature = string.Join(" ", payload.Select(b => b.ToString("X2")));
ulong exactWindow = (ulong)payload.Length;
if (!scanner.TryDataScan(payload, allocation.Address, exactWindow, out var dataAddress)
    || dataAddress != allocation.Address)
    throw new InvalidOperationException("Local package TryDataScan exact-window hit failed.");
if (!scanner.TryPatternScan(payload, mask, allocation.Address, exactWindow, out var patternAddress)
    || patternAddress != allocation.Address)
    throw new InvalidOperationException("Local package TryPatternScan exact-window hit failed.");
if (!scanner.TrySigScan(signature, allocation.Address, exactWindow, out var signatureAddress)
    || signatureAddress != allocation.Address)
    throw new InvalidOperationException("Local package TrySigScan exact-window hit failed.");
if (scanner.TryDataScan([0xDE, 0xAD, 0xBE, 0xEF], allocation.Address, exactWindow, out var missAddress)
    || missAddress != 0)
    throw new InvalidOperationException("Local package TryDataScan miss must return false and zero.");
if (!session.Memory.TryWrite(allocation.Address, payload, out var writeCount) || writeCount != payload.Length)
    throw new InvalidOperationException("Local package TryWrite did not complete.");
if (!session.Memory.TryRead(allocation.Address, payload.Length, out var readData) || !readData.SequenceEqual(payload))
    throw new InvalidOperationException("Local package TryRead did not round trip.");
if (!session.Memory.TryRead(allocation.Address, 0, out var emptyRead) || emptyRead.Length != 0)
    throw new InvalidOperationException("Local package TryRead zero-length contract failed.");
if (!session.Memory.TryWrite(allocation.Address, [], out var emptyWriteCount) || emptyWriteCount != 0)
    throw new InvalidOperationException("Local package TryWrite empty-data contract failed.");
#endif

Console.WriteLine(
    $"NUGET CONSUMER PASS pid={session.Pid} allocation=0x{allocation.Address:X}");
