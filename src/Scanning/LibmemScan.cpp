#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vcclr.h>

using namespace System;
using namespace ::Libmem::Net;
namespace LibmemNet = ::Libmem::Net;
using namespace ::Libmem::Net::Interop;

UInt64 LibmemNet::Libmem::DataScan(array<Byte>^ data,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScan(raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 LibmemNet::Libmem::DataScan(ProcessInfo^ input,array<Byte>^ data,UInt64 a,UInt64 size) {
    auto p=proc(input); if(data==nullptr) throw gcnew ArgumentNullException("data"); if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");
    pin_ptr<Byte> raw=&data[0]; return LM_DataScanEx(&p,raw,data->Length,native_address(a,"address"),native_size(size,"size"));
}
UInt64 LibmemNet::Libmem::PatternScan(array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(mask->Length==0) throw gcnew ArgumentException("Mask is empty.", "mask");
    auto m=utf8(mask,"mask");
    if(m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScan(raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 LibmemNet::Libmem::PatternScan(ProcessInfo^ input,array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(mask->Length==0) throw gcnew ArgumentException("Mask is empty.", "mask");
    auto m=utf8(mask,"mask");
    if(m.size()!=static_cast<size_t>(data->Length)) throw gcnew ArgumentException("Pattern size must match mask length.");
    pin_ptr<Byte> raw=&data[0]; return LM_PatternScanEx(&p,raw,m.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 LibmemNet::Libmem::SigScan(String^ signature,UInt64 a,UInt64 size) {
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    if(String::IsNullOrWhiteSpace(signature)) throw gcnew ArgumentException("Signature must not be empty.", "signature");
    auto s=utf8(signature,"signature"); return LM_SigScan(s.c_str(),native_address(a,"address"),native_size(size,"size"));
}
UInt64 LibmemNet::Libmem::SigScan(ProcessInfo^ input,String^ signature,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    if(String::IsNullOrWhiteSpace(signature)) throw gcnew ArgumentException("Signature must not be empty.", "signature");
    auto s=utf8(signature,"signature"); return LM_SigScanEx(&p,s.c_str(),native_address(a,"address"),native_size(size,"size"));
}
