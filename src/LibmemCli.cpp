#include "LibmemCli.h"
#include <algorithm>
#include <cstring>
#include <limits>
#include <string>
#include <vector>
#include <vcclr.h>
using namespace System;
using namespace System::Text;
using namespace System::Runtime::InteropServices;
using namespace System::Collections::Generic;
using namespace LibmemCli;

namespace {
    lm_address_t native_address(UInt64 value, String^ parameterName);
    lm_size_t native_size(UInt64 value, String^ parameterName);
    bool bad_address(UInt64 value);

    std::string utf8(String^ value) {
        if (value == nullptr) throw gcnew ArgumentNullException("value");
        if (value->IndexOf('\0') >= 0) throw gcnew ArgumentException("Embedded NUL is not supported.");
        array<Byte>^ bytes = Encoding::UTF8->GetBytes(value);
        if (bytes->Length == 0) return std::string();
        pin_ptr<Byte> p = &bytes[0];
        return std::string(reinterpret_cast<const char*>(p), bytes->Length);
    }
    String^ str(const char* text) {
        if (!text) return nullptr;
        int n = static_cast<int>(std::strlen(text));
        auto bytes = gcnew array<Byte>(n);
        if(n) Marshal::Copy(IntPtr((void*)text),bytes,0,n);
        return Encoding::UTF8->GetString(bytes);
    }
    lm_process_t proc(ProcessInfo^ input) {
        if (input == nullptr) throw gcnew ArgumentNullException("process");
        lm_process_t p{};
        p.pid = input->Pid; p.ppid = input->ParentPid; p.arch = static_cast<lm_arch_t>(input->Architecture);
        p.bits = static_cast<lm_size_t>(input->Bits); p.start_time = input->StartTime;
        std::string name = utf8(input->Name == nullptr ? String::Empty : input->Name);
        std::string path = utf8(input->Path == nullptr ? String::Empty : input->Path);
        std::memcpy(p.name, name.data(), std::min(name.size(), sizeof(p.name) - 1));
        std::memcpy(p.path, path.data(), std::min(path.size(), sizeof(p.path) - 1));
        return p;
    }
    lm_module_t mod(ModuleInfo^ input) {
        if (input == nullptr) throw gcnew ArgumentNullException("module");
        lm_module_t m{};
        m.base = native_address(input->Base,"module.Base");
        m.end = native_address(input->End,"module.End");
        m.size = native_size(input->Size,"module.Size");
        std::string name = utf8(input->Name == nullptr ? String::Empty : input->Name);
        std::string path = utf8(input->Path == nullptr ? String::Empty : input->Path);
        std::memcpy(m.name, name.data(), std::min(name.size(), sizeof(m.name) - 1));
        std::memcpy(m.path, path.data(), std::min(path.size(), sizeof(m.path) - 1));
        return m;
    }
    ProcessInfo^ process(const lm_process_t& p) {
        auto r = gcnew ProcessInfo();
        r->Pid=p.pid; r->ParentPid=p.ppid; r->Architecture=static_cast<LibmemCli::Architecture>(p.arch);
        r->Bits=p.bits; r->StartTime=p.start_time;
        r->Name=str(p.name); r->Path=str(p.path);
        return r;
    }
    ThreadInfo^ thread(const lm_thread_t& t) {
        auto r = gcnew ThreadInfo(); r->Id=t.tid; r->OwnerPid=t.owner_pid; return r;
    }
    ModuleInfo^ module(const lm_module_t& m) {
        auto r = gcnew ModuleInfo(); r->Base=m.base; r->End=m.end; r->Size=m.size;
        r->Path=str(m.path); r->Name=str(m.name); return r;
    }
    SegmentInfo^ segment(const lm_segment_t& s) {
        auto r=gcnew SegmentInfo(); r->Base=s.base; r->End=s.end; r->Size=s.size;
        r->Protection=static_cast<MemoryProtection>(s.prot); return r;
    }
    InstructionInfo^ instruction(const lm_inst_t& i) {
        auto r=gcnew InstructionInfo(); r->Address=i.address; r->Size=i.size;
        int n=static_cast<int>(std::min(static_cast<size_t>(i.size), sizeof(i.bytes)));
        r->Bytes=gcnew array<Byte>(n);
        if(n) Marshal::Copy(IntPtr((void*)i.bytes),r->Bytes,0,n);
        r->Mnemonic=str(i.mnemonic); r->OperandString=str(i.op_str); return r;
    }
    // Callbacks must remain completely native; they copy native data into temporary vectors.
    // Managed objects are created only after synchronous native enumeration returns.
#pragma managed(push, off)
    struct NativeSymbol { lm_address_t address; std::string name; };
    lm_bool_t LM_CALL cb_process(lm_process_t* p, void* ctx) {
        static_cast<std::vector<lm_process_t>*>(ctx)->push_back(*p); return LM_TRUE;
    }
    lm_bool_t LM_CALL cb_thread(lm_thread_t* p, void* ctx) {
        static_cast<std::vector<lm_thread_t>*>(ctx)->push_back(*p); return LM_TRUE;
    }
    lm_bool_t LM_CALL cb_module(lm_module_t* p, void* ctx) {
        static_cast<std::vector<lm_module_t>*>(ctx)->push_back(*p); return LM_TRUE;
    }
    lm_bool_t LM_CALL cb_segment(lm_segment_t* p, void* ctx) {
        static_cast<std::vector<lm_segment_t>*>(ctx)->push_back(*p); return LM_TRUE;
    }
    lm_bool_t LM_CALL cb_symbol(lm_symbol_t* p, void* ctx) {
        static_cast<std::vector<NativeSymbol>*>(ctx)->push_back({p->address,p->name ? p->name : ""}); return LM_TRUE;
    }
#pragma managed(pop)
    array<Byte>^ read_common(const lm_process_t* p, UInt64 address, int count) {
        if(count < 0) throw gcnew ArgumentOutOfRangeException("count");
        array<Byte>^ bytes=gcnew array<Byte>(count);
        if(!count) return bytes;
        pin_ptr<Byte> dest=&bytes[0];
        lm_size_t got=p ? LM_ReadMemoryEx(p,native_address(address,"address"),dest,count)
                        : LM_ReadMemory(native_address(address,"address"),dest,count);
        if(got > native_size(count,"count")) throw gcnew InvalidOperationException("Native read exceeded buffer.");
        if(got == native_size(count,"count")) return bytes;
        auto partial = gcnew array<Byte>(static_cast<int>(got));
        Array::Copy(bytes, partial, partial->Length);
        return partial;
    }
    int write_common(const lm_process_t* p, UInt64 address, array<Byte>^ bytes) {
        if(bytes==nullptr) throw gcnew ArgumentNullException("data");
        if(!bytes->Length) return 0;
        pin_ptr<Byte> src=&bytes[0];
        lm_size_t n=p ? LM_WriteMemoryEx(p,native_address(address,"address"),src,bytes->Length)
                      : LM_WriteMemory(native_address(address,"address"),src,bytes->Length);
        if(n>static_cast<lm_size_t>(bytes->Length)) throw gcnew InvalidOperationException("Native write exceeded buffer.");
        return static_cast<int>(n);
    }
    lm_address_t native_address(UInt64 value, String^ parameterName) {
        const UInt64 maximum=static_cast<UInt64>(std::numeric_limits<lm_address_t>::max());
        if(value>maximum)
            throw gcnew ArgumentOutOfRangeException(
                parameterName,
                "Address does not fit the current process architecture.");
        return static_cast<lm_address_t>(value);
    }
    lm_size_t native_size(UInt64 value, String^ parameterName) {
        const UInt64 maximum=static_cast<UInt64>(std::numeric_limits<lm_size_t>::max());
        if(value>maximum)
            throw gcnew ArgumentOutOfRangeException(
                parameterName,
                "Size or index does not fit the current process architecture.");
        return static_cast<lm_size_t>(value);
    }
    bool bad_address(UInt64 value) {
        return value==static_cast<UInt64>(LM_ADDRESS_BAD);
    }
    std::vector<lm_address_t> offsets(array<UInt64>^ input) {
        if (input==nullptr) throw gcnew ArgumentNullException("offsets");
        std::vector<lm_address_t> result; result.reserve(input->Length);
        for each (UInt64 item in input) result.push_back(native_address(item,"offsets"));
        return result;
    }
}

