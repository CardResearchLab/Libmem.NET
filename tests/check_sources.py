"""Source/contract checks. These run on every platform and do not compile mixed-mode C++/CLI."""
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
header = (root / "src/LibmemCli.h").read_text(encoding="utf-8")
source = (root / "src/LibmemCli.cpp").read_text(encoding="utf-8")

for file in [
    "src/LibmemCli.vcxproj",
    "samples/Example.csproj",
    "tests/LibmemCli.SmokeTests/LibmemCli.SmokeTests.csproj",
    "tests/LibmemCli.HookVmtTests/LibmemCli.HookVmtTests.csproj",
    "tests/LibmemCli.InjectorTests/LibmemCli.InjectorTests.csproj",
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

assert "public ref class LibmemException : InvalidOperationException" in header
assert 'LibmemException("LM_EnumProcesses"' in source
assert 'LibmemException("LM_EnumThreadsEx"' in source
assert 'LibmemException("LM_EnumModulesEx"' in source
assert 'LibmemException("LM_ProtMemoryEx"' in source
assert 'LibmemException("LM_FreeMemoryEx"' in source
assert 'LibmemException("LM_UnloadModuleEx"' in source
assert 'LibmemException("LM_LoadModuleEx"' in source
print("PASS LibmemException core error mapping")

assert "HookManager^ ProcessSession::Hooks::get()" in source
assert "HookHandle^ HookManager::Install(UInt64 source,UInt64 destination)" in source
assert 'LibmemException("LM_HookCodeEx"' in source
print("PASS HookManager session contract")

remote_dispose = source.split("RemoteAllocation::~RemoteAllocation()", 1)[1].split("\n}", 1)[0]
assert "if(!Free())" in remote_dispose
assert "allocation remains active" in remote_dispose
remote_finalizer = source.split("RemoteAllocation::!RemoteAllocation()", 1)[1].split("\n}", 1)[0]
assert "FreeMemory" not in remote_finalizer
print("PASS RemoteAllocation lifecycle contract")

assert "InjectorManager^ ProcessSession::Injector::get()" in source
assert "InjectedModuleHandle^ InjectorManager::InjectLibrary(String^ path)" in source
assert "Cross-bitness library injection is not supported" in source
assert "bool InjectedModuleHandle::IsActive::get()" in source
assert "bool InjectedModuleHandle::IsDisposed::get()" in source
inject_dispose = source.split("InjectedModuleHandle::~InjectedModuleHandle()", 1)[1].split("\n}", 1)[0]
assert "active_ && !Unload()" in inject_dispose
assert "owned load reference remains active" in inject_dispose
inject_finalizer = source.split("InjectedModuleHandle::!InjectedModuleHandle()", 1)[1].split("\n}", 1)[0]
assert "UnloadModule" not in inject_finalizer
print("PASS Injector lifecycle contract")

assert "UInt64 HookHandle::Destination::get()" in source
assert "bool HookHandle::IsInstalled::get()" in source
assert "bool HookHandle::IsDisposed::get()" in source
hook_dispose = source.split("HookHandle::~HookHandle()", 1)[1].split("\n}", 1)[0]
assert "installed_ && !Remove()" in hook_dispose
assert "hook remains installed" in hook_dispose
hook_finalizer = source.split("HookHandle::!HookHandle()", 1)[1].split("\n}", 1)[0]
assert "LM_UnhookCode" not in hook_finalizer
print("PASS HookHandle lifecycle contract")

assert "bool VmtManager::IsDisposed::get()" in source
assert "bool VmtManager::ResetNative()" in source
assert "while(native_->hkentries!=LM_NULLPTR)" in source
vmt_dispose = source.split("VmtManager::~VmtManager()", 1)[1].split("\n}", 1)[0]
assert "if(!ResetNative())" in vmt_dispose
assert "manager remains active" in vmt_dispose
assert 'LibmemException("LM_VmtNew"' in source
assert 'LibmemException("LM_VmtHook"' in source
vmt_finalizer = source.split("VmtManager::!VmtManager()", 1)[1].split("\n}", 1)[0]
assert "LM_VmtFree" not in vmt_finalizer
assert "LM_VmtReset" not in vmt_finalizer
print("PASS VmtManager lifecycle contract")

solution = (root / "LibmemCli.sln").read_text(encoding="utf-8")
vcxproj = (root / "src/LibmemCli.vcxproj").read_text(encoding="utf-8")
build_script = (root / "build.ps1").read_text(encoding="utf-8")
native_build_script = (root / "eng/build-native.ps1").read_text(encoding="utf-8")
smoke_project = (root / "tests/LibmemCli.SmokeTests/LibmemCli.SmokeTests.csproj").read_text(encoding="utf-8")
hook_project = (root / "tests/LibmemCli.HookVmtTests/LibmemCli.HookVmtTests.csproj").read_text(encoding="utf-8")
injector_project = (root / "tests/LibmemCli.InjectorTests/LibmemCli.InjectorTests.csproj").read_text(encoding="utf-8")

assert "Debug|x86 = Debug|x86" in solution
assert "Release|x86 = Release|x86" in solution
assert "Debug|Win32" in vcxproj and "Release|Win32" in vcxproj
assert "<LibmemPlatformLabel Condition=\"'$(Platform)' == 'Win32'\">x86</LibmemPlatformLabel>" in vcxproj
assert "_WIN64;" not in vcxproj
assert "[ValidateSet('x64', 'x86')]" in build_script
assert "[ValidateSet('x64', 'x86')]" in native_build_script
for project in [smoke_project, hook_project, injector_project]:
    assert "<Platforms>x64;x86</Platforms>" in project
    assert "<PlatformTarget>$(Platform)</PlatformTarget>" in project
assert "lm_address_t native_address(UInt64 value" in source
assert "lm_size_t native_size(UInt64 value" in source
assert "bool bad_address(UInt64 value)" in source
assert "Address does not fit the current process architecture." in source
assert "Size or index does not fit the current process architecture." in source
print("PASS x86/x64 architecture contract")

manifest_script = (root / "eng/write-manifest.ps1").read_text(encoding="utf-8")
package_script = (root / "eng/package-runtime.ps1").read_text(encoding="utf-8")
verify_script_path = root / "eng/verify-package.py"
verify_script = verify_script_path.read_text(encoding="utf-8")
compile(verify_script, str(verify_script_path), "exec")
build_workflow = (root / ".github/workflows/build.yml").read_text(encoding="utf-8")
reusable_workflow = (root / ".github/workflows/reusable-build.yml").read_text(encoding="utf-8")
release_workflow = (root / ".github/workflows/release.yml").read_text(encoding="utf-8")

assert "schemaVersion = 2" in manifest_script
assert "Get-FileHash" in manifest_script
assert "sha256 = " in manifest_script
assert '$archiveChecksum = "$archive.sha256"' in package_script
assert "Get-FileHash -Path $archive -Algorithm SHA256" in package_script
assert "verify-package.py" in build_workflow
assert "verify-package.py" in reusable_workflow
assert "verify-package.py" in release_workflow
assert "--expected-repository-commit" in release_workflow
assert "LibmemCli-windows-x64.zip.sha256" in release_workflow
assert "LibmemCli-windows-x86.zip.sha256" in release_workflow
assert "platform: [x64, x86]" in build_workflow
assert "platform:" in reusable_workflow and "Target platform (x64 or x86)" in reusable_workflow
assert "needs: [build-x64, build-x86]" in release_workflow
print("PASS package integrity and release provenance contract")

subprocess.run(
    [
        sys.executable,
        str(root / "eng/check-public-api.py"),
        "--header",
        str(root / "src/LibmemCli.h"),
        "--baseline",
        str(root / "api/LibmemCli.PublicApi.txt"),
    ],
    cwd=root,
    check=True,
)
print("PASS committed public API baseline")

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
