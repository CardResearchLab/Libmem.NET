#include "../LibmemCli.h"

using namespace System;
using namespace LibmemCli;

ModuleInfo::ModuleInfo(
    UInt64 baseAddress,
    UInt64 endAddress,
    UInt64 size,
    String^ name,
    String^ path)
    : base_(baseAddress),
      end_(endAddress),
      size_(size),
      name_(name),
      path_(path) {}

UInt64 ModuleInfo::Base::get() { return base_; }
UInt64 ModuleInfo::End::get() { return end_; }
UInt64 ModuleInfo::Size::get() { return size_; }
String^ ModuleInfo::Name::get() { return name_; }
String^ ModuleInfo::Path::get() { return path_; }
