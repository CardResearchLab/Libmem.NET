#include "../Libmem.NET.h"

using namespace System;
using namespace Libmem::Net;

SymbolInfo::SymbolInfo(UInt64 address, String^ name)
    : address_(address),
      name_(name) {}

UInt64 SymbolInfo::Address::get() { return address_; }
String^ SymbolInfo::Name::get() { return name_; }
