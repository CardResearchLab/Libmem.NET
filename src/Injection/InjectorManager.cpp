#include "../LibmemCli.h"
#include "../Interop/NativeConverter.h"

using namespace System;
using namespace LibmemCli;
using namespace LibmemCli::Interop;

InjectedModuleHandle::InjectedModuleHandle(ProcessInfo^ target,ModuleInfo^ moduleInfo,String^ requestedPath)
    : target_(nullptr),module_(nullptr),requestedPath_(requestedPath),active_(true),disposed_(false) {
    if(target==nullptr) throw gcnew ArgumentNullException("target");
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    target_=process(proc(target));
    module_=module(mod(moduleInfo));
}
ModuleInfo^ InjectedModuleHandle::Module::get() {
    return module_==nullptr ? nullptr : module(mod(module_));
}
String^ InjectedModuleHandle::RequestedPath::get() { return requestedPath_; }
bool InjectedModuleHandle::IsActive::get() { return active_; }
bool InjectedModuleHandle::IsDisposed::get() { return disposed_; }
bool InjectedModuleHandle::Unload() {
    if(!active_) return true;
    if(disposed_) return false;
    if(target_==nullptr) {
        active_=false;
        return true;
    }
    if(!Libmem::IsProcessAlive(target_)) {
        // The process address space is gone, so this loader reference cannot remain active.
        active_=false;
        target_=nullptr;
        return true;
    }
    bool ok=Libmem::UnloadModule(target_,module_);
    if(ok) active_=false;
    return ok;
}
InjectedModuleHandle::~InjectedModuleHandle() {
    if(disposed_) return;
    if(active_ && !Unload())
        throw gcnew LibmemException(
            "LM_UnloadModuleEx",
            "Failed to unload injected module during Dispose; the owned load reference remains active.");
    disposed_=true;
    target_=nullptr;
}
InjectedModuleHandle::!InjectedModuleHandle() {
    // Never call FreeLibrary in another process from the GC finalizer thread.
    target_=nullptr;
    disposed_=true;
}

InjectorManager::InjectorManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ InjectorManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("InjectorManager");
    return session_->Target;
}
InjectedModuleHandle^ InjectorManager::InjectLibrary(String^ path) {
    if(String::IsNullOrWhiteSpace(path)) throw gcnew ArgumentException("Library path must not be empty.", "path");

    auto target=Target();
    if(!Libmem::IsProcessAlive(target)) throw gcnew InvalidOperationException("Target process is no longer alive.");
    if(target->Bits!=Libmem::GetBits())
        throw gcnew NotSupportedException("Cross-bitness library injection is not supported by the current runtime.");

    String^ fullPath;
    try {
        fullPath=System::IO::Path::GetFullPath(path);
    } catch(Exception^ ex) {
        throw gcnew ArgumentException("Library path is invalid.", "path", ex);
    }

    if(!System::IO::File::Exists(fullPath))
        throw gcnew System::IO::FileNotFoundException("Library to inject was not found.", fullPath);

    auto nativeTarget=proc(target);
    auto nativePath=utf8(fullPath);

    // Ask libmem only to perform the LoadLibrary operation. Its module_out lookup is
    // name/suffix based; resolve the resulting module ourselves by normalized full path
    // so same-named DLLs from different directories cannot be confused.
    if(LM_LoadModuleEx(&nativeTarget,nativePath.c_str(),nullptr)==LM_FALSE)
        throw gcnew LibmemException("LM_LoadModuleEx", "Library injection failed.");

    ModuleInfo^ loaded=nullptr;
    for each(ModuleInfo^ candidate in Libmem::EnumModules(target)) {
        if(candidate==nullptr || String::IsNullOrWhiteSpace(candidate->Path)) continue;

        String^ candidatePath;
        try {
            candidatePath=System::IO::Path::GetFullPath(candidate->Path);
        } catch(Exception^) {
            continue;
        }

        if(String::Equals(candidatePath,fullPath,StringComparison::OrdinalIgnoreCase)) {
            loaded=candidate;
            break;
        }
    }

    if(loaded==nullptr)
        throw gcnew LibmemException(
            "LM_EnumModulesEx",
            "LoadLibrary completed but the injected module could not be resolved by full path.");

    return gcnew InjectedModuleHandle(target,loaded,fullPath);
}
