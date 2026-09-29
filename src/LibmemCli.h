#pragma once
#include <libmem/libmem.h>
using namespace System;
using namespace System::Collections::Generic;

namespace LibmemCli {
    /// <summary>CPU architectures exposed by the pinned libmem ABI.</summary>
    public enum class Architecture : unsigned int {
        Generic = LM_ARCH_GENERIC,
        ArmV7 = LM_ARCH_ARMV7, ArmV8 = LM_ARCH_ARMV8,
        ThumbV7 = LM_ARCH_THUMBV7, ThumbV8 = LM_ARCH_THUMBV8,
        ArmV7BigEndian = LM_ARCH_ARMV7EB, ThumbV7BigEndian = LM_ARCH_THUMBV7EB,
        ArmV8BigEndian = LM_ARCH_ARMV8EB, ThumbV8BigEndian = LM_ARCH_THUMBV8EB,
        Arm64 = LM_ARCH_AARCH64,
        Mips32 = LM_ARCH_MIPS, Mips64 = LM_ARCH_MIPS64,
        Mips32LittleEndian = LM_ARCH_MIPSEL, Mips64LittleEndian = LM_ARCH_MIPSEL64,
        X86_16 = LM_ARCH_X86_16, X86 = LM_ARCH_X86, X64 = LM_ARCH_X64,
        PowerPc32 = LM_ARCH_PPC32, PowerPc64 = LM_ARCH_PPC64,
        PowerPc64LittleEndian = LM_ARCH_PPC64LE,
        Sparc = LM_ARCH_SPARC, Sparc64 = LM_ARCH_SPARC64,
        SparcLittleEndian = LM_ARCH_SPARCEL, SystemZ = LM_ARCH_SYSZ
    };

    /// <summary>Memory protection flags used by allocation and protection APIs.</summary>
    [Flags] public enum class MemoryProtection : unsigned int {
        None = LM_PROT_NONE, Read = LM_PROT_R, Write = LM_PROT_W,
        Execute = LM_PROT_X, ReadWrite = LM_PROT_RW,
        ExecuteRead = LM_PROT_XR, ExecuteWrite = LM_PROT_XW,
        ExecuteReadWrite = LM_PROT_XRW
    };

    /// <summary>Represents a definite failure reported by a native libmem operation.</summary>
    /// <remarks>Normal not-found and sentinel results keep their native-style return semantics.</remarks>
    public ref class LibmemException : InvalidOperationException {
    private:
        String^ operation_;
    public:
        LibmemException(String^ operation, String^ message);
        LibmemException(String^ operation, String^ message, Exception^ innerException);
        /// <summary>Gets the native libmem operation name associated with the failure.</summary>
        property String^ Operation { String^ get(); }
    };

    /// <summary>Managed description of one native process identity.</summary>
    /// <remarks>Process identity-sensitive APIs also use StartTime to reject PID reuse.</remarks>
    public ref class ProcessInfo sealed {
    public:
        property UInt32 Pid;
        property UInt32 ParentPid;
        property LibmemCli::Architecture Architecture;
        property UInt64 Bits;
        property UInt64 StartTime;
        property String^ Name;
        property String^ Path;
        /// <summary>Checks whether this exact process identity is still alive.</summary>
        bool IsAlive();
        /// <summary>Reads up to count bytes from this process.</summary>
        /// <returns>Only the bytes actually read; the array can be shorter than count.</returns>
        array<Byte>^ Read(UInt64 address, int count);
        /// <summary>Writes data to this process.</summary>
        /// <returns>The number of bytes actually written.</returns>
        int Write(UInt64 address, array<Byte>^ data);
        Int32 ReadInt32(UInt64 address);
        void WriteInt32(UInt64 address, Int32 value);
        UInt64 SigScan(String^ signature, UInt64 address, UInt64 size);
    };
    /// <summary>Managed description of a native thread.</summary>
    public ref class ThreadInfo sealed {
    public:
        property UInt32 Id;
        property UInt32 OwnerPid;
    };
    /// <summary>Managed description of a loaded native module.</summary>
    public ref class ModuleInfo sealed {
    public:
        property UInt64 Base;
        property UInt64 End;
        property UInt64 Size;
        property String^ Name;
        property String^ Path;
    };
    /// <summary>Managed description of a native symbol and its resolved address.</summary>
    public ref class SymbolInfo sealed {
    public:
        property UInt64 Address;
        property String^ Name;
    };
    /// <summary>Managed description of a virtual-memory segment.</summary>
    public ref class SegmentInfo sealed {
    public:
        property UInt64 Base;
        property UInt64 End;
        property UInt64 Size;
        property MemoryProtection Protection;
    };
    /// <summary>Managed representation of one assembled or disassembled instruction.</summary>
    public ref class InstructionInfo sealed {
    public:
        property UInt64 Address;
        property UInt64 Size;
        property array<Byte>^ Bytes;
        property String^ Mnemonic;
        property String^ OperandString;
    };

