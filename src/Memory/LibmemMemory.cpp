#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vcclr.h>

using namespace System;
namespace Libmem::NET {
using namespace ::Libmem::NET::Interop;

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

array<Byte>^ Libmem::ReadMemory(UInt64 a,int size) { return read_common(nullptr,a,size); }
array<Byte>^ Libmem::ReadMemory(ProcessInfo^ input,UInt64 a,int size) { auto p=proc(input); return read_common(&p,a,size); }
int Libmem::WriteMemory(UInt64 a,array<Byte>^ data) { return write_common(nullptr,a,data); }
int Libmem::WriteMemory(ProcessInfo^ input,UInt64 a,array<Byte>^ data) { auto p=proc(input); return write_common(&p,a,data); }
UInt64 Libmem::SetMemory(UInt64 a,Byte value,UInt64 size) { return LM_SetMemory(native_address(a,"address"),value,native_size(size,"size")); }
UInt64 Libmem::SetMemory(ProcessInfo^ input,UInt64 a,Byte value,UInt64 size) { auto p=proc(input); return LM_SetMemoryEx(&p,native_address(a,"address"),value,native_size(size,"size")); }
MemoryProtection Libmem::ProtectMemory(UInt64 a,UInt64 size,MemoryProtection prot) {
    native_protection(prot,"prot");
    lm_prot_t old{};
    if(!LM_ProtMemory(native_address(a,"address"),native_size(size,"size"),static_cast<lm_prot_t>(prot),&old)) throw gcnew LibmemException("LM_ProtMemory", "LM_ProtMemory failed.");
    return static_cast<MemoryProtection>(old);
}
MemoryProtection Libmem::ProtectMemory(ProcessInfo^ input,UInt64 a,UInt64 size,MemoryProtection prot) {
    native_protection(prot,"prot");
    auto p=proc(input); lm_prot_t old{};
    if(!LM_ProtMemoryEx(&p,native_address(a,"address"),native_size(size,"size"),static_cast<lm_prot_t>(prot),&old)) throw gcnew LibmemException("LM_ProtMemoryEx", "LM_ProtMemoryEx failed.");
    return static_cast<MemoryProtection>(old);
}
UInt64 Libmem::AllocateMemory(UInt64 size,MemoryProtection prot) { native_protection(prot,"prot"); return LM_AllocMemory(native_size(size,"size"),static_cast<lm_prot_t>(prot)); }
UInt64 Libmem::AllocateMemory(ProcessInfo^ input,UInt64 size,MemoryProtection prot) { native_protection(prot,"prot"); auto p=proc(input); return LM_AllocMemoryEx(&p,native_size(size,"size"),static_cast<lm_prot_t>(prot)); }
bool Libmem::FreeMemory(UInt64 a,UInt64 size) { return LM_FreeMemory(native_address(a,"address"),native_size(size,"size"))!=LM_FALSE; }
bool Libmem::FreeMemory(ProcessInfo^ input,UInt64 a,UInt64 size) { auto p=proc(input); return LM_FreeMemoryEx(&p,native_address(a,"address"),native_size(size,"size"))!=LM_FALSE; }
UInt64 Libmem::DeepPointer(UInt64 a,array<UInt64>^ data) {
    auto off=offsets(data); return LM_DeepPointer(native_address(a,"address"),off.empty()?nullptr:off.data(),off.size());
}
UInt64 Libmem::DeepPointer(ProcessInfo^ input,UInt64 a,array<UInt64>^ data) {
    auto p=proc(input); auto off=offsets(data); return LM_DeepPointerEx(&p,native_address(a,"address"),off.empty()?nullptr:off.data(),off.size());
}

} // namespace Libmem::NET
