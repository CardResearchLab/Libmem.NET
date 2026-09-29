#include "../LibmemCli.h"

using namespace System;
using namespace LibmemCli;

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
