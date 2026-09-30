#include "../LibmemCli.h"
#include "../Interop/NativeConverter.h"

#include <vector>

using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;
using namespace LibmemCli::Interop;

List<SymbolInfo^>^ Libmem::EnumSymbols(ModuleInfo^ input,bool demangle) {
    auto m=mod(input); std::vector<NativeSymbol> native;
    bool ok=demangle ? LM_EnumSymbolsDemangled(&m,cb_symbol,&native)!=LM_FALSE : LM_EnumSymbols(&m,cb_symbol,&native)!=LM_FALSE;
    if(!ok) throw gcnew LibmemException(
        demangle ? "LM_EnumSymbolsDemangled" : "LM_EnumSymbols",
        demangle ? "LM_EnumSymbolsDemangled failed." : "LM_EnumSymbols failed.");
    auto r=gcnew List<SymbolInfo^>(); for(const auto& s : native) {
        r->Add(gcnew SymbolInfo(s.address, str(s.name.c_str())));
    } return r;
}
UInt64 Libmem::FindSymbolAddress(ModuleInfo^ input,String^ name,bool demangle) {
    auto m=mod(input);
    if(name==nullptr) throw gcnew ArgumentNullException("name");
    auto s=utf8(name,"name");
    return demangle ? LM_FindSymbolAddressDemangled(&m,s.c_str()) : LM_FindSymbolAddress(&m,s.c_str());
}
String^ Libmem::DemangleSymbol(String^ name) {
    if(name==nullptr) throw gcnew ArgumentNullException("name");
    auto s=utf8(name,"name"); lm_char_t* output=LM_DemangleSymbol(s.c_str(),nullptr,0);
    if(!output) return nullptr;
    try { return str(output); } finally { LM_FreeDemangledSymbol(output); }
}
