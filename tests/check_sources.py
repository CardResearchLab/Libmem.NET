"""Source/contract checks. These run on every platform and do not compile mixed-mode C++/CLI."""
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
header = (root / "src/Libmem.NET.h").read_text(encoding="utf-8")
source_files = sorted((root / "src").rglob("*.cpp"))
source = "\n".join(path.read_text(encoding="utf-8") for path in source_files)
assert source_files, "No C++ source files were found under src/"
print("PASS source aggregation:", ", ".join(str(path.relative_to(root)) for path in source_files))

native_converter_header = (root / "src/Interop/NativeConverter.h").read_text(encoding="utf-8")
native_converter_source = (root / "src/Interop/NativeConverter.cpp").read_text(encoding="utf-8")
libmem_facade_source = (root / "src/Libmem.NET.cpp").read_text(encoding="utf-8")
assert "namespace Libmem::NET::Interop" in native_converter_header
assert "lm_process_t proc(ProcessInfo^ input)" in native_converter_header
assert "ProcessInfo^ process(const lm_process_t& value)" in native_converter_header
assert "lm_address_t native_address(UInt64 value" in native_converter_header
assert "std::vector<lm_address_t> offsets(array<UInt64>^ input)" in native_converter_header
assert "lm_process_t proc(ProcessInfo^ input)" not in libmem_facade_source
assert "ProcessInfo^ process(const lm_process_t&" not in libmem_facade_source
assert "lm_address_t native_address(UInt64 value" not in libmem_facade_source
assert "LM_CALL cb_process" in native_converter_source
print("PASS NativeConverter extraction contract")

remote_allocation_source = (root / "src/Memory/RemoteAllocation.cpp").read_text(encoding="utf-8")
injector_source = (root / "src/Injection/InjectorManager.cpp").read_text(encoding="utf-8")
assert "RemoteAllocation::RemoteAllocation" in remote_allocation_source
assert "RemoteAllocation::~RemoteAllocation()" in remote_allocation_source
assert "InjectedModuleHandle::InjectedModuleHandle" in injector_source
assert "InjectedModuleHandle::~InjectedModuleHandle()" in injector_source
assert "InjectorManager::InjectLibrary" in injector_source
assert "RemoteAllocation::RemoteAllocation" not in libmem_facade_source
assert "InjectedModuleHandle::InjectedModuleHandle" not in libmem_facade_source
assert "InjectorManager::InjectLibrary" not in libmem_facade_source
print("PASS resource lifetime extraction contract")

hook_source = (root / "src/Hooks/HookManager.cpp").read_text(encoding="utf-8")
vmt_source = (root / "src/Hooks/VmtManager.cpp").read_text(encoding="utf-8")
assert "HookManager::HookManager" in hook_source
assert "HookHandle::HookHandle" in hook_source
assert "HookHandle^ Libmem::HookCode" in hook_source
assert "VmtManager::VmtManager" in vmt_source
assert "HookManager::HookManager" not in libmem_facade_source
assert "HookHandle::HookHandle" not in libmem_facade_source
assert "VmtManager::VmtManager" not in libmem_facade_source
print("PASS Hook VMT extraction contract")

domain_sources = {
    "process": (root / "src/Core/LibmemProcess.cpp").read_text(encoding="utf-8"),
    "thread": (root / "src/Threads/LibmemThread.cpp").read_text(encoding="utf-8"),
    "module": (root / "src/Modules/LibmemModule.cpp").read_text(encoding="utf-8"),
    "symbol": (root / "src/Symbols/LibmemSymbol.cpp").read_text(encoding="utf-8"),
    "segment": (root / "src/Memory/LibmemSegment.cpp").read_text(encoding="utf-8"),
    "memory": (root / "src/Memory/LibmemMemory.cpp").read_text(encoding="utf-8"),
    "scan": (root / "src/Scanning/LibmemScan.cpp").read_text(encoding="utf-8"),
    "assembly": (root / "src/Assembly/LibmemAssembly.cpp").read_text(encoding="utf-8"),
}
assert "LibmemException::LibmemException" in (root / "src/Core/LibmemException.cpp").read_text(encoding="utf-8")
assert "ProcessInfo::IsAlive" in (root / "src/Core/ProcessInfo.cpp").read_text(encoding="utf-8")
assert "Libmem::EnumProcesses" in domain_sources["process"]
assert "Libmem::EnumThreads" in domain_sources["thread"]
assert "Libmem::EnumModules" in domain_sources["module"]
assert "Libmem::EnumSymbols" in domain_sources["symbol"]
assert "Libmem::EnumSegments" in domain_sources["segment"]
assert "Libmem::ReadMemory" in domain_sources["memory"]
assert "Libmem::DataScan" in domain_sources["scan"]
assert "Libmem::GetArchitecture" in domain_sources["assembly"]
assert "Libmem::" not in libmem_facade_source
assert "ProcessInfo::" not in libmem_facade_source
assert "LibmemException::" not in libmem_facade_source
print("PASS static facade domain split contract")