LibmemException::LibmemException(String^ operation,String^ message)
    : InvalidOperationException(message),operation_(operation) {
    if(String::IsNullOrWhiteSpace(operation))
        throw gcnew ArgumentException("Operation must not be empty.", "operation");
}
LibmemException::LibmemException(String^ operation,String^ message,Exception^ innerException)
    : InvalidOperationException(message,innerException),operation_(operation) {
    if(String::IsNullOrWhiteSpace(operation))
        throw gcnew ArgumentException("Operation must not be empty.", "operation");
}
String^ LibmemException::Operation::get() { return operation_; }

bool ProcessInfo::IsAlive() { return Libmem::IsProcessAlive(this); }
array<Byte>^ ProcessInfo::Read(UInt64 address,int count) { return Libmem::ReadMemory(this,address,count); }
int ProcessInfo::Write(UInt64 address,array<Byte>^ data) { return Libmem::WriteMemory(this,address,data); }
Int32 ProcessInfo::ReadInt32(UInt64 address) {
    auto bytes=Read(address,4);
    if(bytes->Length != 4) throw gcnew LibmemException("LM_ReadMemoryEx", "ReadInt32 could not read 4 bytes.");
    return BitConverter::ToInt32(bytes,0);
}
void ProcessInfo::WriteInt32(UInt64 address,Int32 value) {
    if(Write(address,BitConverter::GetBytes(value))!=4)
        throw gcnew LibmemException("LM_WriteMemoryEx", "WriteInt32 could not write 4 bytes.");
}
UInt64 ProcessInfo::SigScan(String^ signature,UInt64 address,UInt64 size) { return Libmem::SigScan(this,signature,address,size); }

RemoteAllocation::RemoteAllocation(ProcessInfo^ input,UInt64 address,UInt64 size)
    : target_(nullptr),address_(address),size_(size),disposed_(false) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");
    if(address==0 || bad_address(address)) throw gcnew ArgumentOutOfRangeException("address");
    if(size==0) throw gcnew ArgumentOutOfRangeException("size");
    target_=process(proc(input));
}
UInt64 RemoteAllocation::Address::get() { return address_; }
UInt64 RemoteAllocation::Size::get() { return size_; }
bool RemoteAllocation::IsDisposed::get() { return disposed_; }
bool RemoteAllocation::Free() {
    if(disposed_) return true;
    if(target_==nullptr) {
        disposed_=true;
        return true;
    }
    if(!Libmem::IsProcessAlive(target_)) {
        // The OS already reclaimed this address space when the process exited.
        disposed_=true;
        target_=nullptr;
        return true;
    }
    bool ok=Libmem::FreeMemory(target_,address_,size_);
    if(ok) {
        disposed_=true;
        target_=nullptr;
    }
    return ok;
}
RemoteAllocation::~RemoteAllocation() {
    if(disposed_) return;
    if(!Free())
        throw gcnew LibmemException(
            "LM_FreeMemoryEx",
            "Failed to free remote allocation during Dispose; the allocation remains active.");
}
RemoteAllocation::!RemoteAllocation() {
    // Never modify another process from the GC finalizer thread.
    target_=nullptr;
    disposed_=true;
}

