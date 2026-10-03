#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace ::Libmem::Net;

AssemblyManager::AssemblyManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ AssemblyManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("AssemblyManager");
    return session_->Target;
}
::Libmem::Net::Architecture AssemblyManager::Architecture::get() {
    return Target()->Architecture;
}
array<Byte>^ AssemblyManager::Assemble(String^ code,UInt64 runtimeAddress) {
    auto target=Target();
    auto payload=Libmem::Assemble(code,target->Architecture,runtimeAddress);
    if(payload==nullptr)
        throw gcnew LibmemException("LM_AssembleEx", "Failed to assemble code for the target architecture.");
    return payload;
}
List<InstructionInfo^>^ AssemblyManager::Disassemble(array<Byte>^ code,UInt64 instructionCount,UInt64 runtimeAddress) {
    auto target=Target();
    return Libmem::Disassemble(code,target->Architecture,instructionCount,runtimeAddress);
}
List<InstructionInfo^>^ AssemblyManager::Disassemble(UInt64 address,UInt64 maxBytes,UInt64 instructionCount,UInt64 runtimeAddress) {
    auto target=Target();
    if(maxBytes==0) throw gcnew ArgumentOutOfRangeException("maxBytes");
    if(maxBytes>static_cast<UInt64>(Int32::MaxValue)) throw gcnew ArgumentOutOfRangeException("maxBytes");
    auto bytes=Libmem::ReadMemory(target,address,static_cast<int>(maxBytes));
    if(bytes->Length==0) return gcnew List<InstructionInfo^>();
    return Libmem::Disassemble(bytes,target->Architecture,instructionCount,runtimeAddress);
}
UInt64 AssemblyManager::CodeLength(UInt64 address,UInt64 minimumLength) {
    auto length=Libmem::CodeLength(Target(),address,minimumLength);
    if(minimumLength>0 && length==0)
        throw gcnew LibmemException("LM_CodeLengthEx", "Failed to calculate code length in the target process.");
    return length;
}
