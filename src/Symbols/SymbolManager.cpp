#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {

SymbolManager::SymbolManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ SymbolManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("SymbolManager");
    return session_->Target;
}
List<SymbolInfo^>^ SymbolManager::Enumerate(ModuleInfo^ moduleInfo,bool demangle) {
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    auto target=Target();
    if(!moduleInfo->BelongsTo(target))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    return Libmem::EnumSymbols(moduleInfo,demangle);
}
UInt64 SymbolManager::FindAddress(ModuleInfo^ moduleInfo,String^ name,bool demangle) {
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    auto target=Target();
    if(!moduleInfo->BelongsTo(target))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    return Libmem::FindSymbolAddress(moduleInfo,name,demangle);
}
bool SymbolManager::TryFindAddress(ModuleInfo^ moduleInfo,String^ name,bool demangle,UInt64% address) {
    auto value=FindAddress(moduleInfo,name,demangle);
    auto target=Target();
    if(value==UInt64::MaxValue || (target->Bits==32 && value==static_cast<UInt64>(UInt32::MaxValue))) {
        address=0;
        return false;
    }
    address=value;
    return true;
}
String^ SymbolManager::Demangle(String^ name) {
    Target();
    return Libmem::DemangleSymbol(name);
}

} // namespace Libmem::NET
