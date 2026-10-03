#include "../Libmem.NET.h"

using namespace Libmem::Net;

SegmentInfo::SegmentInfo(
    UInt64 baseAddress,
    UInt64 endAddress,
    UInt64 size,
    MemoryProtection protection)
    : base_(baseAddress),
      end_(endAddress),
      size_(size),
      protection_(protection) {}

UInt64 SegmentInfo::Base::get() { return base_; }
UInt64 SegmentInfo::End::get() { return end_; }
UInt64 SegmentInfo::Size::get() { return size_; }
MemoryProtection SegmentInfo::Protection::get() { return protection_; }
