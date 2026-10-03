#pragma once

#include "../Libmem.NET.h"

#include <string>
#include <vector>

// Internal-only native/managed interop boundary.
// This header is an implementation detail and must not become part of the public managed API.
namespace Libmem::Net::Interop {
    struct NativeSymbol {
        lm_address_t address;
        std::string name;
    };

    std::string utf8(String^ value);
    std::string utf8(String^ value, String^ parameterName);
    String^ str(const char* text);

    lm_address_t native_address(UInt64 value, String^ parameterName);
    lm_size_t native_size(UInt64 value, String^ parameterName);
    Libmem::Net::Architecture native_architecture(Libmem::Net::Architecture value, String^ parameterName);
    MemoryProtection native_protection(MemoryProtection value, String^ parameterName);
    bool bad_address(UInt64 value);

    lm_process_t proc(ProcessInfo^ input);
    lm_module_t mod(ModuleInfo^ input);

    ProcessInfo^ process(const lm_process_t& value);
    ThreadInfo^ thread(const lm_thread_t& value);
    ModuleInfo^ module(const lm_module_t& value, ProcessInfo^ owner);
    SegmentInfo^ segment(const lm_segment_t& value);
    InstructionInfo^ instruction(const lm_inst_t& value);

    std::vector<lm_address_t> offsets(array<UInt64>^ input);

    lm_bool_t LM_CALL cb_process(lm_process_t* value, void* context);
    lm_bool_t LM_CALL cb_thread(lm_thread_t* value, void* context);
    lm_bool_t LM_CALL cb_module(lm_module_t* value, void* context);
    lm_bool_t LM_CALL cb_segment(lm_segment_t* value, void* context);
    lm_bool_t LM_CALL cb_symbol(lm_symbol_t* value, void* context);
}
