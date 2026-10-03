#include "../Libmem.NET.h"

using namespace System;
using namespace ::Libmem::Net::Libmem::Net;

ModuleInfo::ModuleInfo(
    UInt64 baseAddress,
    UInt64 endAddress,
    UInt64 size,
    String^ name,
    String^ path,
    UInt32 ownerPid,
    UInt64 ownerStartTime)
    : base_(baseAddress),
      end_(endAddress),
      size_(size),
      name_(name),
      path_(path),
      ownerPid_(ownerPid),
      ownerStartTime_(ownerStartTime) {}

bool ModuleInfo::BelongsTo(ProcessInfo^ process) {
    if(process == nullptr) return false;
    return ownerPid_ == process->Pid && ownerStartTime_ == process->StartTime;
}

ModuleInfo^ ModuleInfo::Clone() {
    return gcnew ModuleInfo(base_, end_, size_, name_, path_, ownerPid_, ownerStartTime_);
}

UInt64 ModuleInfo::Base::get() { return base_; }
UInt64 ModuleInfo::End::get() { return end_; }
UInt64 ModuleInfo::Size::get() { return size_; }
String^ ModuleInfo::Name::get() { return name_; }
String^ ModuleInfo::Path::get() { return path_; }
