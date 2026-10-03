#include "../Libmem.NET.h"

using namespace System;
namespace Libmem::NET {

InstructionInfo::InstructionInfo(
    UInt64 address,
    UInt64 size,
    array<Byte>^ bytes,
    String^ mnemonic,
    String^ operandString)
    : address_(address),
      size_(size),
      bytes_(bytes == nullptr ? gcnew array<Byte>(0) : safe_cast<array<Byte>^>(bytes->Clone())),
      mnemonic_(mnemonic),
      operandString_(operandString) {}

UInt64 InstructionInfo::Address::get() { return address_; }
UInt64 InstructionInfo::Size::get() { return size_; }
array<Byte>^ InstructionInfo::Bytes::get() {
    return safe_cast<array<Byte>^>(bytes_->Clone());
}
String^ InstructionInfo::Mnemonic::get() { return mnemonic_; }
String^ InstructionInfo::OperandString::get() { return operandString_; }

} // namespace Libmem::NET
