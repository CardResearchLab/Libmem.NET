#include "../Libmem.NET.h"

using namespace System;
namespace Libmem::NET {

ProcessInfo::ProcessInfo(
    UInt32 pid,
    UInt32 parentPid,
    ::Libmem::NET::Architecture architecture,
    UInt64 bits,
    UInt64 startTime,
    String^ name,
    String^ path)
    : pid_(pid),
      parentPid_(parentPid),
      architecture_(architecture),
      bits_(bits),
      startTime_(startTime),
      name_(name),
      path_(path) {}

UInt32 ProcessInfo::Pid::get() { return pid_; }
UInt32 ProcessInfo::ParentPid::get() { return parentPid_; }
::Libmem::NET::Architecture ProcessInfo::Architecture::get() { return architecture_; }
UInt64 ProcessInfo::Bits::get() { return bits_; }
UInt64 ProcessInfo::StartTime::get() { return startTime_; }
String^ ProcessInfo::Name::get() { return name_; }
String^ ProcessInfo::Path::get() { return path_; }

bool ProcessInfo::IsAlive() { return Libmem::IsProcessAlive(this); }

} // namespace Libmem::NET
