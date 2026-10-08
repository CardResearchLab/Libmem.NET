#!/usr/bin/env python3
"""Audit pinned native libmem function references in the C++/CLI wrapper.

This is *not* a promise of a one-to-one public managed API. Cleanup helpers
and native local/remote overloads are often intentionally surfaced differently.
"""

from __future__ import annotations

from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]
HEADER = ROOT / "third_party/libmem/include/libmem/libmem.h"
SOURCE = ROOT / "src"

# Native command-line enumeration is unsafe in the pinned Windows upstream:
# implementation only handles self, mutates the supplied PID and may pass an
# uninitialized pointer to realloc. The public Libmem.GetCommandLine API
# handles current-process args through Environment.GetCommandLineArgs and
# returns null for other process identities. Its paired free helper is unused.
INTENTIONALLY_NOT_CALLED = {
    "LM_GetCommandLine": "Managed self-process command-line compatibility workaround",
    "LM_FreeCommandLine": "Ownership helper for the intentionally bypassed native call",
}


def main() -> int:
    if not HEADER.is_file():
        print(f"ERROR missing pinned native header: {HEADER}", file=sys.stderr)
        return 2

    upstream = HEADER.read_text(encoding="utf-8")
    native_api = set(re.findall(r"(?m)^\s*(LM_[A-Za-z0-9_]+)\s*\(", upstream))
    cpp_files = sorted(SOURCE.rglob("*.cpp"))
    if not cpp_files or not native_api:
        print("ERROR native API or wrapper sources unavailable", file=sys.stderr)
        return 2

    wrapper = "\n".join(path.read_text(encoding="utf-8") for path in cpp_files)
    call_sites = set(re.findall(r"\b(LM_[A-Za-z0-9_]+)\s*\(", wrapper))
    used_native = native_api & call_sites
    intentionally_unused = native_api & INTENTIONALLY_NOT_CALLED.keys() - used_native
    missing = native_api - used_native - intentionally_unused

    print(f"NATIVE API COVERAGE: {len(native_api)} pinned functions; "
          f"{len(used_native)} called in C++/CLI; "
          f"{len(intentionally_unused)} intentional alternatives; "
          f"{len(missing)} unreviewed")
    for name in sorted(intentionally_unused):
        print(f"  INTENTIONAL {name}: {INTENTIONALLY_NOT_CALLED[name]}")
    for name in sorted(missing):
        print(f"  UNREVIEWED {name}")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
