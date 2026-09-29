using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LibmemCli;

if (args.Length == 1 && args[0] == "--child")
{
    Thread.Sleep(Timeout.Infinite);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static ProcessInfo WaitForProcess(uint pid)
{
    var deadline = Stopwatch.StartNew();
    while (deadline.Elapsed < TimeSpan.FromSeconds(5))
    {
        ProcessInfo? found = Libmem.GetProcess(pid);
        if (found is not null) return found;
        Thread.Sleep(20);
    }
    throw new InvalidOperationException($"Could not resolve child PID {pid}.");
}

static bool HasExactModule(ProcessSession session, string path) =>
    session.Modules.Enumerate().Any(module =>
        string.Equals(Path.GetFullPath(module.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No test executable path.");
string runtimeLibrary = Path.Combine(AppContext.BaseDirectory, "libmem.dll");
string sleepLibrary = Path.Combine(AppContext.BaseDirectory, "LibmemCli.SleepFixture.dll");
Check(File.Exists(runtimeLibrary) && File.Exists(sleepLibrary), "Native test libraries were not copied.");

string fixtureRoot = Path.Combine(Path.GetTempPath(), "LibmemCli.RemoteTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
try
{
    string modulePath = Path.Combine(fixtureRoot, "RemoteProbe.dll");
    string slowPath = Path.Combine(fixtureRoot, "RemoteSlow.dll");
    string badPath = Path.Combine(fixtureRoot, "RemoteInvalid.dll");
    File.Copy(runtimeLibrary, modulePath);
    File.Copy(sleepLibrary, slowPath);
    File.WriteAllText(badPath, "not a DLL");

    using (Process child = Process.Start(new ProcessStartInfo(executable)
           {
               UseShellExecute = false,
               ArgumentList = { "--child" }
           }) ?? throw new InvalidOperationException("Could not start running child."))
    {
        try
        {
            ProcessInfo info = WaitForProcess((uint)child.Id);
            ProcessInfo enumerated = Libmem.EnumProcesses().Single(item => item.Pid == info.Pid);
            Check(info.StartTime == enumerated.StartTime,
                "GetProcess returned a different start time from target enumeration.");
            Check(info.StartTime != Libmem.CurrentProcess()!.StartTime,
                "GetProcess returned the caller's start time for an external PID.");

            using var session = Libmem.Attach(info)
                ?? throw new InvalidOperationException("Could not attach to the running child.");
            Check(session.IsAlive(), "Running child session is not alive.");
            Check(session.Refresh()?.StartTime == info.StartTime, "Running child refresh lost its identity.");

            ModuleInfo? loaded = session.Modules.Load(modulePath, 10000);
            Check(loaded is not null &&
                string.Equals(Path.GetFullPath(loaded.Path), modulePath, StringComparison.OrdinalIgnoreCase),
                "Running child module load did not resolve the exact path.");

            string sameA = Path.Combine(fixtureRoot, "a", "SharedName.dll");
            string sameB = Path.Combine(fixtureRoot, "b", "SharedName.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(sameA)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sameB)!);
            File.Copy(runtimeLibrary, sameA);
            File.Copy(runtimeLibrary, sameB);
            Check(session.Modules.Load(sameA, 10000) is not null, "First same-name DLL did not load.");
            ModuleInfo? second = session.Modules.Load(sameB, 10000);
            Check(second is null ||
                string.Equals(Path.GetFullPath(second.Path), sameB, StringComparison.OrdinalIgnoreCase),
                "A same-name DLL was falsely resolved to the other path.");

            Check(session.Modules.Load(badPath, 10000) is null, "Invalid DLL was reported as loaded.");
            try
            {
                session.Modules.Load(Path.Combine(fixtureRoot, "missing.dll"), 10000);
                throw new InvalidOperationException("Missing DLL was accepted.");
            }
            catch (FileNotFoundException) { }

            try
            {
                session.Modules.Load(slowPath, 25);
                throw new InvalidOperationException("Slow DLL did not time out.");
            }
            catch (TimeoutException) { }
            var lateDeadline = Stopwatch.StartNew();
            while (!HasExactModule(session, slowPath) && lateDeadline.Elapsed < TimeSpan.FromSeconds(5))
                Thread.Sleep(50);
            Check(HasExactModule(session, slowPath), "Timed-out load did not later complete.");

            session.Detach();
            using var afterDetach = Libmem.Attach(info)
                ?? throw new InvalidOperationException("Could not reattach to the child.");
            Check(HasExactModule(afterDetach, modulePath), "Detaching a session unloaded a persistent module.");
            child.Kill();
            child.WaitForExit();
            Check(!afterDetach.IsAlive(), "Exited child still appears alive.");
            try
            {
                afterDetach.Modules.Enumerate();
                throw new InvalidOperationException("Manager accepted an exited child.");
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("no longer alive")) { }
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); child.WaitForExit(); }
        }
    }

    var startup = new Win32.STARTUPINFO { cb = (uint)Marshal.SizeOf<Win32.STARTUPINFO>() };
    var commandLine = new StringBuilder($"\"{executable}\" --child");
    if (!Win32.CreateProcessW(executable, commandLine, 0, 0, false, 0x00000004,
            0, Path.GetDirectoryName(executable), ref startup, out Win32.PROCESS_INFORMATION suspended))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start suspended child.");
    try
    {
        ProcessInfo info = WaitForProcess(suspended.dwProcessId);
        using var session = Libmem.Attach(info)
            ?? throw new InvalidOperationException("Could not attach to the suspended child.");
        Check(session.IsAlive(), "Suspended child session is not alive.");
        ModuleInfo? loaded = session.Modules.Load(modulePath, 10000);
        Check(loaded is not null && HasExactModule(session, modulePath),
            "Could not load a DLL before the child main thread was resumed.");
        Check(Win32.ResumeThread(suspended.hThread) != uint.MaxValue, "Could not resume child main thread.");
        // Module enumeration can briefly fail while the resumed loader initializes.
        bool stillLoaded = false;
        var resumeDeadline = Stopwatch.StartNew();
        while (resumeDeadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            try { stillLoaded = HasExactModule(session, modulePath); }
            catch (LibmemException) { }
            if (stillLoaded) break;
            Thread.Sleep(50);
        }
        Check(stillLoaded, "Resuming the child lost the loaded module.");
    }
    finally
    {
        Win32.TerminateProcess(suspended.hProcess, 0);
        Win32.WaitForSingleObject(suspended.hProcess, 5000);
        Win32.CloseHandle(suspended.hThread);
        Win32.CloseHandle(suspended.hProcess);
    }
}
finally
{
    if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
}

Console.WriteLine("REMOTE PROCESS TESTS PASS");

internal static class Win32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        internal uint cb;
        internal string? lpReserved;
        internal string? lpDesktop;
        internal string? lpTitle;
        internal uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        internal uint dwFillAttribute, dwFlags;
        internal ushort wShowWindow, cbReserved2;
        internal nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        internal nint hProcess, hThread;
        internal uint dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcessW(string applicationName, StringBuilder commandLine,
        nint processAttributes, nint threadAttributes, bool inheritHandles, uint creationFlags,
        nint environment, string? currentDirectory, ref STARTUPINFO startup, out PROCESS_INFORMATION process);

    [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateProcess(nint process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool CloseHandle(nint handle);
}