project_source_text = (root / "src/Libmem.NET.vcxproj").read_text(encoding="utf-8")
for project_source in [
    r"Core\LibmemException.cpp",
    r"Core\ProcessInfo.cpp",
    r"Core\LibmemProcess.cpp",
    r"Threads\LibmemThread.cpp",
    r"Modules\LibmemModule.cpp",
    r"Symbols\LibmemSymbol.cpp",
    r"Memory\LibmemSegment.cpp",
    r"Memory\LibmemMemory.cpp",
    r"Scanning\LibmemScan.cpp",
    r"Assembly\LibmemAssembly.cpp",
]:
    assert f'Include="{project_source}"' in project_source_text, (
        f"Split translation unit is not compiled by Libmem.NET.vcxproj: {project_source}"
    )
print("PASS split translation units included in vcxproj")

for file in [
    "src/Libmem.NET.vcxproj",
    "samples/Example.csproj",
    "tests/Libmem.NET.SmokeTests/Libmem.NET.SmokeTests.csproj",
    "tests/Libmem.NET.HookVmtTests/Libmem.NET.HookVmtTests.csproj",
    "tests/Libmem.NET.InjectorTests/Libmem.NET.InjectorTests.csproj",
    "tests/Libmem.NET.TestTarget/Libmem.NET.TestTarget.csproj",
    "tests/Libmem.NET.ExternalProcessTests/Libmem.NET.ExternalProcessTests.csproj",
    "packaging/Libmem.NET.csproj",
    "tests/Libmem.NET.NuGetConsumer/Libmem.NET.NuGetConsumer.csproj",
]:
    ET.parse(root / file)
    print("PASS XML", file)

for owner in ["Libmem", "ProcessInfo", "RemoteAllocation", "ProcessSession", "MemoryManager", "ScanManager", "SymbolManager", "AssemblyManager", "ModuleManager", "ThreadManager", "InjectorManager", "InjectedModuleHandle", "HookManager", "HookHandle", "VmtManager"]:
    match = re.search(r"\bpublic ref class\s+" + re.escape(owner) + r"\b", header)
    assert match is not None, f"{owner} public class declaration not found"
    body = header[match.end():].split("\n    };", 1)[0]
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

# Explicit compatibility waivers are allowed only when Libmem.NET deliberately replaces
# an unsafe or unusable pinned-upstream implementation while preserving the managed API.
# At the pinned Windows revision LM_GetCommandLine has undefined behavior (uninitialized
# realloc input) and mutates the supplied PID due to an assignment in its PID check.
# Libmem.NET therefore serves current-process arguments from System.Environment and keeps
# the upstream external-process "unsupported" behavior. LM_FreeCommandLine is paired
# exclusively with that unsafe native allocation path, so neither native entry point is
# executed by the managed wrapper.
native_api_waivers = {"LM_GetCommandLine", "LM_FreeCommandLine"}
assert native_api_waivers <= upstream_apis, (
    "Compatibility waiver references APIs not exposed by the pinned libmem: "
    + ", ".join(sorted(native_api_waivers - upstream_apis))
)
missing_native_apis = sorted(upstream_apis - wrapper_native_calls - native_api_waivers)
assert not missing_native_apis, (
    "Pinned libmem exposes public APIs that Libmem.NET does not reference or explicitly waive: "
    + ", ".join(missing_native_apis)
)
print(
    "PASS upstream public API coverage:",
    len(upstream_apis),
    "with compatibility waivers:",
    ", ".join(sorted(native_api_waivers)),
)

assert "public ref class LibmemException : InvalidOperationException" in header
for operation in [
    "LM_EnumProcesses",
    "LM_EnumThreadsEx",
    "LM_EnumModulesEx",
    "LM_ProtMemoryEx",
    "LM_AllocMemoryEx",
    "LM_FreeMemoryEx",
    "LM_UnloadModuleEx",
    "LM_LoadModuleEx",
    "LM_AssembleEx",
    "LM_CodeLengthEx",
]:
    assert re.search(
        r'LibmemException\(\s*"' + re.escape(operation) + r'"',
        source,
    ), f"{operation} is not mapped to LibmemException"
print("PASS LibmemException core error mapping")

