#include "../LibmemCli.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;

ModuleManager::ModuleManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ModuleManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ModuleManager");
    return session_->Target;
}
List<ModuleInfo^>^ ModuleManager::Enumerate() {
    return Libmem::EnumModules(Target());
}
ModuleInfo^ ModuleManager::Find(String^ name) {
    return Libmem::FindModule(Target(),name);
}
ModuleInfo^ ModuleManager::Load(String^ path) {
    return Libmem::LoadModule(Target(),path);
}
bool ModuleManager::Unload(ModuleInfo^ moduleInfo) {
    return Libmem::UnloadModule(Target(),moduleInfo);
}
