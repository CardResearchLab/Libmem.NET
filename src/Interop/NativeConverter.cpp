#include "NativeConverter.h"

#include <algorithm>
#include <cstring>
#include <limits>
#include <vcclr.h>

using namespace System;
using namespace System::Text;
using namespace System::Runtime::InteropServices;

namespace LibmemCli::Interop {
    std::string utf8(String^ value, String^ parameterName) {
        if(value == nullptr) throw gcnew ArgumentNullException(parameterName);
        if(value->IndexOf('\0') >= 0)
            throw gcnew ArgumentException("Embedded NUL is not supported.", parameterName);

        array<Byte>^ bytes = Encoding::UTF8->GetBytes(value);
        if(bytes->Length == 0) return std::string();

        pin_ptr<Byte> pinned = &bytes[0];
        return std::string(reinterpret_cast<const char*>(pinned), bytes->Length);
    }

    std::string utf8(String^ value) {
        return utf8(value, "value");
    }

    String^ str(const char* text) {
        if(!text) return nullptr;

        int length = static_cast<int>(std::strlen(text));
        auto bytes = gcnew array<Byte>(length);
        if(length) Marshal::Copy(IntPtr((void*)text), bytes, 0, length);
        return Encoding::UTF8->GetString(bytes);
    }

    lm_address_t native_address(UInt64 value, String^ parameterName) {
        const UInt64 maximum = static_cast<UInt64>(std::numeric_limits<lm_address_t>::max());
        if(value > maximum) {
            throw gcnew ArgumentOutOfRangeException(
                parameterName,
                "Address does not fit the current process architecture.");
        }
        return static_cast<lm_address_t>(value);
    }

    lm_size_t native_size(UInt64 value, String^ parameterName) {
        const UInt64 maximum = static_cast<UInt64>(std::numeric_limits<lm_size_t>::max());
        if(value > maximum) {
            throw gcnew ArgumentOutOfRangeException(
                parameterName,
                "Size or index does not fit the current process architecture.");
        }
        return static_cast<lm_size_t>(value);
    }

    LibmemCli::Architecture native_architecture(LibmemCli::Architecture value, String^ parameterName) {
        auto raw = static_cast<unsigned int>(value);
        if(raw > static_cast<unsigned int>(LibmemCli::Architecture::SystemZ))
            throw gcnew ArgumentOutOfRangeException(parameterName, "Unsupported architecture value.");
        return value;
    }

    MemoryProtection native_protection(MemoryProtection value, String^ parameterName) {
        auto raw = static_cast<unsigned int>(value);
        auto validMask = static_cast<unsigned int>(MemoryProtection::ExecuteReadWrite);
        if((raw & ~validMask) != 0)
            throw gcnew ArgumentOutOfRangeException(parameterName, "Unsupported memory protection flags.");
        return value;
    }

    bool bad_address(UInt64 value) {
        return value == static_cast<UInt64>(LM_ADDRESS_BAD);
    }

    lm_process_t proc(ProcessInfo^ input) {
        if(input == nullptr) throw gcnew ArgumentNullException("process");

        lm_process_t result{};
        result.pid = input->Pid;
        result.ppid = input->ParentPid;
        result.arch = static_cast<lm_arch_t>(input->Architecture);
        result.bits = static_cast<lm_size_t>(input->Bits);
        result.start_time = input->StartTime;

        std::string name = utf8(input->Name == nullptr ? String::Empty : input->Name);
        std::string path = utf8(input->Path == nullptr ? String::Empty : input->Path);
        std::memcpy(result.name, name.data(), std::min(name.size(), sizeof(result.name) - 1));
        std::memcpy(result.path, path.data(), std::min(path.size(), sizeof(result.path) - 1));
        return result;
    }

    lm_module_t mod(ModuleInfo^ input) {
        if(input == nullptr) throw gcnew ArgumentNullException("module");

        lm_module_t result{};
        result.base = native_address(input->Base, "module.Base");
        result.end = native_address(input->End, "module.End");
        result.size = native_size(input->Size, "module.Size");

        std::string name = utf8(input->Name == nullptr ? String::Empty : input->Name);
        std::string path = utf8(input->Path == nullptr ? String::Empty : input->Path);
        std::memcpy(result.name, name.data(), std::min(name.size(), sizeof(result.name) - 1));
        std::memcpy(result.path, path.data(), std::min(path.size(), sizeof(result.path) - 1));
        return result;
    }

    ProcessInfo^ process(const lm_process_t& value) {
        return gcnew ProcessInfo(
            value.pid,
            value.ppid,
            static_cast<LibmemCli::Architecture>(value.arch),
            value.bits,
            value.start_time,
            str(value.name),
            str(value.path));
    }

    ThreadInfo^ thread(const lm_thread_t& value) {
        return gcnew ThreadInfo(value.tid, value.owner_pid);
    }

    ModuleInfo^ module(const lm_module_t& value, ProcessInfo^ owner) {
        if(owner == nullptr) throw gcnew ArgumentNullException("owner");
        return gcnew ModuleInfo(
            value.base,
            value.end,
            value.size,
            str(value.name),
            str(value.path),
            owner->Pid,
            owner->StartTime);
    }

    SegmentInfo^ segment(const lm_segment_t& value) {
        return gcnew SegmentInfo(
            value.base,
            value.end,
            value.size,
            static_cast<MemoryProtection>(value.prot));
    }

    InstructionInfo^ instruction(const lm_inst_t& value) {
        int byteCount = static_cast<int>(std::min(static_cast<size_t>(value.size), sizeof(value.bytes)));
        auto bytes = gcnew array<Byte>(byteCount);
        if(byteCount) Marshal::Copy(IntPtr((void*)value.bytes), bytes, 0, byteCount);

        return gcnew InstructionInfo(
            value.address,
            value.size,
            bytes,
            str(value.mnemonic),
            str(value.op_str));
    }

    std::vector<lm_address_t> offsets(array<UInt64>^ input) {
        if(input == nullptr) throw gcnew ArgumentNullException("offsets");

        std::vector<lm_address_t> result;
        result.reserve(input->Length);
        for each(UInt64 item in input) {
            result.push_back(native_address(item, "offsets"));
        }
        return result;
    }

#pragma managed(push, off)
    lm_bool_t LM_CALL cb_process(lm_process_t* value, void* context) {
        static_cast<std::vector<lm_process_t>*>(context)->push_back(*value);
        return LM_TRUE;
    }

    lm_bool_t LM_CALL cb_thread(lm_thread_t* value, void* context) {
        static_cast<std::vector<lm_thread_t>*>(context)->push_back(*value);
        return LM_TRUE;
    }

    lm_bool_t LM_CALL cb_module(lm_module_t* value, void* context) {
        static_cast<std::vector<lm_module_t>*>(context)->push_back(*value);
        return LM_TRUE;
    }

    lm_bool_t LM_CALL cb_segment(lm_segment_t* value, void* context) {
        static_cast<std::vector<lm_segment_t>*>(context)->push_back(*value);
        return LM_TRUE;
    }

    lm_bool_t LM_CALL cb_symbol(lm_symbol_t* value, void* context) {
        static_cast<std::vector<NativeSymbol>*>(context)->push_back(
            { value->address, value->name ? value->name : "" });
        return LM_TRUE;
    }
#pragma managed(pop)
}