memory_manager_source = (root / "src/Memory/MemoryManager.cpp").read_text(encoding="utf-8")
module_manager_source = (root / "src/Modules/ModuleManager.cpp").read_text(encoding="utf-8")
assembly_manager_source = (root / "src/Assembly/AssemblyManager.cpp").read_text(encoding="utf-8")
assert 'LibmemException("LM_AllocMemoryEx"' in memory_manager_source
assert 'LibmemException("LM_LoadModuleEx"' in module_manager_source
assert 'LibmemException("LM_AssembleEx"' in assembly_manager_source
assert 'LibmemException("LM_CodeLengthEx"' in assembly_manager_source
print("PASS strict manager failure mapping contract")

process_compat_source = (root / "src/Core/LibmemProcess.cpp").read_text(encoding="utf-8")
assert "get_process_start_time(GetCurrentProcess())" in process_compat_source
assert "p.start_time=match->start_time" in process_compat_source
print("PASS pinned LM_GetProcessEx start-time compatibility contract")

assert "ThreadManager^ ProcessSession::Threads::get()" in source
assert "ScanManager^ ProcessSession::Scanner::get()" in source
assert "SymbolManager^ ProcessSession::Symbols::get()" in source
assert "AssemblyManager^ ProcessSession::Assembly::get()" in source
assert "List<ThreadInfo^>^ ThreadManager::Enumerate()" in source
assert "UInt64 ScanManager::SigScan(String^ signature,UInt64 address,UInt64 scanSize)" in source
assert "ProcessSession^ ProcessSession::Open(UInt32 pid)" in source
assert "List<SymbolInfo^>^ SymbolManager::Enumerate(ModuleInfo^ moduleInfo,bool demangle)" in source
assert "array<Byte>^ AssemblyManager::Assemble(String^ code,UInt64 runtimeAddress)" in source
assert "List<InstructionInfo^>^ AssemblyManager::Disassemble(UInt64 address,UInt64 maxBytes,UInt64 instructionCount,UInt64 runtimeAddress)" in source
assert "UInt64 AssemblyManager::CodeLength(UInt64 address,UInt64 minimumLength)" in source
print("PASS ProcessSession subsystem aggregation contract")

assert "HookManager^ ProcessSession::Hooks::get()" in source
assert "HookHandle^ HookManager::Install(UInt64 source,UInt64 destination)" in source
assert re.search(r'LibmemException\(\s*"LM_HookCodeEx"', source)
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
assert "std::vector<lm_byte_t> trampolineProbe" in source
assert "LM_ReadMemoryEx(&p,trampoline,trampolineProbe.data(),size)!=size" in source
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
assert re.search(r'LibmemException\(\s*"LM_VmtNew"', source)
assert re.search(r'LibmemException\(\s*"LM_VmtHook"', source)
vmt_finalizer = source.split("VmtManager::!VmtManager()", 1)[1].split("\n}", 1)[0]
assert "LM_VmtFree" not in vmt_finalizer
assert "LM_VmtReset" not in vmt_finalizer
print("PASS VmtManager lifecycle contract")

solution = (root / "Libmem.NET.sln").read_text(encoding="utf-8")
vcxproj = (root / "src/Libmem.NET.vcxproj").read_text(encoding="utf-8")
build_script = (root / "build.ps1").read_text(encoding="utf-8")
native_build_script = (root / "eng/build-native.ps1").read_text(encoding="utf-8")
smoke_project = (root / "tests/Libmem.NET.SmokeTests/Libmem.NET.SmokeTests.csproj").read_text(encoding="utf-8")
hook_project = (root / "tests/Libmem.NET.HookVmtTests/Libmem.NET.HookVmtTests.csproj").read_text(encoding="utf-8")
hook_runtime_source = (root / "tests/Libmem.NET.HookVmtTests/Program.cs").read_text(encoding="utf-8")
for required_vmt_failure_probe in [
    "VMT failure-state backing page",
    "failureVmt.Reset()",
    "Failed VmtManager.Reset must leave the manager active for retry.",
    "VmtManager.Dispose should surface restore failure while retaining ownership.",
    "Retried VmtManager.Dispose should dispose the manager after restoration succeeds.",
    "VmtFailureNativeMethods.VirtualAlloc",
]:
    assert required_vmt_failure_probe in hook_runtime_source, (
        f"Hook/VMT runtime tests lost VMT failure-retry coverage: {required_vmt_failure_probe}"
    )