    /// <summary>Owns one allocation in a target process.</summary>
    /// <remarks>Explicit disposal deterministically frees the allocation or surfaces failure. Finalization never modifies target-process memory.</remarks>
    public ref class RemoteAllocation sealed : IDisposable {
    private:
        ProcessInfo^ target_;
        UInt64 address_;
        UInt64 size_;
        bool disposed_;
    internal:
        RemoteAllocation(ProcessInfo^ process, UInt64 address, UInt64 size);
    public:
        /// <summary>Gets the base address of the owned allocation.</summary>
        property UInt64 Address { UInt64 get(); }
        /// <summary>Gets the allocation size in bytes.</summary>
        property UInt64 Size { UInt64 get(); }
        /// <summary>Gets whether the managed ownership lifetime has ended.</summary>
        property bool IsDisposed { bool get(); }
        /// <summary>Attempts to release the owned allocation.</summary>
        /// <remarks>Successful release, prior release, or target-process exit are treated as released; repeated calls are idempotent.</remarks>
        /// <returns>true when the allocation is released or the target address space no longer exists.</returns>
        bool Free();
        ~RemoteAllocation();
        !RemoteAllocation();
    };

    ref class MemoryManager;
    ref class ModuleManager;
    ref class ThreadManager;
    ref class ScanManager;
    ref class SymbolManager;
    ref class AssemblyManager;
    ref class HookManager;
    ref class HookHandle;
    ref class InjectorManager;
    ref class InjectedModuleHandle;

