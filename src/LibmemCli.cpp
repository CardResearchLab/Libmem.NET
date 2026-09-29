#include "LibmemCli.h"
#include "Interop/NativeConverter.h"
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
using namespace LibmemCli::Interop;

namespace {
    array<Byte>^ read_common(const lm_process_t* processInfo, UInt64 address, int count) {
        if(count < 0) throw gcnew ArgumentOutOfRangeException("count");

        array<Byte>^ bytes = gcnew array<Byte>(count);
        if(!count) return bytes;

        pin_ptr<Byte> destination = &bytes[0];
        lm_size_t read = processInfo
            ? LM_ReadMemoryEx(processInfo, native_address(address, "address"), destination, count)
            : LM_ReadMemory(native_address(address, "address"), destination, count);

        if(read > native_size(count, "count"))
            throw gcnew InvalidOperationException("Native read exceeded buffer.");

        if(read == native_size(count, "count")) return bytes;

        auto partial = gcnew array<Byte>(static_cast<int>(read));
        Array::Copy(bytes, partial, partial->Length);
        return partial;
    }

    int write_common(const lm_process_t* processInfo, UInt64 address, array<Byte>^ bytes) {
        if(bytes == nullptr) throw gcnew ArgumentNullException("data");
        if(!bytes->Length) return 0;

        pin_ptr<Byte> source = &bytes[0];
        lm_size_t written = processInfo
            ? LM_WriteMemoryEx(processInfo, native_address(address, "address"), source, bytes->Length)
            : LM_WriteMemory(native_address(address, "address"), source, bytes->Length);

        if(written > static_cast<lm_size_t>(bytes->Length))
            throw gcnew InvalidOperationException("Native write exceeded buffer.");

        return static_cast<int>(written);
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
