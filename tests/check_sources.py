"""Source/contract checks. These run on every platform and do not compile mixed-mode C++/CLI."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
header = (root / "src/LibmemCli.h").read_text(encoding="utf-8")
source = (root / "src/LibmemCli.cpp").read_text(encoding="utf-8")

for file in [
    "src/LibmemCli.vcxproj",
    "samples/Example.csproj",
    "tests/LibmemCli.SmokeTests/LibmemCli.SmokeTests.csproj",
    "tests/LibmemCli.HookVmtTests/LibmemCli.HookVmtTests.csproj",
]:
    ET.parse(root / file)
    print("PASS XML", file)

for owner in ["Libmem", "ProcessInfo", "RemoteAllocation", "ProcessSession", "MemoryManager", "ModuleManager", "InjectorManager", "InjectedModuleHandle", "HookManager", "HookHandle", "VmtManager"]:
    body = header.split("public ref class " + owner, 1)[1].split("\n    };", 1)[0]
    declarations = re.findall(r"(?<!::)\b(\w+)\s*\([^;{}]*\)\s*;", body)
    declarations = {name for name in declarations if name not in {"get"}}
    implementations = set(re.findall(r"\b" + owner + r"::(\w+)\s*\(", source))
    missing_implementations = declarations - implementations
    assert not missing_implementations, f"{owner} unimplemented: {sorted(missing_implementations)}"
    print("PASS declarations implemented:", owner, len(declarations))

upstream_header_path = root / "third_party/libmem/include/libmem/libmem.h"
assert upstream_header_path.exists(), (
    "libmem.h was not found. Initialize submodules first: "
    "git submodule update --init --recursive"
)
upstream_header = upstream_header_path.read_text(encoding="utf-8", errors="replace")

without_comments = re.sub(r"/\*.*?\*/", "", upstream_header, flags=re.S)
without_comments = re.sub(r"//.*", "", without_comments)

upstream_apis = set(
    re.findall(
        r"\bLM_API\b(?:(?!;).)*?\b(LM_[A-Za-z0-9_]+)\s*\(",
        without_comments,
        flags=re.S,
    )
)
assert len(upstream_apis) >= 50, (
    f"Only parsed {len(upstream_apis)} public APIs from libmem.h; "
    "the parser likely needs to be updated."
)

wrapper_native_calls = set(re.findall(r"\b(LM_[A-Za-z0-9_]+)\s*\(", source))
missing_native_apis = sorted(upstream_apis - wrapper_native_calls)
assert not missing_native_apis, (
    "Pinned libmem exposes public APIs that LibmemCli does not reference: "
    + ", ".join(missing_native_apis)
)
print("PASS upstream public API coverage:", len(upstream_apis))

assert "HookManager^ ProcessSession::Hooks::get()" in source
assert "HookHandle^ HookManager::Install(UInt64 source,UInt64 destination)" in source
assert "return Libmem::HookCode(Target(),source,destination);" in source
print("PASS HookManager session contract")

assert "InjectorManager^ ProcessSession::Injector::get()" in source
assert "InjectedModuleHandle^ InjectorManager::InjectLibrary(String^ path)" in source
assert "Cross-bitness library injection is not supported" in source
assert "bool InjectedModuleHandle::IsActive::get()" in source
assert "bool InjectedModuleHandle::IsDisposed::get()" in source
inject_finalizer = source.split("InjectedModuleHandle::!InjectedModuleHandle()", 1)[1].split("\n}", 1)[0]
assert "UnloadModule" not in inject_finalizer
print("PASS Injector lifecycle contract")

assert "bool HookHandle::IsInstalled::get()" in source
assert "bool HookHandle::IsDisposed::get()" in source
assert 'throw gcnew InvalidOperationException("Unhook failed; hook remains installed.")' not in source
print("PASS HookHandle lifecycle contract")

assert "bool VmtManager::IsDisposed::get()" in source
assert "bool VmtManager::ResetNative()" in source
assert "while(native_->hkentries!=LM_NULLPTR)" in source
vmt_finalizer = source.split("VmtManager::!VmtManager()", 1)[1].split("\n}", 1)[0]
assert "LM_VmtFree" not in vmt_finalizer
assert "LM_VmtReset" not in vmt_finalizer
print("PASS VmtManager lifecycle contract")

version = (root / "VERSION").read_text(encoding="utf-8").strip()
assert re.fullmatch(r"\d+\.\d+\.\d+", version), (
    f"VERSION must use MAJOR.MINOR.PATCH format: {version!r}"
)

assembly_info = (root / "src/AssemblyInfo.cpp").read_text(encoding="utf-8")
assembly_version = version + ".0"
assert f'AssemblyVersionAttribute("{assembly_version}")' in assembly_info
assert f'AssemblyFileVersionAttribute("{assembly_version}")' in assembly_info
assert f'AssemblyInformationalVersionAttribute("{version}")' in assembly_info
print("PASS version metadata:", version)

print("SOURCE CONTRACT CHECKS PASS (NOT A WINDOWS RUNTIME TEST)")