injector_project = (root / "tests/Libmem.NET.InjectorTests/Libmem.NET.InjectorTests.csproj").read_text(encoding="utf-8")
sample_project = (root / "samples/Example.csproj").read_text(encoding="utf-8")
sample_source = (root / "samples/Example.cs").read_text(encoding="utf-8")
hook_sample_project = (root / "samples/HookLifecycle/HookLifecycle.csproj").read_text(encoding="utf-8")
hook_sample_source = (root / "samples/HookLifecycle/Program.cs").read_text(encoding="utf-8")
for required_sample_api in [
    "ProcessSession.Open",
    "session.Modules.Enumerate",
    "session.Threads.Enumerate",
    "session.Memory.Allocate",
    "session.Memory.Write",
    "session.Memory.Read",
    "session.Memory.Protect",
    "session.Scanner.SigScan",
    "LibmemException",
]:
    assert required_sample_api in sample_source, (
        f"C# consumer sample lost recommended API: {required_sample_api}"
    )
assert "NativeApi.Attach" not in sample_source
assert "Hearthstone" not in sample_source
print("PASS C# consumer sample contract")

for hook_sample_marker in [
    "ProcessSession.Open",
    "session.Hooks.Install",
    "hook.Trampoline",
    "hook.PatchedBytes",
    "hook.Remove",
    "LibmemException",
]:
    assert hook_sample_marker in hook_sample_source, (
        f"Hook lifecycle sample lost required API: {hook_sample_marker}"
    )
assert "<PlatformTarget>x64</PlatformTarget>" in hook_sample_project
assert "<ProjectReference" not in hook_sample_project, (
    "Hook lifecycle sample must consume the already-built Libmem.NET artifact in dotnet CLI CI."
)
assert "artifacts\\managed\\x64\\$(Configuration)\\Libmem.NET.dll" in hook_sample_project
assert "artifacts\\managed\\x64\\$(Configuration)\\Ijwhost.dll" in hook_sample_project
assert "artifacts\\native\\x64\\$(Configuration)\\bin\\libmem.dll" in hook_sample_project
print("PASS Hook lifecycle consumer sample contract")

nuget_package_project = (root / "packaging/Libmem.NET.csproj").read_text(encoding="utf-8")
nuget_targets = (root / "packaging/Libmem.NET.targets").read_text(encoding="utf-8")
nuget_consumer_project = (root / "tests/Libmem.NET.NuGetConsumer/Libmem.NET.NuGetConsumer.csproj").read_text(encoding="utf-8")
nuget_consumer_source = (root / "tests/Libmem.NET.NuGetConsumer/Program.cs").read_text(encoding="utf-8")

api_reference = (root / "docs/API.md").read_text(encoding="utf-8")
for api_reference_marker in [
    "## Recommended entry point",
    "## ProcessSession model",
    "## Manager APIs",
    "## Owned resources",
    "## Exception model",
    "## Normal non-exception results",
    "## Static compatibility facade",
    "## Upstream compatibility workarounds",
    "LM_GetProcessEx start time",
    "## IntelliSense documentation",
    "## Hook consumer example",
    "Hook conflict and trampoline boundaries",
    "api/Libmem.NET.PublicApi.txt",
]:
    assert api_reference_marker in api_reference, (
        f"API reference lost required section: {api_reference_marker}"
    )
for forbidden_api_coupling in [
    "Hearthstone.exe",
    "HearthstoneBot",
    "GameState",
    "UnityPlayer",
    "ManagedMod",
]:
    assert forbidden_api_coupling not in api_reference, (
        f"API reference contains application-specific coupling: {forbidden_api_coupling}"
    )
print("PASS consumer API reference contract")

nuget_verifier_path = root / "tests/verify_nuget_package.py"
nuget_verifier = nuget_verifier_path.read_text(encoding="utf-8")
compile(nuget_verifier, str(nuget_verifier_path), "exec")
for package_marker in [
    "<PackageId>Libmem.NET</PackageId>",
    r"lib\net8.0\Libmem.NET.dll",
    r"lib\net8.0\Libmem.NET.xml",
    r"runtimes\win-x64\native\libmem.dll",
    r"runtimes\win-x64\native\Ijwhost.dll",
    r"buildTransitive\Libmem.NET.targets",
    "<RepositoryType>git</RepositoryType>",
    "<RepositoryCommit",
]:
    assert package_marker in nuget_package_project, (
        f"NuGet package lost required asset: {package_marker}"
    )
assert "ContentWithTargetPath" in nuget_targets
assert "'$(OS)' != 'Windows_NT'" in nuget_targets
assert "currently supports only Windows x64" in nuget_targets
for consumer_marker in [
    "ProcessSession.Open",
    "session.Memory.Allocate",
    "session.Memory.Write",
    "session.Memory.Read",
]:
    assert consumer_marker in nuget_consumer_source
