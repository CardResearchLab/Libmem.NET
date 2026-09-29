#include "../LibmemCli.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;

SymbolManager::SymbolManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ SymbolManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("SymbolManager");
    return session_->Target;
}
List<SymbolInfo^>^ SymbolManager::Enumerate(ModuleInfo^ moduleInfo,bool demangle) {
    Target();
    return Libmem::EnumSymbols(moduleInfo,demangle);
}
UInt64 SymbolManager::FindAddress(ModuleInfo^ moduleInfo,String^ name,bool demangle) {
    Target();
    return Libmem::FindSymbolAddress(moduleInfo,name,demangle);
}
String^ SymbolManager::Demangle(String^ name) {
    Target();
    return Libmem::DemangleSymbol(name);
}
