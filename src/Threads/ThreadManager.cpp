#include "../LibmemCli.h"
using namespace System;
using namespace System::Collections::Generic;
using namespace LibmemCli;

ThreadManager::ThreadManager(ProcessSession^ session) : session_(session) {
    if(session==nullptr) throw gcnew ArgumentNullException("session");
}
ProcessInfo^ ThreadManager::Target() {
    if(session_==nullptr) throw gcnew ObjectDisposedException("ThreadManager");
    return session_->Target;
}
List<ThreadInfo^>^ ThreadManager::Enumerate() {
    return Libmem::EnumThreads(Target());
}
ThreadInfo^ ThreadManager::Main::get() {
    return Libmem::GetThread(Target());
}