void ProcessSession::ThrowIfDisposed() {
    if(disposed_) throw gcnew ObjectDisposedException("ProcessSession");
}
ProcessSession::ProcessSession(ProcessInfo^ input) : identity_(nullptr), memory_(nullptr), modules_(nullptr), threads_(nullptr), scanner_(nullptr), hooks_(nullptr), injector_(nullptr), disposed_(false) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");
    identity_=process(proc(input));
    memory_=gcnew MemoryManager(this);
    modules_=gcnew ModuleManager(this);
    threads_=gcnew ThreadManager(this);
    scanner_=gcnew ScanManager(this);
    hooks_=gcnew HookManager(this);
    injector_=gcnew InjectorManager(this);
}
ProcessSession^ ProcessSession::Open(UInt32 pid) { return Libmem::Attach(pid); }
ProcessSession^ ProcessSession::Open(String^ name) { return Libmem::Attach(name); }
ProcessSession^ ProcessSession::Open(ProcessInfo^ input) { return Libmem::Attach(input); }
ProcessInfo^ ProcessSession::Target::get() {
    ThrowIfDisposed();
    return identity_;
}
ProcessInfo^ ProcessSession::Info::get() {
    ThrowIfDisposed();
    return process(proc(identity_));
}
UInt32 ProcessSession::Pid::get() {
    ThrowIfDisposed();
    return identity_->Pid;
}
String^ ProcessSession::Name::get() {
    ThrowIfDisposed();
    return identity_->Name;
}
LibmemCli::Architecture ProcessSession::Architecture::get() {
    ThrowIfDisposed();
    return identity_->Architecture;
}
UInt64 ProcessSession::Bits::get() {
    ThrowIfDisposed();
    return identity_->Bits;
}
MemoryManager^ ProcessSession::Memory::get() {
    ThrowIfDisposed();
    return memory_;
}
ModuleManager^ ProcessSession::Modules::get() {
    ThrowIfDisposed();
    return modules_;
}
ThreadManager^ ProcessSession::Threads::get() {
    ThrowIfDisposed();
    return threads_;
}
ScanManager^ ProcessSession::Scanner::get() {
    ThrowIfDisposed();
    return scanner_;
}
HookManager^ ProcessSession::Hooks::get() {
    ThrowIfDisposed();
    return hooks_;
}
InjectorManager^ ProcessSession::Injector::get() {
    ThrowIfDisposed();
    return injector_;
}
bool ProcessSession::IsDisposed::get() { return disposed_; }
bool ProcessSession::IsAlive() {
    ThrowIfDisposed();
    return Libmem::IsProcessAlive(identity_);
}
ProcessInfo^ ProcessSession::Refresh() {
    ThrowIfDisposed();
    auto current=Libmem::GetProcess(identity_->Pid);
    if(current==nullptr || current->StartTime!=identity_->StartTime) return nullptr;
    identity_=current;
    return process(proc(identity_));
}
RemoteAllocation^ ProcessSession::Allocate(UInt64 size,MemoryProtection protection) {
    ThrowIfDisposed();
    return memory_->Allocate(size,protection);
}
void ProcessSession::Detach() {
    if(disposed_) return;
    disposed_=true;
    identity_=nullptr;
    memory_=nullptr;
    modules_=nullptr;
    threads_=nullptr;
    scanner_=nullptr;
    hooks_=nullptr;
    injector_=nullptr;
}
ProcessSession::~ProcessSession() { Detach(); }

MemoryManager::MemoryManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ MemoryManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("MemoryManager");
    return session_->Target;
}
array<Byte>^ MemoryManager::Read(UInt64 address,int count) {
    return Libmem::ReadMemory(Target(),address,count);
}
int MemoryManager::Write(UInt64 address,array<Byte>^ data) {
    return Libmem::WriteMemory(Target(),address,data);
}
Int32 MemoryManager::ReadInt32(UInt64 address) {
    auto bytes=Read(address,4);
    if(bytes->Length!=4) throw gcnew LibmemException("LM_ReadMemoryEx", "ReadInt32 could not read 4 bytes.");
    return BitConverter::ToInt32(bytes,0);
}
void MemoryManager::WriteInt32(UInt64 address,Int32 value) {
    if(Write(address,BitConverter::GetBytes(value))!=4)
        throw gcnew LibmemException("LM_WriteMemoryEx", "WriteInt32 could not write 4 bytes.");
}
UInt64 MemoryManager::Set(UInt64 address,Byte value,UInt64 size) {
    return Libmem::SetMemory(Target(),address,value,size);
}
MemoryProtection MemoryManager::Protect(UInt64 address,UInt64 size,MemoryProtection protection) {
    return Libmem::ProtectMemory(Target(),address,size,protection);
}
RemoteAllocation^ MemoryManager::Allocate(UInt64 size,MemoryProtection protection) {
    if(size==0) throw gcnew ArgumentOutOfRangeException("size");
    auto target=Target();
    if(!Libmem::IsProcessAlive(target)) throw gcnew InvalidOperationException("Target process is no longer alive.");
    auto address=Libmem::AllocateMemory(target,size,protection);
    if(address==0 || bad_address(address)) return nullptr;
    return gcnew RemoteAllocation(target,address,size);
}
bool MemoryManager::Free(UInt64 address,UInt64 size) {
    return Libmem::FreeMemory(Target(),address,size);
}
UInt64 MemoryManager::DeepPointer(UInt64 baseAddress,array<UInt64>^ offsets) {
    return Libmem::DeepPointer(Target(),baseAddress,offsets);
}
UInt64 MemoryManager::DataScan(array<Byte>^ data,UInt64 address,UInt64 scanSize) {
    return Libmem::DataScan(Target(),data,address,scanSize);
}
UInt64 MemoryManager::PatternScan(array<Byte>^ pattern,String^ mask,UInt64 address,UInt64 scanSize) {
    return Libmem::PatternScan(Target(),pattern,mask,address,scanSize);
}
UInt64 MemoryManager::SigScan(String^ signature,UInt64 address,UInt64 scanSize) {
    return Libmem::SigScan(Target(),signature,address,scanSize);
}

