#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

using namespace System;
using namespace ::Libmem::Net;
using namespace ::Libmem::Net::Interop;

VmtManager::VmtManager(UInt64 address) : native_(nullptr), disposed_(false) {
    if(address==0)
        throw gcnew ArgumentOutOfRangeException("vtableAddress", "VTable address must not be zero.");

    native_=new lm_vmt_t{};
    if(!LM_VmtNew(reinterpret_cast<lm_address_t*>(static_cast<uintptr_t>(native_address(address,"vtableAddress"))),native_)) {
        delete native_; native_=nullptr; disposed_=true; throw gcnew LibmemException("LM_VmtNew", "LM_VmtNew failed.");
    }
}
bool VmtManager::IsDisposed::get() { return disposed_; }
bool VmtManager::ResetNative() {
    if(!native_) return true;

    // The pinned libmem LM_VmtReset reads entry->index after freeing entry.
    // Remove tracked entries one by one first so Reset/Free only see an empty list.
    while(native_->hkentries!=LM_NULLPTR) {
        auto index=native_->hkentries->index;
        if(LM_VmtUnhook(native_,index)==LM_FALSE) return false;
    }
    return true;
}
void VmtManager::Hook(UInt64 index,UInt64 to) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    if(!LM_VmtHook(native_,native_size(index,"index"),native_address(to,"destination")))
        throw gcnew LibmemException("LM_VmtHook", "LM_VmtHook failed.");
}
bool VmtManager::Unhook(UInt64 index) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    return LM_VmtUnhook(native_,native_size(index,"index"))!=LM_FALSE;
}
UInt64 VmtManager::GetOriginal(UInt64 index) {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    return LM_VmtGetOriginal(native_,native_size(index,"index"));
}
void VmtManager::Reset() {
    if(disposed_ || !native_) throw gcnew ObjectDisposedException("VmtManager");
    if(!ResetNative())
        throw gcnew LibmemException(
            "LM_VmtUnhook",
            "VMT reset failed because one or more tracked hooks could not be removed.");
    // Safe after ResetNative: the upstream list is empty, avoiding its reset use-after-free path.
    LM_VmtReset(native_);
}
VmtManager::~VmtManager() {
    if(disposed_) return;
    if(native_) {
        // Explicit Dispose is deterministic. Do not discard the native bookkeeping if
        // one or more VMT entries could not be restored; callers can catch and retry.
        if(!ResetNative())
            throw gcnew LibmemException(
                "LM_VmtUnhook",
                "Failed to restore one or more VMT hooks during Dispose; the manager remains active.");
        // Safe after ResetNative: the tracked-entry list is empty, avoiding the pinned
        // upstream LM_VmtReset use-after-free path inside LM_VmtFree.
        LM_VmtFree(native_);
        delete native_;
        native_=nullptr;
    }
    disposed_=true;
}
VmtManager::!VmtManager() {
    // Do not rewrite VMT entries from the GC finalizer thread.
    // If Dispose was skipped while hooks were active, libmem's hook-entry bookkeeping may leak.
    if(native_) {
        delete native_;
        native_=nullptr;
    }
    disposed_=true;
}
