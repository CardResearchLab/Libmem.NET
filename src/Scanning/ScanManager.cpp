#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {

ScanManager::ScanManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ScanManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ScanManager");
    return session_->Target;
}
UInt64 ScanManager::DeepPointer(UInt64 baseAddress,array<UInt64>^ pointerOffsets) {
    return Libmem::DeepPointer(Target(),baseAddress,pointerOffsets);
}
UInt64 ScanManager::DataScan(array<Byte>^ data,UInt64 address,UInt64 scanSize) {
    return Libmem::DataScan(Target(),data,address,scanSize);
}
UInt64 ScanManager::PatternScan(array<Byte>^ pattern,String^ mask,UInt64 address,UInt64 scanSize) {
    return Libmem::PatternScan(Target(),pattern,mask,address,scanSize);
}
UInt64 ScanManager::SigScan(String^ signature,UInt64 address,UInt64 scanSize) {
    return Libmem::SigScan(Target(),signature,address,scanSize);
}

} // namespace Libmem::NET
