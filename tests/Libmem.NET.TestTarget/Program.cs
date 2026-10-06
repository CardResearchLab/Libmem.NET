using System.Globalization;
using System.Runtime.InteropServices;

const int allocationSize = 4096;
const int executablePageSize = 4096;
byte[] payload =
[
    0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
    0x54, 0x41, 0x52, 0x47, 0x45, 0x54
];

var allocation = Marshal.AllocHGlobal(allocationSize);
var executablePage = NativeMethods.VirtualAlloc(
    IntPtr.Zero,
    (nuint)executablePageSize,
    NativeMethods.MemCommit | NativeMethods.MemReserve,
    NativeMethods.PageExecuteReadWrite);

if (executablePage == IntPtr.Zero)
    throw new InvalidOperationException(
        $"VirtualAlloc failed for the Hook test page. Win32Error={Marshal.GetLastWin32Error()}");

var hookSource = executablePage;
var hookDestination = executablePage + 128;

try
{
    Marshal.Copy(payload, 0, allocation, payload.Length);

    // x64 no-argument functions used only by the external-process Hook tests:
    // source      => mov eax,1 ; nop... ; ret
    // destination => mov eax,2 ; ret
    byte[] sourceCode =
    [
        0xB8, 0x01, 0x00, 0x00, 0x00,
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0xC3
    ];
    byte[] destinationCode =
    [
        0xB8, 0x02, 0x00, 0x00, 0x00,
        0xC3
    ];

    Marshal.Copy(sourceCode, 0, hookSource, sourceCode.Length);
    Marshal.Copy(destinationCode, 0, hookDestination, destinationCode.Length);

    if (!NativeMethods.FlushInstructionCache(
            NativeMethods.GetCurrentProcess(),
            executablePage,
            (nuint)executablePageSize))
    {
        throw new InvalidOperationException(
            $"FlushInstructionCache failed for the Hook test page. Win32Error={Marshal.GetLastWin32Error()}");
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

        if (command.Equals("call-source", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"RESULT {CallNoArgs(hookSource)}");
            Console.Out.Flush();
            continue;
        }

        if (command.StartsWith("call ", StringComparison.OrdinalIgnoreCase))
        {
            var addressText = command["call ".Length..].Trim();
            if (!addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                !ulong.TryParse(
                    addressText[2..],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var address) ||
                address == 0)
            {
                Console.WriteLine("ERROR invalid-address");
                Console.Out.Flush();
                continue;
            }

            Console.WriteLine($"RESULT {CallNoArgs((IntPtr)unchecked((long)address))}");
            Console.Out.Flush();
        }
    }
}
finally
{
    NativeMethods.VirtualFree(executablePage, 0, NativeMethods.MemRelease);
    Marshal.FreeHGlobal(allocation);
}

static int CallNoArgs(IntPtr address)
{
    var function = Marshal.GetDelegateForFunctionPointer<NoArgFunction>(address);
    return function();
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NoArgFunction();

internal static class NativeMethods
{
    internal const uint MemCommit = 0x1000;
    internal const uint MemReserve = 0x2000;
    internal const uint MemRelease = 0x8000;
    internal const uint PageExecuteReadWrite = 0x40;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAlloc(
        IntPtr lpAddress,
        nuint dwSize,
        uint flAllocationType,
        uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFree(
        IntPtr lpAddress,
        nuint dwSize,
        uint dwFreeType);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlushInstructionCache(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        nuint dwSize);
}
