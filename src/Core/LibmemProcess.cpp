#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <algorithm>
#include <vector>

using namespace System;
using namespace System::Collections::Generic;
using namespace Libmem::Net;
using namespace ::Libmem::Net::Interop;

List<ProcessInfo^>^ Libmem::EnumProcesses() {
    std::vector<lm_process_t> native;
    if(!LM_EnumProcesses(cb_process,&native)) throw gcnew LibmemException("LM_EnumProcesses", "LM_EnumProcesses failed.");
    auto r=gcnew List<ProcessInfo^>(); for(const auto& p : native) r->Add(process(p)); return r;
}
ProcessInfo^ Libmem::CurrentProcess() { lm_process_t p{}; return LM_GetProcess(&p) ? process(p) : nullptr; }
ProcessInfo^ Libmem::GetProcess(UInt32 pid) {
    lm_process_t p{};
    if(!LM_GetProcessEx(pid,&p)) return nullptr;

    lm_process_t self{};
    if(LM_GetProcess(&self) && self.pid==p.pid)
        return process(p);

    // Pinned Windows libmem calls get_process_start_time(GetCurrentProcess())
    // inside LM_GetProcessEx instead of using the opened target-process handle.
    // Reconcile only start_time from LM_EnumProcesses so ProcessSession can keep
    // PID + start-time identity checks without accepting PID reuse.
    std::vector<lm_process_t> native;
    if(!LM_EnumProcesses(cb_process,&native)) return nullptr;
    auto match=std::find_if(native.begin(),native.end(),[pid](const lm_process_t& current) {
        return current.pid==pid;
    });
    if(match==native.end()) return nullptr;
    p.start_time=match->start_time;
    return process(p);
}
ProcessInfo^ Libmem::FindProcess(String^ name) {
    if(name==nullptr) throw gcnew ArgumentNullException("name");
    if(String::IsNullOrWhiteSpace(name))
        throw gcnew ArgumentException("Process name must not be empty.", "name");
    lm_process_t p{};
    auto n=utf8(name,"name");
    return LM_FindProcess(n.c_str(),&p) ? process(p) : nullptr;
}
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
