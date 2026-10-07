using System.Runtime.InteropServices;

internal interface ITestTargetArchitecture
{
    Architecture ProcessArchitecture { get; }

    byte[] CreateReturnConstant(int value, int size = 64);
}

internal static class TestTargetArchitecture
{
    internal static ITestTargetArchitecture Current { get; } =
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => new X86TestTargetArchitecture(),
            Architecture.X64 => new X64TestTargetArchitecture(),
            Architecture.Arm64 => new Arm64TestTargetArchitecture(),
            var architecture => throw new PlatformNotSupportedException(
                $"External-process test target does not support {architecture}.")
        };
}

internal sealed class X86TestTargetArchitecture : ITestTargetArchitecture
{
    public Architecture ProcessArchitecture => Architecture.X86;

    public byte[] CreateReturnConstant(int value, int size = 64)
    {
        if (size < 16)
            throw new ArgumentOutOfRangeException(nameof(size));

        var code = Enumerable.Repeat((byte)0x90, size).ToArray();

        // mov eax, imm32
        // Five bytes are enough for the normal x86 relative detour while the
        // remaining NOP sled leaves a safe continuation point for trampolines.
        code[0] = 0xB8;
        BitConverter.GetBytes(value).CopyTo(code, 1);
        code[^1] = 0xC3; // ret
        return code;
    }
}

internal sealed class X64TestTargetArchitecture : ITestTargetArchitecture
{
    public Architecture ProcessArchitecture => Architecture.X64;

    public byte[] CreateReturnConstant(int value, int size = 64)
    {
        if (size < 24)
            throw new ArgumentOutOfRangeException(nameof(size));

        var code = Enumerable.Repeat((byte)0x90, size).ToArray();

        // mov r10, 0x1122334455667788
        // Keep a 10-byte first instruction so the x64 fixture continues to
        // exercise instruction-boundary-aware HookCode behavior.
        code[0] = 0x49;
        code[1] = 0xBA;
        BitConverter.GetBytes(0x1122334455667788UL).CopyTo(code, 2);

        // mov eax, imm32
        code[10] = 0xB8;
        BitConverter.GetBytes(value).CopyTo(code, 11);

        code[^1] = 0xC3; // ret
        return code;
    }
}

internal sealed class Arm64TestTargetArchitecture : ITestTargetArchitecture
{
    public Architecture ProcessArchitecture => Architecture.Arm64;

    public byte[] CreateReturnConstant(int value, int size = 64) =>
        throw new PlatformNotSupportedException(
            "ARM64 external-process machine-code fixtures are reserved for the ARM64 support phase.");
}
