#include "../LibmemCli.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;

AssemblyManager::AssemblyManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ AssemblyManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("AssemblyManager");
    return session_->Target;
}
LibmemCli::Architecture AssemblyManager::Architecture::get() {
    return Target()->Architecture;
}
array<Byte>^ AssemblyManager::Assemble(String^ code,UInt64 runtimeAddress) {
    auto target=Target();
    return Libmem::Assemble(code,target->Architecture,runtimeAddress);
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
    return Libmem::CodeLength(Target(),address,minimumLength);
}
