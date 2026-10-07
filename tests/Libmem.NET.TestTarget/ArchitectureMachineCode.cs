using System.Runtime.InteropServices;

namespace Libmem.NET.TestTarget;

internal interface IMachineCodeFixture
{
    string Name { get; }

    byte[] CreateReturnConstant(int value, int size = 64);

    int ExpectedPatchedBytes(ulong source, ulong destination);
}

internal static class MachineCodeFixture
{
    internal static IMachineCodeFixture Current { get; } =
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => X86MachineCodeFixture.Instance,
            Architecture.X64 => X64MachineCodeFixture.Instance,
            Architecture.Arm64 => Arm64MachineCodeFixture.Instance,
            var architecture => throw new PlatformNotSupportedException(
                $"No machine-code fixture backend is defined for {architecture}.")
        };
}

internal sealed class X86MachineCodeFixture : IMachineCodeFixture
{
    internal static X86MachineCodeFixture Instance { get; } = new();

    public string Name => "x86";

    public byte[] CreateReturnConstant(int value, int size = 64)
    {
        if (size < 16)
            throw new ArgumentOutOfRangeException(nameof(size));

        var code = Enumerable.Repeat((byte)0x90, size).ToArray();

        // mov eax, imm32 (5 bytes), followed by NOP padding and ret.
        // The 5-byte first instruction gives the x86 hook path a deterministic
        // instruction boundary without exposing that assumption to the test driver.
        code[0] = 0xB8;
        BitConverter.GetBytes(value).CopyTo(code, 1);
        code[^1] = 0xC3;
        return code;
    }

    public int ExpectedPatchedBytes(ulong source, ulong destination) => 5;
}

internal sealed class X64MachineCodeFixture : IMachineCodeFixture
{
    internal static X64MachineCodeFixture Instance { get; } = new();

    public string Name => "x64";

    public byte[] CreateReturnConstant(int value, int size = 64)
    {
        if (size < 24)
            throw new ArgumentOutOfRangeException(nameof(size));

        var code = Enumerable.Repeat((byte)0x90, size).ToArray();

        // mov r10, 0x1122334455667788 (10 bytes)
        // followed by mov eax, imm32 (5 bytes). This deliberately forces
        // HookCode to respect x64 instruction boundaries.
        code[0] = 0x49;
        code[1] = 0xBA;
        BitConverter.GetBytes(0x1122334455667788UL).CopyTo(code, 2);
        code[10] = 0xB8;
        BitConverter.GetBytes(value).CopyTo(code, 11);
        code[^1] = 0xC3;
        return code;
    }

    public int ExpectedPatchedBytes(ulong source, ulong destination)
    {
        var relative = checked((long)destination - (long)source - 5L);
        var usesRelativeJump = relative >= int.MinValue && relative <= int.MaxValue;
        return usesRelativeJump ? 10 : 15;
    }
}

internal sealed class Arm64MachineCodeFixture : IMachineCodeFixture
{
    internal static Arm64MachineCodeFixture Instance { get; } = new();

    public string Name => "arm64";

    public byte[] CreateReturnConstant(int value, int size = 64) =>
        throw new PlatformNotSupportedException(
            "ARM64 test-target machine code is reserved for the future ARM64 support phase.");

    public int ExpectedPatchedBytes(ulong source, ulong destination) =>
        throw new PlatformNotSupportedException(
            "ARM64 hook patch expectations are reserved for the future ARM64 support phase.");
}
