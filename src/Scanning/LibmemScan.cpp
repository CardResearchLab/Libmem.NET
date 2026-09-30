#include "../LibmemCli.h"
#include "../Interop/NativeConverter.h"

#include <vcclr.h>

using namespace System;
using namespace LibmemCli;
using namespace LibmemCli::Interop;

UInt64 Libmem::DataScan(array<Byte>^ data,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScan(raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::DataScan(ProcessInfo^ input,array<Byte>^ data,UInt64 a,UInt64 size) {
    auto p=proc(input); if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScanEx(&p,raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::PatternScan(array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    auto m=utf8(mask);
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScan(raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::PatternScan(ProcessInfo^ input,array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    auto m=utf8(mask);
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScanEx(&p,raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::SigScan(String^ signature,UInt64 a,UInt64 size) {
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    auto s=utf8(signature); return LM_SigScan(s.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 Libmem::SigScan(ProcessInfo^ input,String^ signature,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    auto s=utf8(signature); return LM_SigScanEx(&p,s.c_str(),native_address(a,"address"),native_size(size,"size"));
}
