#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vcclr.h>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;
using namespace ::Libmem::Net;
using namespace ::Libmem::Net::Interop;

::Libmem::Net::Architecture Libmem::GetArchitecture() { return static_cast<::Libmem::Net::Architecture>(LM_GetArchitecture()); }
InstructionInfo^ Libmem::Assemble(String^ code) {
    if(code==nullptr) throw gcnew ArgumentNullException("code");
    auto s=utf8(code,"code"); lm_inst_t i{}; return LM_Assemble(s.c_str(),&i) ? instruction(i) : nullptr;
}
array<Byte>^ Libmem::Assemble(String^ code,::Libmem::Net::Architecture arch,UInt64 runtimeAddress) {
    if(code==nullptr) throw gcnew ArgumentNullException("code");
    native_architecture(arch,"architecture");
    auto s=utf8(code,"code"); lm_byte_t* payload=nullptr;
    lm_size_t n=LM_AssembleEx(s.c_str(),static_cast<lm_arch_t>(arch),native_address(runtimeAddress,"runtimeAddress"),&payload);
    if(n==0 || !payload) return nullptr;
    try {
        if(n>Int32::MaxValue) throw gcnew InvalidOperationException("Payload exceeds managed array capacity.");
        auto bytes=gcnew array<Byte>(static_cast<int>(n)); Marshal::Copy(IntPtr(payload),bytes,0,bytes->Length); return bytes;
    } finally { LM_FreePayload(payload); }
}
InstructionInfo^ Libmem::Disassemble(UInt64 address) { lm_inst_t i{}; return LM_Disassemble(native_address(address,"address"),&i) ? instruction(i) : nullptr; }
List<InstructionInfo^>^ Libmem::Disassemble(UInt64 address,::Libmem::Net::Architecture arch,UInt64 maxBytes,UInt64 count,UInt64 runtimeAddress) {
    native_architecture(arch,"architecture");
    if(!maxBytes && !count) throw gcnew ArgumentException("Specify maxBytes or instructionCount.");
    lm_inst_t* instructions=nullptr;
    lm_size_t n=LM_DisassembleEx(native_address(address,"address"),static_cast<lm_arch_t>(arch),native_size(maxBytes,"maxBytes"),native_size(count,"count"),native_address(runtimeAddress,"runtimeAddress"),&instructions);
    if(!n || !instructions) return gcnew List<InstructionInfo^>();
    try {
        auto result=gcnew List<InstructionInfo^>();
        for(lm_size_t j=0;j<n;++j) result->Add(instruction(instructions[j])); return result;
    } finally { LM_FreeInstructions(instructions); }
}
List<InstructionInfo^>^ Libmem::Disassemble(array<Byte>^ code,::Libmem::Net::Architecture arch,UInt64 count,UInt64 runtimeAddress) {
    if(code==nullptr) throw gcnew ArgumentNullException("code");
    native_architecture(arch,"architecture");
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
