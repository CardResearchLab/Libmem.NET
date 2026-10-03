#include "../Libmem.NET.h"

using namespace System;
namespace Libmem::NET {

ThreadInfo::ThreadInfo(UInt32 id, UInt32 ownerPid)
    : id_(id),
      ownerPid_(ownerPid) {}

UInt32 ThreadInfo::Id::get() { return id_; }
UInt32 ThreadInfo::OwnerPid::get() { return ownerPid_; }

} // namespace Libmem::NET
