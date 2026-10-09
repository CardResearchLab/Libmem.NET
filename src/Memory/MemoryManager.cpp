#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"
using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {

namespace {
    bool IsBadAddress(UInt64 value) {
        return value == UInt64::MaxValue ||
               (IntPtr::Size == 4 && value == static_cast<UInt64>(UInt32::MaxValue));
    }
}

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
bool MemoryManager::TryRead(UInt64 address,int count,array<Byte>^% data) {
    data=Read(address,count);
    return data->Length==count;
}
int MemoryManager::Write(UInt64 address,array<Byte>^ data) {
    return Libmem::WriteMemory(Target(),address,data);
}
bool MemoryManager::TryWrite(UInt64 address,array<Byte>^ data,int% bytesWritten) {
    bytesWritten=Write(address,data);
    return bytesWritten==data->Length;
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
Int64 MemoryManager::ReadInt64(UInt64 address) {
    auto bytes=Read(address,8);
    if(bytes->Length!=8) throw gcnew LibmemException("LM_ReadMemoryEx", "ReadInt64 could not read 8 bytes.");
    return BitConverter::ToInt64(bytes,0);
}
void MemoryManager::WriteInt64(UInt64 address,Int64 value) {
    if(Write(address,BitConverter::GetBytes(value))!=8)
        throw gcnew LibmemException("LM_WriteMemoryEx", "WriteInt64 could not write 8 bytes.");
}
UInt64 MemoryManager::ReadPointer(UInt64 address) {
    auto target=Target();
    if(target->Bits!=32 && target->Bits!=64)
        throw gcnew NotSupportedException("Only 32-bit and 64-bit target pointers are supported.");
    int length=target->Bits==32 ? 4 : 8;
    auto bytes=Read(address,length);
    if(bytes->Length!=length)
        throw gcnew LibmemException("LM_ReadMemoryEx", "ReadPointer could not read the complete target pointer.");
    return length==4 ? static_cast<UInt64>(BitConverter::ToUInt32(bytes,0)) : BitConverter::ToUInt64(bytes,0);
}
void MemoryManager::WritePointer(UInt64 address,UInt64 value) {
    auto target=Target();
    if(target->Bits!=32 && target->Bits!=64)
        throw gcnew NotSupportedException("Only 32-bit and 64-bit target pointers are supported.");
    if(target->Bits==32 && value>UInt32::MaxValue)
        throw gcnew ArgumentOutOfRangeException("value", "Pointer value does not fit in the 32-bit target address space.");
    auto bytes=target->Bits==32 ? BitConverter::GetBytes(static_cast<UInt32>(value)) : BitConverter::GetBytes(value);
    if(Write(address,bytes)!=bytes->Length)
        throw gcnew LibmemException("LM_WriteMemoryEx", "WritePointer could not write the complete target pointer.");
}
UInt64 MemoryManager::Set(UInt64 address,Byte value,UInt64 size) {
    return Libmem::SetMemory(Target(),address,value,size);
}
MemoryProtection MemoryManager::Protect(UInt64 address,UInt64 size,MemoryProtection protection) {
    ::Libmem::NET::Interop::native_protection(protection,"protection");
    return Libmem::ProtectMemory(Target(),address,size,protection);
}
RemoteAllocation^ MemoryManager::Allocate(UInt64 size,MemoryProtection protection) {
    if(size==0) throw gcnew ArgumentOutOfRangeException("size");
    ::Libmem::NET::Interop::native_protection(protection,"protection");
    auto target=Target();
    // The static facade validates exact identity before calling native allocation.
    auto address=Libmem::AllocateMemory(target,size,protection);
    if(address==0 || IsBadAddress(address))
        throw gcnew LibmemException("LM_AllocMemoryEx", "Failed to allocate memory in the target process.");
    return gcnew RemoteAllocation(target,address,size);
}
bool MemoryManager::Free(UInt64 address,UInt64 size) {
    // Centralize the stale-identity guard in the static facade so callers
    // cannot bypass it and Manager operations do not enumerate twice.
    return Libmem::FreeMemory(Target(),address,size);
}

} // namespace Libmem::NET
