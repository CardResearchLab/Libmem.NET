#include "../LibmemCli.h"

using namespace System;
using namespace LibmemCli;

bool ProcessInfo::IsAlive() { return Libmem::IsProcessAlive(this); }
