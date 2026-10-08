#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <cstdlib>
#include <cstring>
#include <limits>
#include <vector>
#include <vcclr.h>

using namespace System;
namespace Libmem::NET {
using namespace ::Libmem::NET::Interop;

namespace {
    bool final_candidate_address(
        lm_address_t address,
        lm_size_t scanSize,
        lm_size_t patternSize,
        lm_address_t& finalAddress) {
        if(patternSize==0 || scanSize<patternSize || address==LM_ADDRESS_BAD)
            return false;

        const lm_size_t offset=scanSize-patternSize;
        const lm_address_t maximum=std::numeric_limits<lm_address_t>::max();
        if(offset>maximum-address)
            throw gcnew ArgumentOutOfRangeException(
                "scanSize",
                "Scan range exceeds the current process address space.");

        finalAddress=address+offset;
        return true;
    }

    bool read_candidate(
        const lm_process_t* processInfo,
        lm_address_t address,
        lm_size_t size,
        std::vector<lm_byte_t>& bytes) {
        bytes.assign(size, 0);
        if(size==0) return false;

        if(processInfo)
            return LM_ReadMemoryEx(processInfo,address,bytes.data(),size)==size;

        lm_process_t current{};
        if(!LM_GetProcess(&current))
            return false;
        return LM_ReadMemoryEx(&current,address,bytes.data(),size)==size;
    }

    bool final_data_match(
        const lm_process_t* processInfo,
        lm_address_t finalAddress,
        const lm_byte_t* pattern,
        lm_size_t patternSize) {
        std::vector<lm_byte_t> bytes;
        if(!read_candidate(processInfo,finalAddress,patternSize,bytes))
            return false;
        return std::memcmp(bytes.data(),pattern,patternSize)==0;
    }

    bool final_pattern_match(
        const lm_process_t* processInfo,
        lm_address_t finalAddress,
        const lm_byte_t* pattern,
        const std::string& mask,
        lm_size_t patternSize) {
        std::vector<lm_byte_t> bytes;
        if(!read_candidate(processInfo,finalAddress,patternSize,bytes))
            return false;

        for(lm_size_t i=0;i<patternSize;++i) {
            if(mask[i]!='?' && bytes[i]!=pattern[i])
                return false;
        }
        return true;
    }

    lm_size_t signature_byte_count(const std::string& signature) {
        const char* ptr=signature.c_str();
        size_t byteCount=0;

        while(ptr && *ptr) {
            char* parsedEnd=nullptr;
            const long value=std::strtol(ptr,&parsedEnd,16);
            const char* endPtr=parsedEnd;
            if(value==0 && ptr==parsedEnd)
                endPtr=std::strchr(ptr+1,' ');

            ++byteCount;
            ptr=endPtr;
        }

        return native_size(static_cast<UInt64>(byteCount),"signature");
    }

    bool final_signature_match(
        const lm_process_t* processInfo,
        lm_address_t finalAddress,
        const std::string& signature,
        lm_size_t patternSize) {
        std::vector<lm_byte_t> bytes;
        if(!read_candidate(processInfo,finalAddress,patternSize,bytes))
            return false;

        // The pinned scanner omits its final candidate because its loop uses
        // ptr != end instead of including end. A one-byte padded local buffer
        // makes the pinned parser/scanner evaluate exactly this one candidate
        // without changing signature parsing semantics.
        bytes.push_back(0);
        const auto localAddress=reinterpret_cast<lm_address_t>(bytes.data());
        const auto localScanSize=native_size(
            static_cast<UInt64>(patternSize)+1,
            "signature");
        return LM_SigScan(signature.c_str(),localAddress,localScanSize)!=LM_ADDRESS_BAD;
    }
}

UInt64 Libmem::DataScan(array<Byte>^ data,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("data");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");

    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=static_cast<lm_size_t>(data->Length);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    pin_ptr<Byte> raw=&data[0];
    const auto result=LM_DataScan(raw,patternSize,address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_data_match(nullptr,finalAddress,raw,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

UInt64 Libmem::DataScan(ProcessInfo^ input,array<Byte>^ data,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(data==nullptr) throw gcnew ArgumentNullException("data");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "data");

    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=static_cast<lm_size_t>(data->Length);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    pin_ptr<Byte> raw=&data[0];
    const auto result=LM_DataScanEx(&p,raw,patternSize,address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_data_match(&p,finalAddress,raw,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

UInt64 Libmem::PatternScan(array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(mask->Length==0) throw gcnew ArgumentException("Mask is empty.", "mask");

    auto m=utf8(mask,"mask");
    if(m.size()!=static_cast<size_t>(data->Length))
        throw gcnew ArgumentException("Pattern size must match mask length.");

    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=static_cast<lm_size_t>(data->Length);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    pin_ptr<Byte> raw=&data[0];
    const auto result=LM_PatternScan(raw,m.c_str(),address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_pattern_match(nullptr,finalAddress,raw,m,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

UInt64 Libmem::PatternScan(ProcessInfo^ input,array<Byte>^ data,String^ mask,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(data==nullptr) throw gcnew ArgumentNullException("pattern");
    if(mask==nullptr) throw gcnew ArgumentNullException("mask");
    if(!data->Length) throw gcnew ArgumentException("Pattern is empty.", "pattern");
    if(mask->Length==0) throw gcnew ArgumentException("Mask is empty.", "mask");

    auto m=utf8(mask,"mask");
    if(m.size()!=static_cast<size_t>(data->Length))
        throw gcnew ArgumentException("Pattern size must match mask length.");

    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=static_cast<lm_size_t>(data->Length);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    pin_ptr<Byte> raw=&data[0];
    const auto result=LM_PatternScanEx(&p,raw,m.c_str(),address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_pattern_match(&p,finalAddress,raw,m,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

UInt64 Libmem::SigScan(String^ signature,UInt64 a,UInt64 size) {
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    if(String::IsNullOrWhiteSpace(signature))
        throw gcnew ArgumentException("Signature must not be empty.", "signature");

    auto s=utf8(signature,"signature");
    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=signature_byte_count(s);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    const auto result=LM_SigScan(s.c_str(),address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_signature_match(nullptr,finalAddress,s,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

UInt64 Libmem::SigScan(ProcessInfo^ input,String^ signature,UInt64 a,UInt64 size) {
    auto p=proc(input);
    if(signature==nullptr) throw gcnew ArgumentNullException("signature");
    if(String::IsNullOrWhiteSpace(signature))
        throw gcnew ArgumentException("Signature must not be empty.", "signature");

    auto s=utf8(signature,"signature");
    const auto address=native_address(a,"address");
    const auto scanSize=native_size(size,"scanSize");
    const auto patternSize=signature_byte_count(s);
    lm_address_t finalAddress=LM_ADDRESS_BAD;
    const bool hasFinal=final_candidate_address(address,scanSize,patternSize,finalAddress);

    const auto result=LM_SigScanEx(&p,s.c_str(),address,scanSize);
    if(result!=LM_ADDRESS_BAD || !hasFinal)
        return result;

    return final_signature_match(&p,finalAddress,s,patternSize)
        ? static_cast<UInt64>(finalAddress)
        : static_cast<UInt64>(LM_ADDRESS_BAD);
}

} // namespace Libmem::NET
