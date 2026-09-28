using System;
using System.Linq;
using LibmemCli;

Console.WriteLine($"Libmem process bits: {Libmem.GetBits()}");
var self = Libmem.CurrentProcess() ?? throw new InvalidOperationException("Current process not found");
using var session = Libmem.Attach(self) ?? throw new InvalidOperationException("Could not attach to the current process");

Console.WriteLine($"Self: {session.Name} pid={session.Pid} arch={session.Architecture}");
Console.WriteLine($"Self modules: {session.Modules.Enumerate().Count}");

using (var allocation = session.Memory.Allocate(4096, MemoryProtection.ReadWrite))
{
    if (allocation is null)
        throw new InvalidOperationException("Remote allocation failed");

    Console.WriteLine($"Owned allocation: 0x{allocation.Address:X}, size={allocation.Size}");
}

// Safely demonstrate read/write against a buffer in THIS sample process only.
IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
try
{
    long signed = buffer.ToInt64();
    ulong address = unchecked((ulong)signed);
    self.WriteInt32(address, 123456);
    Console.WriteLine($"Read back: {self.ReadInt32(address)}");
    Console.WriteLine($"First four bytes: {BitConverter.ToString(self.Read(address, 4))}");
}
finally
{
    System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
}

var notepad = Libmem.FindProcess("notepad.exe");
Console.WriteLine(notepad is null ? "Notepad is not running" : $"Notepad PID: {notepad.Pid}");
// All failing scans return UInt64.MaxValue on x64; use that as the no-match sentinel.