    /// <summary>Represents a managed attachment to one concrete process identity.</summary>
    /// <remarks>The session binds PID and process start time. It does not own a Windows process handle; it provides a stable lifetime and aggregation root for subsystem APIs. Target-process exit does not implicitly dispose the session; bound identity metadata remains readable until Detach or Dispose.</remarks>
    public ref class ProcessSession sealed : IDisposable {
    private:
        ProcessInfo^ identity_;
        MemoryManager^ memory_;
        ModuleManager^ modules_;
        ThreadManager^ threads_;
        ScanManager^ scanner_;
        SymbolManager^ symbols_;
        AssemblyManager^ assembly_;
        HookManager^ hooks_;
        InjectorManager^ injector_;
        bool disposed_;
        void ThrowIfDisposed();
    internal:
        ProcessSession(ProcessInfo^ process);
        property ProcessInfo^ Target { ProcessInfo^ get(); }
    public:
        /// <summary>Opens a session for a process ID.</summary>
        /// <returns>A session for the current process identity, or null if the process cannot be resolved.</returns>
        static ProcessSession^ Open(UInt32 pid);
        /// <summary>Opens a session for the first process matching name.</summary>
        /// <returns>A session, or null when no matching process is found.</returns>
        static ProcessSession^ Open(String^ name);
        /// <summary>Opens a session only if the supplied process identity still matches PID and start time.</summary>
        /// <returns>A session, or null when the identity is stale.</returns>
        static ProcessSession^ Open(ProcessInfo^ process);
        property ProcessInfo^ Info { ProcessInfo^ get(); }
        property UInt32 Pid { UInt32 get(); }
        property String^ Name { String^ get(); }
        property LibmemCli::Architecture Architecture { LibmemCli::Architecture get(); }
        property UInt64 Bits { UInt64 get(); }
        property MemoryManager^ Memory { MemoryManager^ get(); }
        property ModuleManager^ Modules { ModuleManager^ get(); }
        property ThreadManager^ Threads { ThreadManager^ get(); }
        property ScanManager^ Scanner { ScanManager^ get(); }
        property SymbolManager^ Symbols { SymbolManager^ get(); }
        property AssemblyManager^ Assembly { AssemblyManager^ get(); }
        property HookManager^ Hooks { HookManager^ get(); }
        property InjectorManager^ Injector { InjectorManager^ get(); }
        property bool IsDisposed { bool get(); }
        /// <summary>Checks whether the exact attached process identity is still alive.</summary>
        bool IsAlive();
        /// <summary>Refreshes the process metadata without accepting PID reuse.</summary>
        /// <returns>Updated process information, or null if the original process identity no longer exists.</returns>
        ProcessInfo^ Refresh();
        /// <summary>Allocates owned memory in the target process.</summary>
        /// <exception cref="LibmemException">Thrown when the native allocation definitely fails.</exception>
        RemoteAllocation^ Allocate(UInt64 size, MemoryProtection protection);
        /// <summary>Ends the session lifetime. Existing independently-owned resource handles keep their own lifetime.</summary>
        /// <remarks>This operation is idempotent.</remarks>
        void Detach();
        ~ProcessSession();
    };

