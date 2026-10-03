#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vector>

using namespace System;
using namespace System::Collections::Generic;
using namespace Libmem::Net;
using namespace Libmem::Net::Interop;

List<SegmentInfo^>^ Libmem::EnumSegments() {
    std::vector<lm_segment_t> native;
    if(!LM_EnumSegments(cb_segment,&native)) throw gcnew LibmemException("LM_EnumSegments", "LM_EnumSegments failed.");
    auto r=gcnew List<SegmentInfo^>(); for(const auto& s : native) r->Add(segment(s)); return r;
}
List<SegmentInfo^>^ Libmem::EnumSegments(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_segment_t> native;
    if(!LM_EnumSegmentsEx(&p,cb_segment,&native)) throw gcnew LibmemException("LM_EnumSegmentsEx", "LM_EnumSegmentsEx failed.");
    auto r=gcnew List<SegmentInfo^>(); for(const auto& s : native) r->Add(segment(s)); return r;
}
SegmentInfo^ Libmem::FindSegment(UInt64 a) { lm_segment_t s{}; return LM_FindSegment(native_address(a,"address"),&s) ? segment(s) : nullptr; }
SegmentInfo^ Libmem::FindSegment(ProcessInfo^ input,UInt64 a) { auto p=proc(input); lm_segment_t s{}; return LM_FindSegmentEx(&p,native_address(a,"address"),&s) ? segment(s) : nullptr; }
