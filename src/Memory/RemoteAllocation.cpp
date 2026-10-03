#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

using namespace System;
using namespace Libmem::Net;
using namespace Libmem::Net::Interop;

RemoteAllocation::RemoteAllocation(ProcessInfo^ input,UInt64 address,UInt64 size)
    : target_(nullptr),address_(address),size_(size),disposed_(false) {
    if(input==nullptr) throw gcnew ArgumentNullException("process");
    if(address==0 || bad_address(address)) throw gcnew ArgumentOutOfRangeException("address");
    if(size==0) throw gcnew ArgumentOutOfRangeException("size");
    target_=process(proc(input));
}
UInt64 RemoteAllocation::Address::get() { return address_; }
UInt64 RemoteAllocation::Size::get() { return size_; }
bool RemoteAllocation::IsDisposed::get() { return disposed_; }
bool RemoteAllocation::Free() {
    if(disposed_) return true;
    if(target_==nullptr) {
        disposed_=true;
        return true;
    }
    if(!Libmem::IsProcessAlive(target_)) {
        // The OS already reclaimed this address space when the process exited.
        disposed_=true;
        target_=nullptr;
        return true;
    }
    bool ok=Libmem::FreeMemory(target_,address_,size_);
    if(ok) {
        disposed_=true;
        target_=nullptr;
    }
    return ok;
}
RemoteAllocation::~RemoteAllocation() {
    if(disposed_) return;
    if(!Free())
        throw gcnew LibmemException(
            "LM_FreeMemoryEx",
            "Failed to free remote allocation during Dispose; the allocation remains active.");
}
RemoteAllocation::!RemoteAllocation() {
    // Never modify another process from the GC finalizer thread.
    target_=nullptr;
    disposed_=true;
}
