#include "../Libmem.NET.h"
using namespace System;
using namespace System::Collections::Generic;
namespace Libmem::NET {

namespace {
    bool scan_missed(UInt64 value, ProcessInfo^ target) {
        return value==UInt64::MaxValue
            || (target->Bits==32 && value==static_cast<UInt64>(UInt32::MaxValue));
    }
}

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
bool ScanManager::TryDataScan(array<Byte>^ data,UInt64 address,UInt64 scanSize,UInt64% foundAddress) {
    auto target=Target();
    auto value=Libmem::DataScan(target,data,address,scanSize);
    if(scan_missed(value,target)) {
        foundAddress=0;
        return false;
    }
    foundAddress=value;
    return true;
}
UInt64 ScanManager::PatternScan(array<Byte>^ pattern,String^ mask,UInt64 address,UInt64 scanSize) {
    return Libmem::PatternScan(Target(),pattern,mask,address,scanSize);
}
bool ScanManager::TryPatternScan(array<Byte>^ pattern,String^ mask,UInt64 address,UInt64 scanSize,UInt64% foundAddress) {
    auto target=Target();
    auto value=Libmem::PatternScan(target,pattern,mask,address,scanSize);
    if(scan_missed(value,target)) {
        foundAddress=0;
        return false;
    }
    foundAddress=value;
    return true;
}
UInt64 ScanManager::SigScan(String^ signature,UInt64 address,UInt64 scanSize) {
    return Libmem::SigScan(Target(),signature,address,scanSize);
}

bool ScanManager::TrySigScan(String^ signature,UInt64 address,UInt64 scanSize,UInt64% foundAddress) {
    auto target=Target();
    auto value=Libmem::SigScan(target,signature,address,scanSize);
    if(scan_missed(value,target)) {
        foundAddress=0;
        return false;
    }
    foundAddress=value;
    return true;
}

} // namespace Libmem::NET
