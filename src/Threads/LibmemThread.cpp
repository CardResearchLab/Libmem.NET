#include "../Libmem.NET.h"
#include "../Interop/NativeConverter.h"

#include <vector>

using namespace System;
using namespace System::Collections::Generic;
using namespace ::Libmem::Net::Libmem::Net;
using namespace ::Libmem::Net::Interop;

List<ThreadInfo^>^ ::Libmem::Net::Libmem::EnumThreads() {
    std::vector<lm_thread_t> native;
    if(!LM_EnumThreads(cb_thread,&native)) throw gcnew LibmemException("LM_EnumThreads", "LM_EnumThreads failed.");
    auto r=gcnew List<ThreadInfo^>(); for(const auto& t : native) r->Add(thread(t)); return r;
}
List<ThreadInfo^>^ ::Libmem::Net::Libmem::EnumThreads(ProcessInfo^ input) {
    auto p=proc(input); std::vector<lm_thread_t> native;
    if(!LM_EnumThreadsEx(&p,cb_thread,&native)) throw gcnew LibmemException("LM_EnumThreadsEx", "LM_EnumThreadsEx failed.");
    auto r=gcnew List<ThreadInfo^>(); for(const auto& t : native) r->Add(thread(t)); return r;
}
ThreadInfo^ ::Libmem::Net::Libmem::CurrentThread() { lm_thread_t t{}; return LM_GetThread(&t) ? thread(t) : nullptr; }
ThreadInfo^ ::Libmem::Net::Libmem::GetThread(ProcessInfo^ input) { auto p=proc(input); lm_thread_t t{}; return LM_GetThreadEx(&p,&t) ? thread(t) : nullptr; }
ProcessInfo^ ::Libmem::Net::Libmem::GetThreadProcess(ThreadInfo^ input) {
    if(input==nullptr) throw gcnew ArgumentNullException("thread");
    lm_thread_t t{input->Id,input->OwnerPid}; lm_process_t p{};
    return LM_GetThreadProcess(&t,&p) ? process(p) : nullptr;
}
