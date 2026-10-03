#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace ::Libmem::Net::Libmem::Net;

ModuleManager::ModuleManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ModuleManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ModuleManager");
    return session_->Target;
}
List<ModuleInfo^>^ ModuleManager::Enumerate() {
    return ::Libmem::Net::Libmem::EnumModules(Target());
}
ModuleInfo^ ModuleManager::Find(String^ name) {
    return ::Libmem::Net::Libmem::FindModule(Target(),name);
}
ModuleInfo^ ModuleManager::Load(String^ path) {
    auto loaded=::Libmem::Net::Libmem::LoadModule(Target(),path);
    if(loaded==nullptr)
        throw gcnew LibmemException("LM_LoadModuleEx", "Failed to load module into the target process.");
    return loaded;
}
bool ModuleManager::Unload(ModuleInfo^ moduleInfo) {
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    auto target=Target();
    if(!moduleInfo->BelongsTo(target))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    return ::Libmem::Net::Libmem::UnloadModule(target,moduleInfo);
}
