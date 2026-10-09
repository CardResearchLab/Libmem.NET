#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vector>

using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {
using namespace ::Libmem::NET::Interop;

List<ModuleInfo^>^ Libmem::EnumModules() {
    auto owner=CurrentProcess();
    if(owner==nullptr) throw gcnew LibmemException("LM_GetProcess", "Could not resolve the current process for module provenance.");
    std::vector<lm_module_t> native;
    if(!LM_EnumModules(cb_module,&native)) throw gcnew LibmemException("LM_EnumModules", "LM_EnumModules failed.");
    auto r=gcnew List<ModuleInfo^>(); for(const auto& m : native) r->Add(module(m,owner)); return r;
}
List<ModuleInfo^>^ Libmem::EnumModules(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_module_t> native;
    if(!LM_EnumModulesEx(&p,cb_module,&native)) throw gcnew LibmemException("LM_EnumModulesEx", "LM_EnumModulesEx failed.");
    auto r=gcnew List<ModuleInfo^>(); for(const auto& m : native) r->Add(module(m,input)); return r;
}
ModuleInfo^ Libmem::FindModule(String^ name) {
    if(name==nullptr) throw gcnew ArgumentNullException("name");
    if(String::IsNullOrWhiteSpace(name))
        throw gcnew ArgumentException("Module name must not be empty.", "name");
    lm_module_t m{};
    auto n=utf8(name,"name");
    if(!LM_FindModule(n.c_str(),&m)) return nullptr;
    auto owner=CurrentProcess();
    if(owner==nullptr) throw gcnew LibmemException("LM_GetProcess", "Could not resolve the current process for module provenance.");
    return module(m,owner);
}
ModuleInfo^ Libmem::FindModule(ProcessInfo^ input,String^ name) {
    if(name==nullptr) throw gcnew ArgumentNullException("name");
    if(String::IsNullOrWhiteSpace(name))
        throw gcnew ArgumentException("Module name must not be empty.", "name");
    auto p=proc(input);
    lm_module_t m{};
    auto n=utf8(name,"name");
    return LM_FindModuleEx(&p,n.c_str(),&m) ? module(m,input) : nullptr;
}
ModuleInfo^ Libmem::LoadModule(String^ path) {
    if(path==nullptr) throw gcnew ArgumentNullException("path");
    if(String::IsNullOrWhiteSpace(path))
        throw gcnew ArgumentException("Module path must not be empty.", "path");
    lm_module_t m{};
    auto s=utf8(path,"path");
    if(!LM_LoadModule(s.c_str(),&m)) return nullptr;
    auto owner=CurrentProcess();
    if(owner==nullptr) throw gcnew LibmemException("LM_GetProcess", "Could not resolve the current process for module provenance.");
    return module(m,owner);
}
ModuleInfo^ Libmem::LoadModule(ProcessInfo^ input,String^ path) {
    if(path==nullptr) throw gcnew ArgumentNullException("path");
    if(String::IsNullOrWhiteSpace(path))
        throw gcnew ArgumentException("Module path must not be empty.", "path");
    auto p=proc(input);
    lm_module_t m{};
    auto s=utf8(path,"path");
    // The pinned LM_LoadModuleEx opens the target by PID, not by creation time.
    if(!Libmem::IsProcessAlive(input))
        throw gcnew InvalidOperationException("Target process is no longer alive.");
    return LM_LoadModuleEx(&p,s.c_str(),&m) ? module(m,input) : nullptr;
}
bool Libmem::UnloadModule(ModuleInfo^ input) {
    if(input==nullptr) throw gcnew ArgumentNullException("module");
    auto owner=CurrentProcess();
    if(owner==nullptr) throw gcnew LibmemException("LM_GetProcess", "Could not resolve the current process for module provenance.");
    if(!input->BelongsTo(owner))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    auto m=mod(input);
    return LM_UnloadModule(&m)!=LM_FALSE;
}
bool Libmem::UnloadModule(ProcessInfo^ input,ModuleInfo^ m) {
    auto p=proc(input);
    if(m==nullptr) throw gcnew ArgumentNullException("module");
    if(!m->BelongsTo(input))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    auto native=mod(m);
    // PID-only FreeLibrary in the pinned native API must not run for a stale owner.
    if(!Libmem::IsProcessAlive(input)) return false;
    return LM_UnloadModuleEx(&p,&native)!=LM_FALSE;
}

} // namespace Libmem::NET
