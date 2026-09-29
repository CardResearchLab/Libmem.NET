#include "../LibmemCli.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;

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
    if(address==0 || IsBadAddress(address))
        throw gcnew LibmemException("LM_AllocMemoryEx", "Failed to allocate memory in the target process.");
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