ScanManager::ScanManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ScanManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ScanManager");
    return session_->Target;
}
UInt64 ScanManager::DeepPointer(UInt64 baseAddress,array<UInt64>^ pointerOffsets) {
    return Libmem::DeepPointer(Target(),baseAddress,pointerOffsets);
}
UInt64 ScanManager::DataScan(array<Byte>^ data,UInt64 address,UInt64 scanSize) {
    return Libmem::DataScan(Target(),data,address,scanSize);
}
UInt64 ScanManager::PatternScan(array<Byte>^ pattern,String^ mask,UInt64 address,UInt64 scanSize) {
    return Libmem::PatternScan(Target(),pattern,mask,address,scanSize);
}
UInt64 ScanManager::SigScan(String^ signature,UInt64 address,UInt64 scanSize) {
    return Libmem::SigScan(Target(),signature,address,scanSize);
}

ModuleManager::ModuleManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ModuleManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ModuleManager");
    return session_->Target;
}
List<ModuleInfo^>^ ModuleManager::Enumerate() {
    return Libmem::EnumModules(Target());
}
ModuleInfo^ ModuleManager::Find(String^ name) {
    return Libmem::FindModule(Target(),name);
}
ModuleInfo^ ModuleManager::Load(String^ path) {
    return Libmem::LoadModule(Target(),path);
}
bool ModuleManager::Unload(ModuleInfo^ moduleInfo) {
    return Libmem::UnloadModule(Target(),moduleInfo);
}

ThreadManager::ThreadManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ThreadManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ThreadManager");
    return session_->Target;
}
List<ThreadInfo^>^ ThreadManager::Enumerate() {
    return Libmem::EnumThreads(Target());
}
ThreadInfo^ ThreadManager::Main::get() {
    return Libmem::GetThread(Target());
}

InjectedModuleHandle::InjectedModuleHandle(ProcessInfo^ target,ModuleInfo^ moduleInfo,String^ requestedPath)
    : target_(nullptr),module_(nullptr),requestedPath_(requestedPath),active_(true),disposed_(false) {
    if(target==nullptr) throw gcnew ArgumentNullException("target");
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    target_=process(proc(target));
    module_=module(mod(moduleInfo));
}
ModuleInfo^ InjectedModuleHandle::Module::get() {
    return module_==nullptr ? nullptr : module(mod(module_));
}
String^ InjectedModuleHandle::RequestedPath::get() { return requestedPath_; }
bool InjectedModuleHandle::IsActive::get() { return active_; }
bool InjectedModuleHandle::IsDisposed::get() { return disposed_; }
bool InjectedModuleHandle::Unload() {
    if(!active_) return true;
    if(disposed_) return false;
    if(target_==nullptr) {
        active_=false;
        return true;
    }
    if(!Libmem::IsProcessAlive(target_)) {
        // The process address space is gone, so this loader reference cannot remain active.
        active_=false;
        target_=nullptr;
        return true;
    }
    bool ok=Libmem::UnloadModule(target_,module_);
    if(ok) active_=false;
    return ok;
}
InjectedModuleHandle::~InjectedModuleHandle() {
    if(disposed_) return;
    if(active_ && !Unload())
        throw gcnew LibmemException(
            "LM_UnloadModuleEx",
            "Failed to unload injected module during Dispose; the owned load reference remains active.");
    disposed_=true;
    target_=nullptr;
}
InjectedModuleHandle::!InjectedModuleHandle() {
    // Never call FreeLibrary in another process from the GC finalizer thread.
    target_=nullptr;
    disposed_=true;
}

InjectorManager::InjectorManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ InjectorManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("InjectorManager");
    return session_->Target;
}
InjectedModuleHandle^ InjectorManager::InjectLibrary(String^ path) {
    if(String::IsNullOrWhiteSpace(path)) throw gcnew ArgumentException("Library path must not be empty.", "path");

    auto target=Target();
    if(!Libmem::IsProcessAlive(target)) throw gcnew InvalidOperationException("Target process is no longer alive.");
    if(target->Bits!=Libmem::GetBits())
        throw gcnew NotSupportedException("Cross-bitness library injection is not supported by the current runtime.");

    String^ fullPath;
    try {
        fullPath=System::IO::Path::GetFullPath(path);
    } catch(Exception^ ex) {
        throw gcnew ArgumentException("Library path is invalid.", "path", ex);
    }

    if(!System::IO::File::Exists(fullPath))
        throw gcnew System::IO::FileNotFoundException("Library to inject was not found.", fullPath);

    auto nativeTarget=proc(target);
    auto nativePath=utf8(fullPath);

    // Ask libmem only to perform the LoadLibrary operation. Its module_out lookup is
    // name/suffix based; resolve the resulting module ourselves by normalized full path
    // so same-named DLLs from different directories cannot be confused.
    if(LM_LoadModuleEx(&nativeTarget,nativePath.c_str(),nullptr)==LM_FALSE)
        throw gcnew LibmemException("LM_LoadModuleEx", "Library injection failed.");

    ModuleInfo^ loaded=nullptr;
    for each(ModuleInfo^ candidate in Libmem::EnumModules(target)) {
        if(candidate==nullptr || String::IsNullOrWhiteSpace(candidate->Path)) continue;

        String^ candidatePath;
        try {
            candidatePath=System::IO::Path::GetFullPath(candidate->Path);
        } catch(Exception^) {
            continue;
        }

        if(String::Equals(candidatePath,fullPath,StringComparison::OrdinalIgnoreCase)) {
            loaded=candidate;
            break;
        }
    }

    if(loaded==nullptr)
        throw gcnew LibmemException(
            "LM_EnumModulesEx",
            "LoadLibrary completed but the injected module could not be resolved by full path.");

    return gcnew InjectedModuleHandle(target,loaded,fullPath);
}