print("PASS local NuGet package contract")

nuget_package_script = (root / "eng/package-nuget.ps1").read_text(encoding="utf-8")
for package_script_marker in [
    "Libmem.NET.csproj",
    "Libmem.NET.xml",
    "RepositoryCommit",
    "git -C $repoRoot rev-parse HEAD",
    "-dev.$shortCommit",
    "package-version.txt",
    "verify_nuget_package.py",
]:
    assert package_script_marker in nuget_package_script, (
        f"NuGet packaging script lost required behavior: {package_script_marker}"
    )
print("PASS NuGet packaging script contract")

consumption_guide = (root / "docs/CONSUMPTION.md").read_text(encoding="utf-8")
for consumption_marker in [
    "## 1. Runtime ZIP",
    "## 2. Git Submodule",
    "## 3. NuGet package",
    "Libmem.NET",
    "runtimes/win-x64/native",
    "PackageReference",
    "## NuGet release acceptance criteria",
]:
    assert consumption_marker in consumption_guide, (
        f"Consumption guide lost required section: {consumption_marker}"
    )
assert "Trusted Publishing" in consumption_guide
assert "NUGET_USER" in consumption_guide
print("PASS consumption guide contract")
test_target_project = (root / "tests/Libmem.NET.TestTarget/Libmem.NET.TestTarget.csproj").read_text(encoding="utf-8")
external_process_project = (root / "tests/Libmem.NET.ExternalProcessTests/Libmem.NET.ExternalProcessTests.csproj").read_text(encoding="utf-8")

assert "Debug|x86 = Debug|x86" in solution
assert "Release|x86 = Release|x86" in solution
assert "Debug|Win32" in vcxproj and "Release|Win32" in vcxproj
assert "<LibmemPlatformLabel Condition=\"'$(Platform)' == 'Win32'\">x86</LibmemPlatformLabel>" in vcxproj
assert "_WIN64;" not in vcxproj
assert "[ValidateSet('x64', 'x86')]" in build_script
assert "[ValidateSet('x64', 'x86')]" in native_build_script
for script in [
    (root / "eng/package-runtime.ps1").read_text(encoding="utf-8"),
    (root / "eng/write-manifest.ps1").read_text(encoding="utf-8"),
]:
    assert "[ValidateSet('x64', 'x86')]" in script
for project in [sample_project, smoke_project, hook_project, injector_project]:
    assert "<Platforms>x64;x86</Platforms>" in project
    assert "<PlatformTarget>$(Platform)</PlatformTarget>" in project
assert "<Platforms>x64</Platforms>" in test_target_project, "TestTarget must remain x64-only for the current roadmap."
assert "<PlatformTarget>x64</PlatformTarget>" in test_target_project
assert "<Platforms>x64</Platforms>" in external_process_project, "ExternalProcessTests must remain x64-only for the current roadmap."
assert "<PlatformTarget>x64</PlatformTarget>" in external_process_project
assert "<PlatformTarget>x64</PlatformTarget>" in nuget_consumer_project, "NuGetConsumer must remain x64-only."
assert "Libmem.NET" in nuget_consumer_project
assert "lm_address_t native_address(UInt64 value" in source
assert "lm_size_t native_size(UInt64 value" in source
assert "bool bad_address(UInt64 value)" in source
assert "Address does not fit the current process architecture." in source
assert "Size or index does not fit the current process architecture." in source
print("PASS x86/x64 architecture contract")

test_target_source = (root / "tests/Libmem.NET.TestTarget/Program.cs").read_text(encoding="utf-8")
external_process_test_source = (root / "tests/Libmem.NET.ExternalProcessTests/Program.cs").read_text(encoding="utf-8")
assert "Marshal.AllocHGlobal" in test_target_source
assert "NativeMethods.VirtualAlloc" in test_target_source
assert "NativeMethods.VirtualProtect" in test_target_source
assert "pageExecuteRead = 0x20" in test_target_source
assert "hookSource=0x" in test_target_source
assert "hookDestination=0x" in test_target_source
assert 'command.StartsWith("call "' in test_target_source
assert 'command.StartsWith("protect "' in test_target_source
assert "READY pid=" in test_target_source
for required_call in [
    "ProcessSession.Open",
    "ProcessSession.Open(ready.Pid)",
    "NativeApi.EnumProcesses",
    "session.Memory.Read",
    "session.Memory.Write",
    "session.Memory.Allocate",
    "session.Memory.Protect",
    "session.Scanner.SigScan",
    "session.Hooks.Install",
    "remoteHook.Trampoline",
    "ExpectedPatchedBytes",
    "SetTargetProtection",
    "pageNoAccess",
    "retryHook.Remove",
    "ProtectionOf(process!, ready.HookSource)",
    "exitReclaimedHook.Remove",
    "session.Memory.Free(exitReclaimedHook.Trampoline",
    "sourceProtectionAfterFailedRemove",
    "NativeApi.FindSegment",
    "session.IsAlive",
    "session.Refresh",
]:
    assert required_call in external_process_test_source, (
        f"External-process runtime coverage lost required call: {required_call}"
    )
