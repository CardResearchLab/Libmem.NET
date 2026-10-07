using Libmem.NET;

static byte[] ReturnConstant(int value)
{
    var code = Enumerable.Repeat((byte)0x90, 64).ToArray();
    code[0] = 0xB8; // mov eax, imm32
    BitConverter.GetBytes(value).CopyTo(code, 1);
    code[^1] = 0xC3; // ret
    return code;
}

static unsafe int CallNoArgs(ulong address)
{
    var fn = (delegate* unmanaged<int>)(void*)address;
    return fn();
}

using var session = ProcessSession.Open((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not open the current process.");

using var source = session.Memory.Allocate(4096, MemoryProtection.ExecuteReadWrite)
    ?? throw new InvalidOperationException("Could not allocate source code.");
using var destination = session.Memory.Allocate(4096, MemoryProtection.ExecuteReadWrite)
    ?? throw new InvalidOperationException("Could not allocate destination code.");

var sourceCode = ReturnConstant(1);
var destinationCode = ReturnConstant(2);

if (session.Memory.Write(source.Address, sourceCode) != sourceCode.Length ||
    session.Memory.Write(destination.Address, destinationCode) != destinationCode.Length)
    throw new InvalidOperationException("Could not write generated code.");

Console.WriteLine($"Before hook: {CallNoArgs(source.Address)}");

try
{
    using var hook = session.Hooks.Install(source.Address, destination.Address);

    Console.WriteLine($"Hooked:      {CallNoArgs(source.Address)}");
    Console.WriteLine($"Original:    {CallNoArgs(hook.Trampoline)}");
    Console.WriteLine($"Patched:     {hook.PatchedBytes} byte(s)");

    if (!hook.Remove())
        throw new InvalidOperationException("Hook removal failed; ownership is retained and may be retried.");

    Console.WriteLine($"Restored:    {CallNoArgs(source.Address)}");
}
catch (LibmemException ex)
{
    Console.Error.WriteLine($"{ex.Operation}: {ex.Message}");
}
