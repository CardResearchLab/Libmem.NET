#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace ::Libmem::Net;

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
    return ::Libmem::Net::Libmem::ReadMemory(Target(),address,count);
}
int MemoryManager::Write(UInt64 address,array<Byte>^ data) {
    return ::Libmem::Net::Libmem::WriteMemory(Target(),address,data);
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
    return ::Libmem::Net::Libmem::SetMemory(Target(),address,value,size);
}
MemoryProtection MemoryManager::Protect(UInt64 address,UInt64 size,MemoryProtection protection) {
    ::Libmem::Net::Interop::native_protection(protection,"protection");
    return ::Libmem::Net::Libmem::ProtectMemory(Target(),address,size,protection);
}
RemoteAllocation^ MemoryManager::Allocate(UInt64 size,MemoryProtection protection) {
    if(size==0) throw gcnew ArgumentOutOfRangeException("size");
    ::Libmem::Net::Interop::native_protection(protection,"protection");
    auto target=Target();
    if(!::Libmem::Net::Libmem::IsProcessAlive(target)) throw gcnew InvalidOperationException("Target process is no longer alive.");
    auto address=::Libmem::Net::Libmem::AllocateMemory(target,size,protection);
    if(address==0 || IsBadAddress(address))
        throw gcnew LibmemException("LM_AllocMemoryEx", "Failed to allocate memory in the target process.");
    return gcnew RemoteAllocation(target,address,size);
}
bool MemoryManager::Free(UInt64 address,UInt64 size) {
    return ::Libmem::Net::Libmem::FreeMemory(Target(),address,size);
}
