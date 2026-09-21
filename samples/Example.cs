using System;
using System.Linq;
using LibmemCli;

Console.WriteLine($"Libmem process bits: {Libmem.GetBits()}");
var self = Libmem.CurrentProcess() ?? throw new InvalidOperationException("Current process not found");
Console.WriteLine($"Self: {self.Name} pid={self.Pid}");
Console.WriteLine($"Self modules: {Libmem.EnumModules(self).Count}");

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
