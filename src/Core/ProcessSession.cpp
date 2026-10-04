#include "../Libmem.NET.h"
using namespace System;
namespace Libmem::NET {

namespace {
    ProcessInfo^ CloneProcessInfo(ProcessInfo^ input) {
        if(input==nullptr) throw gcnew ArgumentNullException("process");
        return gcnew ProcessInfo(
            input->Pid,
            input->ParentPid,
            input->Architecture,
            input->Bits,
            input->StartTime,
            input->Name,
            input->Path);
    }
}

void ProcessSession::ThrowIfDisposed() {
    if(disposed_) throw gcnew ObjectDisposedException("ProcessSession");
}
ProcessSession::ProcessSession(ProcessInfo^ input) : identity_(nullptr), memory_(nullptr), modules_(nullptr), threads_(nullptr), scanner_(nullptr), symbols_(nullptr), assembly_(nullptr), hooks_(nullptr), injector_(nullptr), disposed_(false) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");
    identity_=CloneProcessInfo(input);
    memory_=gcnew MemoryManager(this);
    modules_=gcnew ModuleManager(this);
    threads_=gcnew ThreadManager(this);
    scanner_=gcnew ScanManager(this);
    symbols_=gcnew SymbolManager(this);
    assembly_=gcnew AssemblyManager(this);
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
    return CloneProcessInfo(identity_);
}
UInt32 ProcessSession::Pid::get() {
    ThrowIfDisposed();
    return identity_->Pid;
}
String^ ProcessSession::Name::get() {
    ThrowIfDisposed();
    return identity_->Name;
}
::Libmem::NET::Architecture ProcessSession::Architecture::get() {
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
SymbolManager^ ProcessSession::Symbols::get() {
    ThrowIfDisposed();
    return symbols_;
}
AssemblyManager^ ProcessSession::Assembly::get() {
    ThrowIfDisposed();
    return assembly_;
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
    identity_=CloneProcessInfo(current);
    return CloneProcessInfo(identity_);
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
    symbols_=nullptr;
    assembly_=nullptr;
    hooks_=nullptr;
    injector_=nullptr;
}
ProcessSession::~ProcessSession() { Detach(); }

} // namespace Libmem::NET