print("PASS external-process runtime coverage contract")

readme_zh = (root / "README.md").read_text(encoding="utf-8")
readme_en = (root / "README.en.md").read_text(encoding="utf-8")
for readme in [readme_zh, readme_en]:
    assert ".github/workflows/external-process-tests.yml" in readme
    assert "Libmem.NET.TestTarget" in readme
print("PASS external-process documentation contract")
for readme in [readme_zh, readme_en]:
    assert ".github/workflows/nuget-consumer-tests.yml" in readme
    assert "Libmem.NET" in readme
assert "PR 自动验证集中在 Build" in readme_zh
assert "single automatic PR gate" in readme_en
print("PASS NuGet package documentation contract")
for readme in [readme_zh, readme_en]:
    assert 'InvalidOperationException("Injection failed")' not in readme
    assert 'InvalidOperationException("Hook failed")' not in readme
print("PASS manager README semantics contract")

manifest_script = (root / "eng/write-manifest.ps1").read_text(encoding="utf-8")
package_script = (root / "eng/package-runtime.ps1").read_text(encoding="utf-8")
assert "<GenerateXMLDocumentationFiles>true</GenerateXMLDocumentationFiles>" in vcxproj
assert "<Xdcmake>" in vcxproj
assert "<OutputFile>$(OutDir)$(TargetName).xml</OutputFile>" in vcxproj
assert "(Join-Path $managed 'Libmem.NET.xml')" in package_script
assert "/// <summary>" in header
assert "ProcessSession" in header and "LibmemException" in header
print("PASS XML documentation build/package contract")
assert "Libmem.NET.xml" in readme_zh
assert "IntelliSense" in readme_zh
assert "Libmem.NET.xml" in readme_en
assert "IntelliSense" in readme_en
print("PASS XML documentation README contract")
verify_script_path = root / "eng/verify-package.py"
verify_script = verify_script_path.read_text(encoding="utf-8")
compile(verify_script, str(verify_script_path), "exec")
build_workflow = (root / ".github/workflows/build.yml").read_text(encoding="utf-8")
reusable_workflow = (root / ".github/workflows/reusable-build.yml").read_text(encoding="utf-8")
release_workflow = (root / ".github/workflows/release.yml").read_text(encoding="utf-8")
hook_workflow = (root / ".github/workflows/hook-vmt-tests.yml").read_text(encoding="utf-8")
injector_workflow = (root / ".github/workflows/injector-tests.yml").read_text(encoding="utf-8")
external_process_workflow = (root / ".github/workflows/external-process-tests.yml").read_text(encoding="utf-8")
nuget_consumer_workflow = (root / ".github/workflows/nuget-consumer-tests.yml").read_text(encoding="utf-8")

assert "schemaVersion = 2" in manifest_script
assert "Get-FileHash" in manifest_script
assert "sha256 = " in manifest_script
assert '$archiveChecksum = "$archive.sha256"' in package_script
assert "Get-FileHash -Path $archive -Algorithm SHA256" in package_script
assert "verify-package.py" in build_workflow
assert "verify-package.py" in reusable_workflow
assert "include-nuget:" in reusable_workflow
assert "Package official NuGet release" in reusable_workflow
assert "package-nuget.ps1" in reusable_workflow
assert "verify-package.py" in release_workflow
assert "--expected-repository-commit" in release_workflow
assert "id-token: write" in release_workflow
assert "NuGet/login@v1" in release_workflow
assert "secrets.NUGET_USER" in release_workflow
assert "Libmem.NET.${EXPECTED_VERSION}.nupkg" in release_workflow
assert "dotnet nuget push" in release_workflow
assert "https://api.nuget.org/v3/index.json" in release_workflow
assert "Libmem.NET-windows-x64.zip.sha256" in release_workflow
assert "Libmem.NET-windows-x86.zip.sha256" not in release_workflow
release_notes_script_path = root / "eng/render-release-notes.py"
release_notes_script = release_notes_script_path.read_text(encoding="utf-8")
compile(release_notes_script, str(release_notes_script_path), "exec")
assert "render-release-notes.py" in release_workflow
assert "--notes-file dist/release-notes.md" in release_workflow
assert '--title "Libmem.NET $TAG_NAME"' in release_workflow
assert "--generate-notes" not in release_workflow
subprocess.run(
    [sys.executable, str(root / "tests/test_release_notes.py")],
    cwd=root,
    check=True,
)
print("PASS formal GitHub Release notes contract")
x86_runtime_setup = (root / "eng/setup-dotnet-x86.ps1").read_text(encoding="utf-8")
assert "-Architecture x86" in x86_runtime_setup
assert "DOTNET_ROOT_X86" in x86_runtime_setup
assert "DOTNET_ROOT(x86)" in x86_runtime_setup