List<ProcessInfo^>^ Libmem::EnumProcesses() {
    std::vector<lm_process_t> native;
    if(!LM_EnumProcesses(cb_process,&native)) throw gcnew LibmemException("LM_EnumProcesses", "LM_EnumProcesses failed.");
    auto r=gcnew List<ProcessInfo^>(); for(const auto& p : native) r->Add(process(p)); return r;
}
ProcessInfo^ Libmem::CurrentProcess() { lm_process_t p{}; return LM_GetProcess(&p) ? process(p) : nullptr; }
ProcessInfo^ Libmem::GetProcess(UInt32 pid) { lm_process_t p{}; return LM_GetProcessEx(pid,&p) ? process(p) : nullptr; }
ProcessInfo^ Libmem::FindProcess(String^ name) { lm_process_t p{}; auto n=utf8(name); return LM_FindProcess(n.c_str(),&p) ? process(p) : nullptr; }
ProcessSession^ Libmem::Attach(UInt32 pid) {
    auto current=GetProcess(pid);
    return current==nullptr ? nullptr : gcnew ProcessSession(current);
}
ProcessSession^ Libmem::Attach(String^ name) {
    auto current=FindProcess(name);
    return current==nullptr ? nullptr : gcnew ProcessSession(current);
}
ProcessSession^ Libmem::Attach(ProcessInfo^ input) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");
    auto current=GetProcess(input->Pid);
    if(current==nullptr || current->StartTime!=input->StartTime) return nullptr;
    return gcnew ProcessSession(current);
}
bool Libmem::IsProcessAlive(ProcessInfo^ input) {
    auto expected=proc(input);
    lm_process_t self{};
    if(LM_GetProcess(&self) && self.pid==expected.pid)
        return LM_IsProcessAlive(&expected)!=LM_FALSE;
    std::vector<lm_process_t> native;
    if(!LM_EnumProcesses(cb_process,&native)) return false;
    return std::any_of(native.begin(),native.end(),[&expected](const lm_process_t& current) {
        return current.pid==expected.pid && current.start_time==expected.start_time;
    });
}
array<String^>^ Libmem::GetCommandLine(ProcessInfo^ input) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");

    // The pinned Windows libmem implementation only supports the current process.
    // Its LM_GetCommandLine implementation at the pinned revision also mutates the
    // supplied PID and passes an uninitialized pointer to realloc, so calling it can
    // return the wrong process command line or trigger undefined behavior. Preserve
    // the intended upstream contract without exposing that native bug to managed code.
    lm_process_t current{};
    if(!LM_GetProcess(&current))
        throw gcnew LibmemException("LM_GetProcess", "Could not resolve the current process for GetCommandLine.");

    if(input->Pid!=current.pid || input->StartTime!=current.start_time)
        return nullptr;

    return Environment::GetCommandLineArgs();
}
UInt64 Libmem::GetBits() { return LM_GetBits(); }
UInt64 Libmem::GetSystemBits() { return LM_GetSystemBits(); }

List<ThreadInfo^>^ Libmem::EnumThreads() {
    std::vector<lm_thread_t> native;
    if(!LM_EnumThreads(cb_thread,&native)) throw gcnew LibmemException("LM_EnumThreads", "LM_EnumThreads failed.");
    auto r=gcnew List<ThreadInfo^>(); for(const auto& t : native) r->Add(thread(t)); return r;
}
List<ThreadInfo^>^ Libmem::EnumThreads(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_thread_t> native;
    if(!LM_EnumThreadsEx(&p,cb_thread,&native)) throw gcnew LibmemException("LM_EnumThreadsEx", "LM_EnumThreadsEx failed.");
    auto r=gcnew List<ThreadInfo^>(); for(const auto& t : native) r->Add(thread(t)); return r;
}
ThreadInfo^ Libmem::CurrentThread() { lm_thread_t t{}; return LM_GetThread(&t) ? thread(t) : nullptr; }
ThreadInfo^ Libmem::GetThread(ProcessInfo^ input) { auto p=proc(input); lm_thread_t t{}; return LM_GetThreadEx(&p,&t) ? thread(t) : nullptr; }
ProcessInfo^ Libmem::GetThreadProcess(ThreadInfo^ input) {
    if(input==nullptr) throw gcnew ArgumentNullException("thread");
    lm_thread_t t{input->Id,input->OwnerPid}; lm_process_t p{};
    return LM_GetThreadProcess(&t,&p) ? process(p) : nullptr;
}

List<ModuleInfo^>^ Libmem::EnumModules() {
    std::vector<lm_module_t> native;
    if(!LM_EnumModules(cb_module,&native)) throw gcnew LibmemException("LM_EnumModules", "LM_EnumModules failed.");
    auto r=gcnew List<ModuleInfo^>(); for(const auto& m : native) r->Add(module(m)); return r;
}
List<ModuleInfo^>^ Libmem::EnumModules(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_module_t> native;
    if(!LM_EnumModulesEx(&p,cb_module,&native)) throw gcnew LibmemException("LM_EnumModulesEx", "LM_EnumModulesEx failed.");
    auto r=gcnew List<ModuleInfo^>(); for(const auto& m : native) r->Add(module(m)); return r;
}
ModuleInfo^ Libmem::FindModule(String^ name) { lm_module_t m{}; auto n=utf8(name); return LM_FindModule(n.c_str(),&m) ? module(m) : nullptr; }
ModuleInfo^ Libmem::FindModule(ProcessInfo^ input,String^ name) { auto p=proc(input); lm_module_t m{}; auto n=utf8(name); return LM_FindModuleEx(&p,n.c_str(),&m) ? module(m) : nullptr; }
ModuleInfo^ Libmem::LoadModule(String^ path) { lm_module_t m{}; auto s=utf8(path); return LM_LoadModule(s.c_str(),&m) ? module(m) : nullptr; }
ModuleInfo^ Libmem::LoadModule(ProcessInfo^ input,String^ path) { auto p=proc(input); lm_module_t m{}; auto s=utf8(path); return LM_LoadModuleEx(&p,s.c_str(),&m) ? module(m) : nullptr; }
bool Libmem::UnloadModule(ModuleInfo^ input) { auto m=mod(input); return LM_UnloadModule(&m)!=LM_FALSE; }
bool Libmem::UnloadModule(ProcessInfo^ input,ModuleInfo^ m) { auto p=proc(input); auto native=mod(m); return LM_UnloadModuleEx(&p,&native)!=LM_FALSE; }

