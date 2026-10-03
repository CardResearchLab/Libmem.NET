using System.Runtime.InteropServices;

const int allocationSize = 4096;
byte[] payload =
[
    0x4C, 0x49, 0x42, 0x4D, 0x45, 0x4D,
    0x54, 0x41, 0x52, 0x47, 0x45, 0x54
];

var allocation = Marshal.AllocHGlobal(allocationSize);

try
{
    Marshal.Copy(payload, 0, allocation, payload.Length);

    Console.WriteLine(
        $"READY pid={Environment.ProcessId} address=0x{allocation.ToInt64():X} size={allocationSize}");
    Console.Out.Flush();

    while (Console.ReadLine() is { } command)
    {
        if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("PONG");
            Console.Out.Flush();
        }
    }
}
finally
{
    Marshal.FreeHGlobal(allocation);
}
