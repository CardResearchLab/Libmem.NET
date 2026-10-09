#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {

AssemblyManager::AssemblyManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ AssemblyManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("AssemblyManager");
    return session_->Target;
}
::Libmem::NET::Architecture AssemblyManager::Architecture::get() {
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

array<Byte>^ AssemblyManager::ReadAlignedCode(UInt64 address,UInt64 minimumLength) {
    auto target=Target();
    if(minimumLength==0) return gcnew array<Byte>(0);
    if(minimumLength>static_cast<UInt64>(Int32::MaxValue))
        throw gcnew ArgumentOutOfRangeException("minimumLength", "Requested code range exceeds managed array capacity.");

    // Fast path: retain the pinned native code-length behavior for ordinary readable code.
    // LM_CodeLengthEx always requests LM_INST_MAX bytes, even for a one-byte NOP at
    // the end of a readable page. That request crosses a PAGE_NOACCESS boundary
    // and fails even though the complete requested instruction is readable.
    UInt64 length=0;
    try {
        length=CodeLength(address,minimumLength);
    } catch(LibmemException^) {
        // Retry below using bounded reads when the native instruction probe fails.
    }

    if(length>=minimumLength) {
        if(length>static_cast<UInt64>(Int32::MaxValue))
            throw gcnew ArgumentOutOfRangeException("minimumLength", "Instruction-aligned code exceeds managed array capacity.");

        auto bytes=Libmem::ReadMemory(target,address,static_cast<int>(length));
        if(bytes->LongLength==static_cast<Int64>(length))
            return bytes;
    }

    // Boundary fallback: read at most 15 bytes (the x86/x64 instruction maximum)
    // and shrink the probe if ReadProcessMemory rejects a cross-page request.
    // Decode only bytes that were actually read; never decode an uninitialized
    // tail from the native code-length scratch buffer.
    UInt64 alignedLength=0;
    while(alignedLength<minimumLength) {
        if(address>UInt64::MaxValue-alignedLength)
            throw gcnew LibmemException("LM_CodeLengthEx", "Instruction address overflow.");
        UInt64 current=address+alignedLength;
        array<Byte>^ probe=nullptr;
        for(int count=15;count>=1;--count) {
            probe=Libmem::ReadMemory(target,current,count);
            if(probe->Length>0) break;
        }
        if(probe==nullptr || probe->Length==0)
            throw gcnew LibmemException("LM_ReadMemoryEx", "Could not read the instruction at the requested target address.");

        auto instructions=Libmem::Disassemble(probe,target->Architecture,1,current);
        if(instructions->Count!=1 || instructions[0]->Size==0 ||
           instructions[0]->Size>static_cast<UInt64>(probe->Length))
            throw gcnew LibmemException("LM_CodeLengthEx", "Could not decode a complete instruction from readable target bytes.");

        auto instructionLength=instructions[0]->Size;
        if(instructionLength>static_cast<UInt64>(Int32::MaxValue)-alignedLength)
            throw gcnew ArgumentOutOfRangeException("minimumLength", "Instruction-aligned code exceeds managed array capacity.");
        alignedLength+=instructionLength;
    }

    auto complete=Libmem::ReadMemory(target,address,static_cast<int>(alignedLength));
    if(complete->LongLength!=static_cast<Int64>(alignedLength))
        throw gcnew LibmemException("LM_ReadMemoryEx", "Could not read the complete instruction-aligned code range.");
    return complete;
}

} // namespace Libmem::NET