    /// <summary>Session-bound target-process memory operations.</summary>
    public ref class MemoryManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        MemoryManager(ProcessSession^ session);
    public:
        /// <summary>Reads up to count bytes from the target process.</summary>
        /// <returns>Only the bytes actually read.</returns>
        array<Byte>^ Read(UInt64 address, int count);
        /// <summary>Writes bytes to the target process.</summary>
        /// <returns>The number of bytes actually written.</returns>
        int Write(UInt64 address, array<Byte>^ data);
        Int32 ReadInt32(UInt64 address);
        void WriteInt32(UInt64 address, Int32 value);
        UInt64 Set(UInt64 address, Byte value, UInt64 size);
        /// <summary>Changes target-process memory protection and returns the previous protection.</summary>
        MemoryProtection Protect(UInt64 address, UInt64 size, MemoryProtection protection);
        /// <summary>Allocates memory in the target process and returns an owning handle.</summary>
        /// <exception cref="LibmemException">Thrown when LM_AllocMemoryEx reports failure.</exception>
        RemoteAllocation^ Allocate(UInt64 size, MemoryProtection protection);
        /// <summary>Releases a target-process allocation described by address and size.</summary>
        bool Free(UInt64 address, UInt64 size);
    };

    /// <summary>Session-bound pointer-resolution and memory scanning operations.</summary>
    public ref class ScanManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        ScanManager(ProcessSession^ session);
    public:
        UInt64 DeepPointer(UInt64 baseAddress, array<UInt64>^ offsets);
        UInt64 DataScan(array<Byte>^ data, UInt64 address, UInt64 scanSize);
        UInt64 PatternScan(array<Byte>^ pattern, String^ mask, UInt64 address, UInt64 scanSize);
        UInt64 SigScan(String^ signature, UInt64 address, UInt64 scanSize);
    };

    /// <summary>Session-bound symbol enumeration, lookup, and demangling operations.</summary>
    public ref class SymbolManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        SymbolManager(ProcessSession^ session);
    public:
        List<SymbolInfo^>^ Enumerate(ModuleInfo^ module, bool demangle);
        UInt64 FindAddress(ModuleInfo^ module, String^ name, bool demangle);
        String^ Demangle(String^ name);
    };

    /// <summary>Session-bound assembly, disassembly, and code-length operations.</summary>
    /// <remarks>Operations default to the target process architecture.</remarks>
    public ref class AssemblyManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        AssemblyManager(ProcessSession^ session);
    public:
        property LibmemCli::Architecture Architecture { LibmemCli::Architecture get(); }
        /// <summary>Assembles source text for the target architecture.</summary>
        /// <exception cref="LibmemException">Thrown when LM_AssembleEx reports failure.</exception>
        array<Byte>^ Assemble(String^ code, UInt64 runtimeAddress);
        List<InstructionInfo^>^ Disassemble(array<Byte>^ code, UInt64 instructionCount, UInt64 runtimeAddress);
        List<InstructionInfo^>^ Disassemble(UInt64 address, UInt64 maxBytes, UInt64 instructionCount, UInt64 runtimeAddress);
        /// <summary>Calculates the amount of target code required to cover at least minimumLength bytes.</summary>
        /// <exception cref="LibmemException">Thrown when a non-zero query fails.</exception>
        UInt64 CodeLength(UInt64 address, UInt64 minimumLength);
    };

    /// <summary>Session-bound module enumeration, lookup, load, and unload operations.</summary>
    public ref class ModuleManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        ModuleManager(ProcessSession^ session);
    public:
        List<ModuleInfo^>^ Enumerate();
        /// <summary>Finds a loaded module by name.</summary>
        /// <returns>The module, or null for a normal miss.</returns>
        ModuleInfo^ Find(String^ name);
        /// <summary>Loads a module into the target process.</summary>
        /// <exception cref="LibmemException">Thrown when LM_LoadModuleEx reports failure.</exception>
        ModuleInfo^ Load(String^ path);
        /// <summary>Requests unload of a module from the target process.</summary>
        bool Unload(ModuleInfo^ module);
    };

    /// <summary>Session-bound thread enumeration and main-thread lookup.</summary>
    public ref class ThreadManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        ThreadManager(ProcessSession^ session);
    public:
        List<ThreadInfo^>^ Enumerate();
        property ThreadInfo^ Main { ThreadInfo^ get(); }
    };

    /// <summary>Owns one LoadLibrary reference created in the target process.</summary>
    /// <remarks>Explicit disposal releases the owned reference or surfaces failure. Finalization never calls into the target process.</remarks>
    public ref class InjectedModuleHandle sealed : IDisposable {
    private:
        ProcessInfo^ target_;
        ModuleInfo^ module_;
        String^ requestedPath_;
        bool active_;
        bool disposed_;
    internal:
        InjectedModuleHandle(ProcessInfo^ target, ModuleInfo^ module, String^ requestedPath);
    public:
        property ModuleInfo^ Module { ModuleInfo^ get(); }
        property String^ RequestedPath { String^ get(); }
        property bool IsActive { bool get(); }
        property bool IsDisposed { bool get(); }
        /// <summary>Attempts to release the load reference owned by this handle.</summary>
        /// <remarks>After successful release or target-process exit, repeated calls are idempotent.</remarks>
        bool Unload();
        ~InjectedModuleHandle();
        !InjectedModuleHandle();
    };

    /// <summary>Higher-level DLL injection API with explicit ownership semantics.</summary>
    public ref class InjectorManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        InjectorManager(ProcessSession^ session);
    public:
        /// <summary>Injects a native library into the target process and returns an owning module handle.</summary>
        /// <exception cref="NotSupportedException">Thrown for cross-bitness injection.</exception>
        /// <exception cref="LibmemException">Thrown when injection or post-load module resolution fails.</exception>
        InjectedModuleHandle^ InjectLibrary(String^ path);
    };

    /// <summary>Session-bound native code hook installation.</summary>
    public ref class HookManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        HookManager(ProcessSession^ session);
    public:
        /// <summary>Installs a hook in the target process.</summary>
        /// <exception cref="LibmemException">Thrown when LM_HookCodeEx reports failure.</exception>
        HookHandle^ Install(UInt64 source, UInt64 destination);
    };

    /// <summary>Owns one installed native hook and its trampoline.</summary>
    /// <remarks>Explicit disposal restores original code. Finalization never patches target-process code.</remarks>
    public ref class HookHandle sealed : IDisposable {
    private:
        ProcessInfo^ target_;
        UInt64 from_, destination_, trampoline_, size_;
        bool installed_;
        bool disposed_;
    internal:
        HookHandle(ProcessInfo^ target, UInt64 from, UInt64 destination, UInt64 trampoline, UInt64 size);
    public:
        property UInt64 Source { UInt64 get(); }
        property UInt64 Destination { UInt64 get(); }
        property UInt64 Trampoline { UInt64 get(); }
        property UInt64 PatchedBytes { UInt64 get(); }
        property bool IsInstalled { bool get(); }
        property bool IsDisposed { bool get(); }
        /// <summary>Attempts to restore the original code and release the installed hook.</summary>
        /// <remarks>After successful removal, repeated calls are idempotent.</remarks>
        bool Remove();
        ~HookHandle();
        !HookHandle();
    };

    /// <summary>Owns local-process VMT hook bookkeeping.</summary>
    /// <remarks>The target VMT and replacement code must remain valid for this object's lifetime. VMT operations are local-process only. Successful disposal is idempotent; operational methods after disposal throw ObjectDisposedException.</remarks>
    public ref class VmtManager sealed : IDisposable {
    private:
        lm_vmt_t* native_;
        bool disposed_;
        bool ResetNative();
    public:
        VmtManager(UInt64 vtableAddress);
        property bool IsDisposed { bool get(); }
        void Hook(UInt64 index, UInt64 replacementAddress);
        bool Unhook(UInt64 index);
        UInt64 GetOriginal(UInt64 index);
        void Reset();
        ~VmtManager();
        !VmtManager();
    };

    /// <summary>Low-level static compatibility facade over the pinned libmem C ABI.</summary>
    /// <remarks>New code should prefer ProcessSession and its subsystem managers when target binding or ownership semantics matter.</remarks>
    public ref class Libmem abstract sealed {
    public:
        // Process
        static List<ProcessInfo^>^ EnumProcesses();
        static ProcessInfo^ CurrentProcess();
        static ProcessInfo^ GetProcess(UInt32 pid);
        static ProcessInfo^ FindProcess(String^ name);
        /// <summary>Creates a session bound to PID and process start time.</summary>
        /// <returns>A session, or null when the process cannot be resolved.</returns>
        static ProcessSession^ Attach(UInt32 pid);
        static ProcessSession^ Attach(String^ name);
        static ProcessSession^ Attach(ProcessInfo^ process);
        static bool IsProcessAlive(ProcessInfo^ process);
        static array<String^>^ GetCommandLine(ProcessInfo^ process);
        static UInt64 GetBits();
        static UInt64 GetSystemBits();
        // Thread
        static List<ThreadInfo^>^ EnumThreads();
        static List<ThreadInfo^>^ EnumThreads(ProcessInfo^ process);
        static ThreadInfo^ CurrentThread();
        static ThreadInfo^ GetThread(ProcessInfo^ process);
        static ProcessInfo^ GetThreadProcess(ThreadInfo^ thread);
        // Module
        static List<ModuleInfo^>^ EnumModules();
        static List<ModuleInfo^>^ EnumModules(ProcessInfo^ process);
        static ModuleInfo^ FindModule(String^ name);
        static ModuleInfo^ FindModule(ProcessInfo^ process, String^ name);
        static ModuleInfo^ LoadModule(String^ path);
        static ModuleInfo^ LoadModule(ProcessInfo^ process, String^ path);
        static bool UnloadModule(ModuleInfo^ module);
        static bool UnloadModule(ProcessInfo^ process, ModuleInfo^ module);
        // Symbol
        static List<SymbolInfo^>^ EnumSymbols(ModuleInfo^ module, bool demangle);
        static UInt64 FindSymbolAddress(ModuleInfo^ module, String^ name, bool demangle);
        static String^ DemangleSymbol(String^ name);
        // Segment
        static List<SegmentInfo^>^ EnumSegments();
        static List<SegmentInfo^>^ EnumSegments(ProcessInfo^ process);
        static SegmentInfo^ FindSegment(UInt64 address);
        static SegmentInfo^ FindSegment(ProcessInfo^ process, UInt64 address);
        /// <summary>Reads memory from the current process.</summary>
        /// <returns>Only the bytes actually read.</returns>
        static array<Byte>^ ReadMemory(UInt64 source, int count);
        static array<Byte>^ ReadMemory(ProcessInfo^ process, UInt64 source, int count);
        /// <summary>Writes memory in the current process.</summary>
        /// <returns>The number of bytes actually written.</returns>
        static int WriteMemory(UInt64 address, array<Byte>^ data);
        static int WriteMemory(ProcessInfo^ process, UInt64 address, array<Byte>^ data);
        static UInt64 SetMemory(UInt64 address, Byte value, UInt64 size);
        static UInt64 SetMemory(ProcessInfo^ process, UInt64 address, Byte value, UInt64 size);
        static MemoryProtection ProtectMemory(UInt64 address, UInt64 size, MemoryProtection prot);
        static MemoryProtection ProtectMemory(ProcessInfo^ process, UInt64 address, UInt64 size, MemoryProtection prot);
        static UInt64 AllocateMemory(UInt64 size, MemoryProtection prot);
        static UInt64 AllocateMemory(ProcessInfo^ process, UInt64 size, MemoryProtection prot);
        static bool FreeMemory(UInt64 address, UInt64 size);
        static bool FreeMemory(ProcessInfo^ process, UInt64 address, UInt64 size);
        static UInt64 DeepPointer(UInt64 baseAddress, array<UInt64>^ offsets);
        static UInt64 DeepPointer(ProcessInfo^ process, UInt64 baseAddress, array<UInt64>^ offsets);
        /// <summary>Scans current-process memory for an exact byte sequence.</summary>
        /// <returns>The matching address, or the libmem bad-address sentinel when no match is found.</returns>
        static UInt64 DataScan(array<Byte>^ data, UInt64 address, UInt64 scanSize);
        static UInt64 DataScan(ProcessInfo^ process, array<Byte>^ data, UInt64 address, UInt64 scanSize);
        static UInt64 PatternScan(array<Byte>^ pattern, String^ mask, UInt64 address, UInt64 scanSize);
        static UInt64 PatternScan(ProcessInfo^ process, array<Byte>^ pattern, String^ mask, UInt64 address, UInt64 scanSize);
        static UInt64 SigScan(String^ signature, UInt64 address, UInt64 scanSize);
        static UInt64 SigScan(ProcessInfo^ process, String^ signature, UInt64 address, UInt64 scanSize);
        // Assembler and disassembler
        static LibmemCli::Architecture GetArchitecture();
        static InstructionInfo^ Assemble(String^ code);
        static array<Byte>^ Assemble(String^ code, LibmemCli::Architecture architecture, UInt64 runtimeAddress);
        static InstructionInfo^ Disassemble(UInt64 codeAddress);
        static List<InstructionInfo^>^ Disassemble(UInt64 codeAddress, LibmemCli::Architecture architecture, UInt64 maxBytes, UInt64 instructionCount, UInt64 runtimeAddress);
        static List<InstructionInfo^>^ Disassemble(array<Byte>^ code, LibmemCli::Architecture architecture, UInt64 instructionCount, UInt64 runtimeAddress);
        static UInt64 CodeLength(UInt64 codeAddress, UInt64 minimumLength);
        static UInt64 CodeLength(ProcessInfo^ process, UInt64 codeAddress, UInt64 minimumLength);
        /// <summary>Installs a native code hook in the current process.</summary>
        /// <remarks>The destination must point to executable native code in the same address space.</remarks>
        static HookHandle^ HookCode(UInt64 source, UInt64 destination);
        static HookHandle^ HookCode(ProcessInfo^ process, UInt64 source, UInt64 destination);
    };
}