# x64 is the supported/default CI and release target.
assert "Release x64" in build_workflow
assert "Platform x86" not in build_workflow
assert "setup-dotnet-x86.ps1" not in build_workflow
assert build_workflow.count(".\\build.ps1 -Configuration Release -Platform x64") == 1
assert build_workflow.count(".\\build.ps1 -Configuration Debug -Platform x64") == 1

def workflow_steps(workflow):
    """Extract named step blocks at the workflow's step indentation."""
    matches = list(re.finditer(r"^      - name: (.+)$", workflow, re.MULTILINE))
    return {
        match.group(1): workflow[match.end():matches[i + 1].start() if i + 1 < len(matches) else len(workflow)]
        for i, match in enumerate(matches)
    }

assert re.search(
    r"workflow_dispatch:\s+inputs:\s+debug:\s+description: [^\n]+\s+"
    r"type: boolean\s+required: false\s+default: false", build_workflow
), "Debug must be an opt-in boolean input, disabled by default."
build_steps = workflow_steps(build_workflow)
debug_steps = {"Build Libmem.NET Debug x64", "Run Debug runtime smoke tests"}
for name, block in build_steps.items():
    conditions = re.findall(r"^        if: (.+)$", block, re.MULTILINE)
    if name in debug_steps:
        assert conditions == ["${{ github.event_name == 'workflow_dispatch' && inputs.debug }}"], (
            f"{name} must only run with manual Debug opt-in."
        )
    else:
        assert not conditions, f"Default Release validation must remain unconditional: {name}"
assert debug_steps <= build_steps.keys()
print("PASS Release default and manual Debug policy")

release_steps = workflow_steps(release_workflow)
assert re.findall(r"^        if: (.+)$", release_steps["Publish GitHub Release"], re.MULTILINE) == [
    "github.ref_type == 'tag' && inputs.publish-nuget != true"
], "GitHub downloads must be tag-only and separate from a later NuGet run."
for name in ["NuGet login (OIDC)", "Publish Libmem.NET to nuget.org"]:
    assert re.findall(r"^        if: (.+)$", release_steps[name], re.MULTILINE) == [
        "github.event_name == 'workflow_dispatch' && inputs.publish-nuget == true"
    ], f"NuGet publication must require manual opt-in: {name}"
assert 'test "$GITHUB_REF_TYPE" = "tag"' in release_steps["Validate release ref"]
assert 'gh release view "$TAG_NAME"' in release_steps["Validate release ref"]
assert '--prerelease --latest=false' in release_steps["Publish GitHub Release"]
assert 'artifacts/package/*.nupkg' in build_workflow
assert 'artifacts/package/*.nupkg' in reusable_workflow
assert 'artifacts/nuget/*.nupkg' not in reusable_workflow, "Mixed upload roots break release download paths."
assert 'gh release download' in release_steps["Download published NuGet asset for later push"]
for name in ["Validate release package", "Validate NuGet package", "Render formal release notes"]:
    assert not re.search(r"^        if:", release_steps[name], re.MULTILINE), (
        f"Release branches must still perform {name}."
    )
assert "if: github.ref_type == 'branch'" in release_steps["Release dry run complete"]
assert "No GitHub Release or nuget.org package was published." in release_steps["Release dry run complete"]
assert "gh api -X DELETE" not in release_workflow
assert release_workflow.count("gh release create") == 1
assert release_workflow.count("dotnet nuget push") == 1
assert release_workflow.count("NuGet/login@v1") == 1
print("PASS tag-only publication and branch dry-run policy")

release_doc = (root / "docs/RELEASES.md").read_text(encoding="utf-8")
release_checklist = (root / "docs/RELEASE_CHECKLIST.md").read_text(encoding="utf-8")
for readme in [readme_zh, readme_en]:
    assert "docs/MIGRATION.md" in readme
    assert "docs/RELEASES.md" in readme
    assert "docs/RELEASE_CHECKLIST.md" in readme
    assert "LibmemCli-windows-x64.zip" in readme, "Historical assets must retain their real names."
    assert "ProcessSession.Open((uint)Environment.ProcessId)" in readme
