#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace ::Libmem::Net::Libmem::Net;

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
    return ::Libmem::Net::Libmem::EnumSymbols(moduleInfo,demangle);
}
UInt64 SymbolManager::FindAddress(ModuleInfo^ moduleInfo,String^ name,bool demangle) {
    if(moduleInfo==nullptr) throw gcnew ArgumentNullException("module");
    auto target=Target();
    if(!moduleInfo->BelongsTo(target))
        throw gcnew ArgumentException("ModuleInfo belongs to a different process identity.", "module");
    return ::Libmem::Net::Libmem::FindSymbolAddress(moduleInfo,name,demangle);
}
String^ SymbolManager::Demangle(String^ name) {
    Target();
    return ::Libmem::Net::Libmem::DemangleSymbol(name);
}