List<SymbolInfo^>^ Libmem::EnumSymbols(ModuleInfo^ input,bool demangle) {
    auto m=mod(input); std::vector<NativeSymbol> native;
    bool ok=demangle ? LM_EnumSymbolsDemangled(&m,cb_symbol,&native)!=LM_FALSE : LM_EnumSymbols(&m,cb_symbol,&native)!=LM_FALSE;
    if(!ok) throw gcnew LibmemException(
        demangle ? "LM_EnumSymbolsDemangled" : "LM_EnumSymbols",
        demangle ? "LM_EnumSymbolsDemangled failed." : "LM_EnumSymbols failed.");
    auto r=gcnew List<SymbolInfo^>(); for(const auto& s : native) {
        auto x=gcnew SymbolInfo(); x->Address=s.address; x->Name=str(s.name.c_str()); r->Add(x);
    } return r;
}
UInt64 Libmem::FindSymbolAddress(ModuleInfo^ input,String^ name,bool demangle) {
    auto m=mod(input); auto s=utf8(name);
    return demangle ? LM_FindSymbolAddressDemangled(&m,s.c_str()) : LM_FindSymbolAddress(&m,s.c_str());
}
String^ Libmem::DemangleSymbol(String^ name) {
    auto s=utf8(name); lm_char_t* output=LM_DemangleSymbol(s.c_str(),nullptr,0);
    if(!output) return nullptr;
    try { return str(output); } finally { LM_FreeDemangledSymbol(output); }
}
List<SegmentInfo^>^ Libmem::EnumSegments() {
    std::vector<lm_segment_t> native;
    if(!LM_EnumSegments(cb_segment,&native)) throw gcnew LibmemException("LM_EnumSegments", "LM_EnumSegments failed.");
    auto r=gcnew List<SegmentInfo^>(); for(const auto& s : native) r->Add(segment(s)); return r;
}
List<SegmentInfo^>^ Libmem::EnumSegments(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_segment_t> native;
    if(!LM_EnumSegmentsEx(&p,cb_segment,&native)) throw gcnew LibmemException("LM_EnumSegmentsEx", "LM_EnumSegmentsEx failed.");
    auto r=gcnew List<SegmentInfo^>(); for(const auto& s : native) r->Add(segment(s)); return r;
}
SegmentInfo^ Libmem::FindSegment(UInt64 a) { lm_segment_t s{}; return LM_FindSegment(native_address(a,"address"),&s) ? segment(s) : nullptr; }
SegmentInfo^ Libmem::FindSegment(ProcessInfo^ input,UInt64 a) { auto p=proc(input); lm_segment_t s{}; return LM_FindSegmentEx(&p,native_address(a,"address"),&s) ? segment(s) : nullptr; }

