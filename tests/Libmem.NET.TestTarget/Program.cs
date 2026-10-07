using System.Globalization;
using System.Runtime.InteropServices;

const int allocationSize = 4096;
const uint memCommitReserve = 0x3000;
const uint memRelease = 0x8000;
const uint pageExecuteReadWrite = 0x40;

static byte[] ReturnConstant(int value, int size = 64)
{
    if (size < 24)
        throw new ArgumentOutOfRangeException(nameof(size));

    var code = Enumerable.Repeat((byte)0x90, size).ToArray();

    // A 10-byte instruction forces HookCode to respect an instruction boundary:
    // mov r10, 0x1122334455667788
    code[0] = 0x49;
    code[1] = 0xBA;
    BitConverter.GetBytes(0x1122334455667788UL).CopyTo(code, 2);

    // mov eax, imm32
    code[10] = 0xB8;
    BitConverter.GetBytes(value).CopyTo(code, 11);

    code[^1] = 0xC3; // ret
    return code;
}

static int CallAddress(ulong address)
{
    var pointer = new IntPtr(unchecked((long)address));
    var function = Marshal.GetDelegateForFunctionPointer<NoArgsDelegate>(pointer);
    return function();
}

static ulong ParseAddress(string text)
{
    if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Expected hexadecimal address, got: {text}");

    return ulong.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

byte[] payload =
[
    0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
    0x54, 0x41, 0x52, 0x47, 0x45, 0x54
];

var allocation = Marshal.AllocHGlobal(allocationSize);
var hookSource = NativeMethods.VirtualAlloc(
    IntPtr.Zero,
    (nuint)allocationSize,
    memCommitReserve,
    pageExecuteReadWrite);
var hookDestination = NativeMethods.VirtualAlloc(
    IntPtr.Zero,
    (nuint)allocationSize,
    memCommitReserve,
    pageExecuteReadWrite);

if (hookSource == IntPtr.Zero || hookDestination == IntPtr.Zero)
{
    if (hookSource != IntPtr.Zero)
        NativeMethods.VirtualFree(hookSource, 0, memRelease);
    if (hookDestination != IntPtr.Zero)
        NativeMethods.VirtualFree(hookDestination, 0, memRelease);
    Marshal.FreeHGlobal(allocation);
    throw new InvalidOperationException(
        $"Could not allocate executable Hook test pages. Win32Error={Marshal.GetLastWin32Error()}");
}

try
{
    Marshal.Copy(payload, 0, allocation, payload.Length);

    var sourceCode = ReturnConstant(1);
    var destinationCode = ReturnConstant(2);
    Marshal.Copy(sourceCode, 0, hookSource, sourceCode.Length);
    Marshal.Copy(destinationCode, 0, hookDestination, destinationCode.Length);

    var currentProcess = NativeMethods.GetCurrentProcess();
    if (!NativeMethods.FlushInstructionCache(currentProcess, hookSource, (nuint)sourceCode.Length)
        || !NativeMethods.FlushInstructionCache(currentProcess, hookDestination, (nuint)destinationCode.Length))
    {
        throw new InvalidOperationException(
            $"Could not flush generated Hook test code. Win32Error={Marshal.GetLastWin32Error()}");
    }

    Console.WriteLine(
        $"READY pid={Environment.ProcessId} address=0x{allocation.ToInt64():X} size={allocationSize} " +
        $"hookSource=0x{hookSource.ToInt64():X} hookDestination=0x{hookDestination.ToInt64():X}");
    Console.Out.Flush();

    while (Console.ReadLine() is { } command)
    {
        if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("PONG");
            Console.Out.Flush();
            continue;
        }

        if (command.StartsWith("call ", StringComparison.OrdinalIgnoreCase))
        {
            var address = ParseAddress(command[5..].Trim());
            Console.WriteLine($"RESULT {CallAddress(address)}");
            Console.Out.Flush();
            continue;
        }

        if (command.StartsWith("protect ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                Console.WriteLine("ERROR protect");
                Console.Out.Flush();
                continue;
            }

            var address = ParseAddress(parts[1]);
            var protectionText = parts[2].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? parts[2][2..]
                : parts[2];
            var protection = uint.Parse(
                protectionText,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);

            if (!NativeMethods.VirtualProtect(
                    new IntPtr(unchecked((long)address)),
                    1,
                    protection,
                    out var oldProtection))
            {
                Console.WriteLine($"ERROR protect {Marshal.GetLastWin32Error()}");
            }
            else
            {
                Console.WriteLine($"PROTECT old=0x{oldProtection:X}");
            }

            Console.Out.Flush();
            continue;
        }

        Console.WriteLine("UNKNOWN");
        Console.Out.Flush();
    }
}
finally
{
    NativeMethods.VirtualFree(hookSource, 0, memRelease);
    NativeMethods.VirtualFree(hookDestination, 0, memRelease);
    Marshal.FreeHGlobal(allocation);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NoArgsDelegate();

internal static class NativeMethods
{
    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlushInstructionCache(
        IntPtr process,
        IntPtr baseAddress,
        nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAlloc(
        IntPtr address,
        nuint size,
        uint allocationType,
        uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualProtect(
        IntPtr address,
        nuint size,
        uint newProtection,
        out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFree(
        IntPtr address,
        nuint size,
        uint freeType);
}
