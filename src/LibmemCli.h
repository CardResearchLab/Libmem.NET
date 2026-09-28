#pragma once
#include <libmem/libmem.h>
using namespace System;
using namespace System::Collections::Generic;

namespace LibmemCli {
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

    [Flags] public enum class MemoryProtection : unsigned int {
        None = LM_PROT_NONE, Read = LM_PROT_R, Write = LM_PROT_W,
        Execute = LM_PROT_X, ReadWrite = LM_PROT_RW,
        ExecuteRead = LM_PROT_XR, ExecuteWrite = LM_PROT_XW,
        ExecuteReadWrite = LM_PROT_XRW
    };

    public ref class ProcessInfo sealed {
    public:
        property UInt32 Pid;
        property UInt32 ParentPid;
        property LibmemCli::Architecture Architecture;
        property UInt64 Bits;
        property UInt64 StartTime;
        property String^ Name;
        property String^ Path;
        bool IsAlive();
        array<Byte>^ Read(UInt64 address, int count);
        int Write(UInt64 address, array<Byte>^ data);
        Int32 ReadInt32(UInt64 address);
        void WriteInt32(UInt64 address, Int32 value);
        UInt64 SigScan(String^ signature, UInt64 address, UInt64 size);
    };
    public ref class ThreadInfo sealed {
    public:
        property UInt32 Id;
        property UInt32 OwnerPid;
    };
    public ref class ModuleInfo sealed {
    public:
        property UInt64 Base;
        property UInt64 End;
        property UInt64 Size;
        property String^ Name;
        property String^ Path;
    };
    public ref class SymbolInfo sealed {
    public:
        property UInt64 Address;
        property String^ Name;
    };
    public ref class SegmentInfo sealed {
    public:
        property UInt64 Base;
        property UInt64 End;
        property UInt64 Size;
        property MemoryProtection Protection;
    };
    public ref class InstructionInfo sealed {
    public:
        property UInt64 Address;
        property UInt64 Size;
        property array<Byte>^ Bytes;
        property String^ Mnemonic;
        property String^ OperandString;
    };

    // RemoteAllocation owns one allocation in a target process.
    // Explicit disposal frees the allocation; finalization never touches process memory.
    public ref class RemoteAllocation sealed : IDisposable {
    private:
        ProcessInfo^ target_;
        UInt64 address_;
        UInt64 size_;
        bool disposed_;
    internal:
        RemoteAllocation(ProcessInfo^ process, UInt64 address, UInt64 size);
    public:
        property UInt64 Address { UInt64 get(); }
        property UInt64 Size { UInt64 get(); }
        property bool IsDisposed { bool get(); }
        bool Free();
        ~RemoteAllocation();
        !RemoteAllocation();
    };

    ref class MemoryManager;
    ref class ModuleManager;

    // ProcessSession represents an attachment to one concrete process identity (PID + start time).
    // It does not own an OS process handle; it provides a stable lifetime boundary for higher-level APIs.
    public ref class ProcessSession sealed : IDisposable {
    private:
        ProcessInfo^ identity_;
        MemoryManager^ memory_;
        ModuleManager^ modules_;
        bool disposed_;
        void ThrowIfDisposed();
    internal:
        ProcessSession(ProcessInfo^ process);
        property ProcessInfo^ Target { ProcessInfo^ get(); }
    public:
        property ProcessInfo^ Info { ProcessInfo^ get(); }
        property UInt32 Pid { UInt32 get(); }
        property String^ Name { String^ get(); }
        property LibmemCli::Architecture Architecture { LibmemCli::Architecture get(); }
        property UInt64 Bits { UInt64 get(); }
        property MemoryManager^ Memory { MemoryManager^ get(); }
        property ModuleManager^ Modules { ModuleManager^ get(); }
        property bool IsDisposed { bool get(); }
        bool IsAlive();
        ProcessInfo^ Refresh();
        RemoteAllocation^ Allocate(UInt64 size, MemoryProtection protection);
        void Detach();
        ~ProcessSession();
    };

    // Session-bound memory operations. All calls target the exact process identity held by ProcessSession.
    public ref class MemoryManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        MemoryManager(ProcessSession^ session);
    public:
        array<Byte>^ Read(UInt64 address, int count);
        int Write(UInt64 address, array<Byte>^ data);
        Int32 ReadInt32(UInt64 address);
        void WriteInt32(UInt64 address, Int32 value);
        UInt64 Set(UInt64 address, Byte value, UInt64 size);
        MemoryProtection Protect(UInt64 address, UInt64 size, MemoryProtection protection);
        RemoteAllocation^ Allocate(UInt64 size, MemoryProtection protection);
        bool Free(UInt64 address, UInt64 size);
        UInt64 DeepPointer(UInt64 baseAddress, array<UInt64>^ offsets);
        UInt64 DataScan(array<Byte>^ data, UInt64 address, UInt64 scanSize);
        UInt64 PatternScan(array<Byte>^ pattern, String^ mask, UInt64 address, UInt64 scanSize);
        UInt64 SigScan(String^ signature, UInt64 address, UInt64 scanSize);
    };

    // Session-bound module operations for one concrete target process.
    public ref class ModuleManager sealed {
    private:
        ProcessSession^ session_;
        ProcessInfo^ Target();
    internal:
        ModuleManager(ProcessSession^ session);
    public:
        List<ModuleInfo^>^ Enumerate();
        ModuleInfo^ Find(String^ name);
        ModuleInfo^ Load(String^ path);
        bool Unload(ModuleInfo^ module);
    };

    // HookHandle owns a native trampoline. Explicit disposal restores the original code.
    public ref class HookHandle sealed : IDisposable {
    private:
        ProcessInfo^ target_;
        UInt64 from_, trampoline_, size_;
        bool disposed_;
    internal:
        HookHandle(ProcessInfo^ target, UInt64 from, UInt64 trampoline, UInt64 size);
    public:
        property UInt64 Trampoline { UInt64 get(); }
        property UInt64 PatchedBytes { UInt64 get(); }
        bool Remove();
        ~HookHandle();
        !HookHandle();
    };

    // Local process only. The VMT and its code must remain valid throughout this object's lifetime.
    public ref class VmtManager sealed : IDisposable {
    private:
        lm_vmt_t* native_;
    public:
        VmtManager(UInt64 vtableAddress);
        void Hook(UInt64 index, UInt64 replacementAddress);
        bool Unhook(UInt64 index);
        UInt64 GetOriginal(UInt64 index);
        void Reset();
        ~VmtManager();
        !VmtManager();
    };

    public ref class Libmem abstract sealed {
    public:
        // Process
        static List<ProcessInfo^>^ EnumProcesses();
        static ProcessInfo^ CurrentProcess();
        static ProcessInfo^ GetProcess(UInt32 pid);
        static ProcessInfo^ FindProcess(String^ name);
        // Attach creates a long-lived process context bound to PID + start time.
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
        // Memory. Read returns only bytes actually read; short writes return their actual byte count.
        static array<Byte>^ ReadMemory(UInt64 source, int count);
        static array<Byte>^ ReadMemory(ProcessInfo^ process, UInt64 source, int count);
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
        // Scan. Libmem uses UINTPTR_MAX to indicate an address was not found.
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
        // Hooks: destination must be executable native code in the respective address space.
        static HookHandle^ HookCode(UInt64 source, UInt64 destination);
        static HookHandle^ HookCode(ProcessInfo^ process, UInt64 source, UInt64 destination);
    };
}