array<Byte>^ Libmem::ReadMemory(UInt64 a,int size) { return read_common(nullptr,a,size); }
array<Byte>^ Libmem::ReadMemory(ProcessInfo^ input,UInt64 a,int size) { auto p=proc(input); return read_common(&p,a,size); }
int Libmem::WriteMemory(UInt64 a,array<Byte>^ data) { return write_common(nullptr,a,data); }
int Libmem::WriteMemory(ProcessInfo^ input,UInt64 a,array<Byte>^ data) { auto p=proc(input); return write_common(&p,a,data); }
UInt64 Libmem::SetMemory(UInt64 a,Byte value,UInt64 size) { return LM_SetMemory(native_address(a,"address"),value,native_size(size,"size")); }
UInt64 Libmem::SetMemory(ProcessInfo^ input,UInt64 a,Byte value,UInt64 size) { auto p=proc(input); return LM_SetMemoryEx(&p,native_address(a,"address"),value,native_size(size,"size")); }
MemoryProtection Libmem::ProtectMemory(UInt64 a,UInt64 size,MemoryProtection prot) {
    lm_prot_t old{};
    if(!LM_ProtMemory(native_address(a,"address"),native_size(size,"size"),static_cast<lm_prot_t>(prot),&old)) throw gcnew LibmemException("LM_ProtMemory", "LM_ProtMemory failed.");
    return static_cast<MemoryProtection>(old);
}
MemoryProtection Libmem::ProtectMemory(ProcessInfo^ input,UInt64 a,UInt64 size,MemoryProtection prot) {
    auto p=proc(input); lm_prot_t old{};
    if(!LM_ProtMemoryEx(&p,native_address(a,"address"),native_size(size,"size"),static_cast<lm_prot_t>(prot),&old)) throw gcnew LibmemException("LM_ProtMemoryEx", "LM_ProtMemoryEx failed.");
    return static_cast<MemoryProtection>(old);
}
UInt64 Libmem::AllocateMemory(UInt64 size,MemoryProtection prot) { return LM_AllocMemory(native_size(size,"size"),static_cast<lm_prot_t>(prot)); }
UInt64 Libmem::AllocateMemory(ProcessInfo^ input,UInt64 size,MemoryProtection prot) { auto p=proc(input); return LM_AllocMemoryEx(&p,native_size(size,"size"),static_cast<lm_prot_t>(prot)); }
bool Libmem::FreeMemory(UInt64 a,UInt64 size) { return LM_FreeMemory(native_address(a,"address"),native_size(size,"size"))!=LM_FALSE; }
bool Libmem::FreeMemory(ProcessInfo^ input,UInt64 a,UInt64 size) { auto p=proc(input); return LM_FreeMemoryEx(&p,native_address(a,"address"),native_size(size,"size"))!=LM_FALSE; }
UInt64 Libmem::DeepPointer(UInt64 a,array<UInt64>^ data) {
    auto off=offsets(data); return LM_DeepPointer(native_address(a,"address"),off.empty()?nullptr:off.data(),off.size());
}
UInt64 Libmem::DeepPointer(ProcessInfo^ input,UInt64 a,array<UInt64>^ data) {
    auto p=proc(input); auto off=offsets(data); return LM_DeepPointerEx(&p,native_address(a,"address"),off.empty()?nullptr:off.data(),off.size());
}
UInt64 Libmem::DataScan(array<Byte>^ data,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScan(raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::DataScan(ProcessInfo^ input,array<Byte>^ data,UInt64 a,UInt64 size) {
    auto p=proc(input); if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScanEx(&p,raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::PatternScan(array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("pattern"); auto m=utf8(mask);
    if(!data->Length || m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScan(raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::PatternScan(ProcessInfo^ input,array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    auto p=proc(input); if(data==nullptr) throw gcnew ArgumentNullException("pattern"); auto m=utf8(mask);
    if(!data->Length || m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScanEx(&p,raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::SigScan(String^ signature,UInt64 a,UInt64 size) { auto s=utf8(signature); return LM_SigScan(s.c_str(),native_address(a,"address"),native_size(size,"size")); }
UInt64 Libmem::SigScan(ProcessInfo^ input,String^ signature,UInt64 a,UInt64 size) { auto p=proc(input); auto s=utf8(signature); return LM_SigScanEx(&p,s.c_str(),native_address(a,"address"),native_size(size,"size")); }

LibmemCli::Architecture Libmem::GetArchitecture() { return static_cast<LibmemCli::Architecture>(LM_GetArchitecture()); }
InstructionInfo^ Libmem::Assemble(String^ code) { auto s=utf8(code); lm_inst_t i{}; return LM_Assemble(s.c_str(),&i) ? instruction(i) : nullptr; }
array<Byte>^ Libmem::Assemble(String^ code,LibmemCli::Architecture arch,UInt64 runtimeAddress) {
    auto s=utf8(code); lm_byte_t* payload=nullptr;
    lm_size_t n=LM_AssembleEx(s.c_str(),static_cast<lm_arch_t>(arch),native_address(runtimeAddress,"runtimeAddress"),&payload);
    if(n==0 || !payload) return nullptr;
    try {
        if(n>Int32::MaxValue) throw gcnew InvalidOperationException("Payload exceeds managed array capacity.");
        auto bytes=gcnew array<Byte>(static_cast<int>(n)); Marshal::Copy(IntPtr(payload),bytes,0,bytes->Length); return bytes;
    } finally { LM_FreePayload(payload); }
}
InstructionInfo^ Libmem::Disassemble(UInt64 address) { lm_inst_t i{}; return LM_Disassemble(native_address(address,"address"),&i) ? instruction(i) : nullptr; }
List<InstructionInfo^>^ Libmem::Disassemble(UInt64 address,LibmemCli::Architecture arch,UInt64 maxBytes,UInt64 count,UInt64 runtimeAddress) {
    if(!maxBytes && !count) throw gcnew ArgumentException("Specify maxBytes or instructionCount.");
    lm_inst_t* instructions=nullptr;
    lm_size_t n=LM_DisassembleEx(native_address(address,"address"),static_cast<lm_arch_t>(arch),native_size(maxBytes,"maxBytes"),native_size(count,"count"),native_address(runtimeAddress,"runtimeAddress"),&instructions);
    if(!n || !instructions) return gcnew List<InstructionInfo^>();
    try {
        auto result=gcnew List<InstructionInfo^>();
        for(lm_size_t j=0;j<n;++j) result->Add(instruction(instructions[j])); return result;
    } finally { LM_FreeInstructions(instructions); }
}
List<InstructionInfo^>^ Libmem::Disassemble(array<Byte>^ code,LibmemCli::Architecture arch,UInt64 count,UInt64 runtimeAddress) {
    if(code==nullptr) throw gcnew ArgumentNullException("code");
    if(code->Length==0) return gcnew List<InstructionInfo^>();
    pin_ptr<Byte> pinned=&code[0]; lm_inst_t* instructions=nullptr;
    lm_byte_t* raw=pinned;
    auto address=reinterpret_cast<lm_address_t>(raw);
    lm_size_t n=LM_DisassembleEx(address,static_cast<lm_arch_t>(arch),native_size(static_cast<UInt64>(code->LongLength),"code"),
                                 native_size(count,"count"),native_address(runtimeAddress,"runtimeAddress"),&instructions);
    if(!n || !instructions) return gcnew List<InstructionInfo^>();
    try {
        auto result=gcnew List<InstructionInfo^>();
        for(lm_size_t j=0;j<n;++j) result->Add(instruction(instructions[j]));
        return result;
    } finally { LM_FreeInstructions(instructions); }
}
UInt64 Libmem::CodeLength(UInt64 a,UInt64 size) { return LM_CodeLength(native_address(a,"address"),native_size(size,"size")); }
UInt64 Libmem::CodeLength(ProcessInfo^ input,UInt64 a,UInt64 size) { auto p=proc(input); return LM_CodeLengthEx(&p,native_address(a,"address"),native_size(size,"size")); }

HookManager::HookManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ HookManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("HookManager");
    return session_->Target;
}
HookHandle^ HookManager::Install(UInt64 source,UInt64 destination) {
    auto handle=Libmem::HookCode(Target(),source,destination);
    if(handle==nullptr)
        throw gcnew LibmemException("LM_HookCodeEx", "Failed to install hook in the target process.");
    return handle;
}

HookHandle::HookHandle(ProcessInfo^ target,UInt64 from,UInt64 destination,UInt64 trampoline,UInt64 size)
    : target_(nullptr),from_(from),destination_(destination),trampoline_(trampoline),size_(size),installed_(true),disposed_(false) {
    if(target!=nullptr) target_=process(proc(target));
}
UInt64 HookHandle::Source::get() { return from_; }
UInt64 HookHandle::Destination::get() { return destination_; }
UInt64 HookHandle::Trampoline::get() { return trampoline_; }
UInt64 HookHandle::PatchedBytes::get() { return size_; }
bool HookHandle::IsInstalled::get() { return installed_; }
bool HookHandle::IsDisposed::get() { return disposed_; }
bool HookHandle::Remove() {
    if(!installed_) return true;
    if(disposed_) return false;

    bool ok;
    if(target_!=nullptr) {
        if(!Libmem::IsProcessAlive(target_)) {
            // The target address space no longer exists, so the hook cannot remain installed.
            installed_=false;
            return true;
        }
        auto p=proc(target_);
        ok=LM_UnhookCodeEx(&p,native_address(from_,"source"),native_address(trampoline_,"trampoline"),native_size(size_,"size"))!=LM_FALSE;
    } else {
        ok=LM_UnhookCode(native_address(from_,"source"),native_address(trampoline_,"trampoline"),native_size(size_,"size"))!=LM_FALSE;
    }

    if(ok) installed_=false;
    return ok;
}
HookHandle::~HookHandle() {
    if(disposed_) return;
    if(installed_ && !Remove())
        throw gcnew LibmemException(
            target_!=nullptr ? "LM_UnhookCodeEx" : "LM_UnhookCode",
            "Failed to remove hook during Dispose; the hook remains installed.");
    disposed_=true;
    target_=nullptr;
}
HookHandle::!HookHandle() {
    // Never patch process code from the GC finalizer thread.
    // If explicit disposal was skipped, IsInstalled may have remained true until finalization.
    target_=nullptr;
    disposed_=true;
}

HookHandle^ Libmem::HookCode(UInt64 from,UInt64 to) {
    lm_address_t trampoline=LM_ADDRESS_BAD;
    auto n=LM_HookCode(native_address(from,"source"),native_address(to,"destination"),&trampoline);
    return n ? gcnew HookHandle(nullptr,from,to,trampoline,n) : nullptr;
}
HookHandle^ Libmem::HookCode(ProcessInfo^ input,UInt64 from,UInt64 to) {
    auto p=proc(input); lm_address_t trampoline=LM_ADDRESS_BAD;
    auto n=LM_HookCodeEx(&p,native_address(from,"source"),native_address(to,"destination"),&trampoline);
    return n ? gcnew HookHandle(input,from,to,trampoline,n) : nullptr;
}
VmtManager::VmtManager(UInt64 address) : native_(new lm_vmt_t{}), disposed_(false) {
    if(address==0 || !LM_VmtNew(reinterpret_cast<lm_address_t*>(static_cast<uintptr_t>(native_address(address,"vtableAddress"))),native_)) {
        delete native_; native_=nullptr; disposed_=true; throw gcnew LibmemException("LM_VmtNew", "LM_VmtNew failed.");
    }
}
bool VmtManager::IsDisposed::get() { return disposed_; }
bool VmtManager::ResetNative() {
    if(!native_) return true;

    // The pinned libmem LM_VmtReset reads entry->index after freeing entry.
    // Remove tracked entries one by one first so Reset/Free only see an empty list.
    while(native_->hkentries!=LM_NULLPTR) {
        auto index=native_->hkentries->index;
        if(LM_VmtUnhook(native_,index)==LM_FALSE) return false;
    }
    return true;
}
void VmtManager::Hook(UInt64 index,UInt64 to) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    if(!LM_VmtHook(native_,native_size(index,"index"),native_address(to,"destination")))
        throw gcnew LibmemException("LM_VmtHook", "LM_VmtHook failed.");
}
bool VmtManager::Unhook(UInt64 index) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    return LM_VmtUnhook(native_,native_size(index,"index"))!=LM_FALSE;
}
UInt64 VmtManager::GetOriginal(UInt64 index) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    return LM_VmtGetOriginal(native_,native_size(index,"index"));
}
void VmtManager::Reset() {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    if(!ResetNative())
        throw gcnew LibmemException(
            "LM_VmtUnhook",
            "VMT reset failed because one or more tracked hooks could not be removed.");
    // Safe after ResetNative: the upstream list is empty, avoiding its reset use-after-free path.
    LM_VmtReset(native_);
}
VmtManager::~VmtManager() {
    if(disposed_) return;
    if(native_) {
        // Explicit Dispose is deterministic. Do not discard the native bookkeeping if
        // one or more VMT entries could not be restored; callers can catch and retry.
        if(!ResetNative())
            throw gcnew LibmemException(
                "LM_VmtUnhook",
                "Failed to restore one or more VMT hooks during Dispose; the manager remains active.");
        // Safe after ResetNative: the tracked-entry list is empty, avoiding the pinned
        // upstream LM_VmtReset use-after-free path inside LM_VmtFree.
        LM_VmtFree(native_);
        delete native_;
        native_=nullptr;
    }
    disposed_=true;
}
VmtManager::!VmtManager() {
    // Do not rewrite VMT entries from the GC finalizer thread.
    // If Dispose was skipped while hooks were active, libmem's hook-entry bookkeeping may leak.
    if(native_) {
        delete native_;
        native_=nullptr;
    }
    disposed_=true;
}