for marker in ["Public API freeze", "Independent NuGet consumer", "NuGet account-side setup", "NUGET_USER"]:
    assert marker in release_checklist
assert "Historical v1.0.0" in release_doc
assert "LibmemCli-windows-x64.zip" in release_doc
for file in ["v1.0.0.md", "v1.0.0-github.md"]:
    historical_notes = (root / "docs/releases" / file).read_text(encoding="utf-8")
    assert "Historical release before the Libmem.NET identity migration" in historical_notes
    assert "LibmemCli.dll" in historical_notes
print("PASS release documentation and historical identity contract")

for suite in ["SmokeTests", "ExternalProcessTests", "HookVmtTests", "InjectorTests", "NuGetConsumer"]:
    assert f"Libmem.NET.{suite}" in build_workflow, f"Unified PR gate lost {suite}"
for step in ["Run C# example", "Run Hook lifecycle sample", "Publish NuGet consumer", "Reject non-x64 NuGet consumer", "Verify runtime package"]:
    assert step in build_workflow, f"Unified PR gate lost {step}"
for manual_workflow in [hook_workflow, injector_workflow, external_process_workflow, nuget_consumer_workflow]:
    assert "workflow_dispatch:" in manual_workflow
    assert "pull_request:" not in manual_workflow, "Specialized suites must not duplicate the unified PR build."
for consumer in [sample_source, nuget_consumer_source, external_process_test_source]:
    assert "using NativeApi = global::Libmem.NET.Libmem;" in consumer
    assert not re.search(r"(?<![\w.])Libmem\.(?!NET\b)", consumer), "Ambiguous static facade reference remains."
assert "repository = 'CardResearchLab/Libmem.NET'" in manifest_script
assert "Hook and VMT x64" in hook_workflow
assert "setup-dotnet-x86.ps1" not in hook_workflow
assert "Injector x64" in injector_workflow
assert "setup-dotnet-x86.ps1" not in injector_workflow
assert "External Process x64" in external_process_workflow
assert "Libmem.NET.TestTarget" in external_process_workflow
assert "Libmem.NET.ExternalProcessTests" in external_process_workflow
assert "setup-dotnet-x86.ps1" not in external_process_workflow
assert "NuGet Consumer x64" in nuget_consumer_workflow
assert "package-nuget.ps1" in nuget_consumer_workflow
assert "package-version.txt" in nuget_consumer_workflow
assert "Libmem.NET.NuGetConsumer" in nuget_consumer_workflow
assert "Publish NuGet consumer" in nuget_consumer_workflow
assert "Reject non-x64 NuGet consumer" in nuget_consumer_workflow
assert "Libmem.NET.dll" in nuget_consumer_workflow
assert "Ijwhost.dll" in nuget_consumer_workflow
assert "nuget.org" not in nuget_consumer_workflow
assert "setup-dotnet-x86.ps1" not in nuget_consumer_workflow
assert "needs: [build-x64]" in release_workflow
assert "build-x86:" not in release_workflow

# Keep the reusable/manual x86 path available for future compatibility work.
assert "setup-dotnet-x86.ps1" in reusable_workflow
assert "platform:" in reusable_workflow
assert "x64 is the supported release target" in reusable_workflow
print("PASS x64-first package integrity and release provenance contract")

subprocess.run(
    [
        sys.executable,
        str(root / "eng/check-public-api.py"),
        "--header",
        str(root / "src/Libmem.NET.h"),
        "--baseline",
        str(root / "api/Libmem.NET.PublicApi.txt"),
    ],
    cwd=root,
    check=True,
)
print("PASS committed public API baseline")

version = (root / "VERSION").read_text(encoding="utf-8").strip()
assert re.fullmatch(
    r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)"
    r"(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?", version
), (
    f"VERSION must use MAJOR.MINOR.PATCH[-PRERELEASE] format: {version!r}"
)

assembly_info = (root / "src/AssemblyInfo.cpp").read_text(encoding="utf-8")
assembly_version = version.split("-", 1)[0] + ".0"
assert f'AssemblyVersionAttribute("{assembly_version}")' in assembly_info
assert f'AssemblyFileVersionAttribute("{assembly_version}")' in assembly_info
assert f'AssemblyInformationalVersionAttribute("{version}")' in assembly_info
print("PASS version metadata:", version)

print("SOURCE CONTRACT CHECKS PASS (NOT A WINDOWS RUNTIME TEST)")
