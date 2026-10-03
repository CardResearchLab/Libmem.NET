#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

using namespace System;
using namespace Libmem::Net;
using namespace Libmem::Net::Interop;

HookManager::HookManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ HookManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("HookManager");
    return session_->Target;
}
HookHandle^ HookManager::Install(UInt64 source,UInt64 destination) {
    auto handle=Libmem::HookCode(Target(),source,destination);
    if(handle==nullptr)
        throw gcnew LibmemException("LM_HookCodeEx", "Failed to install hook in the target process.");
    return handle;
}

HookHandle::HookHandle(ProcessInfo^ target,UInt64 from,UInt64 destination,UInt64 trampoline,UInt64 size)
    : target_(nullptr),from_(from),destination_(destination),trampoline_(trampoline),size_(size),installed_(true),disposed_(false) {
    if(target!=nullptr) target_=process(proc(target));
}
UInt64 HookHandle::Source::get() { return from_; }
UInt64 HookHandle::Destination::get() { return destination_; }
UInt64 HookHandle::Trampoline::get() { return trampoline_; }
UInt64 HookHandle::PatchedBytes::get() { return size_; }
bool HookHandle::IsInstalled::get() { return installed_; }
bool HookHandle::IsDisposed::get() { return disposed_; }
bool HookHandle::Remove() {
    if(!installed_) return true;
    if(disposed_) return false;

    bool ok;
    if(target_!=nullptr) {
        if(!Libmem::IsProcessAlive(target_)) {
            // The target address space no longer exists, so the hook cannot remain installed.
            installed_=false;
            return true;
        }
        auto p=proc(target_);
        ok=LM_UnhookCodeEx(&p,native_address(from_,"source"),native_address(trampoline_,"trampoline"),native_size(size_,"size"))!=LM_FALSE;
    } else {
        ok=LM_UnhookCode(native_address(from_,"source"),native_address(trampoline_,"trampoline"),native_size(size_,"size"))!=LM_FALSE;
    }

    if(ok) installed_=false;
    return ok;
}
HookHandle::~HookHandle() {
    if(disposed_) return;
    if(installed_ && !Remove())
        throw gcnew LibmemException(
            target_!=nullptr ? "LM_UnhookCodeEx" : "LM_UnhookCode",
            "Failed to remove hook during Dispose; the hook remains installed.");
    disposed_=true;
    target_=nullptr;
}
HookHandle::!HookHandle() {
    // Never patch process code from the GC finalizer thread.
    // If explicit disposal was skipped, IsInstalled may have remained true until finalization.
    target_=nullptr;
    disposed_=true;
}

HookHandle^ Libmem::HookCode(UInt64 from,UInt64 to) {
    lm_address_t trampoline=LM_ADDRESS_BAD;
    auto n=LM_HookCode(native_address(from,"source"),native_address(to,"destination"),&trampoline);
    return n ? gcnew HookHandle(nullptr,from,to,trampoline,n) : nullptr;
}
HookHandle^ Libmem::HookCode(ProcessInfo^ input,UInt64 from,UInt64 to) {
    auto p=proc(input); lm_address_t trampoline=LM_ADDRESS_BAD;
    auto n=LM_HookCodeEx(&p,native_address(from,"source"),native_address(to,"destination"),&trampoline);
    return n ? gcnew HookHandle(input,from,to,trampoline,n) : nullptr;
}
