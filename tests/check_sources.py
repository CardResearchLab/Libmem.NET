"""Non-Windows, source-only checks; does not compile mixed-mode C++/CLI."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
header = (root / 'src/LibmemCli.h').read_text(encoding='utf-8')
source = (root / 'src/LibmemCli.cpp').read_text(encoding='utf-8')
for file in ['src/LibmemCli.vcxproj','samples/Example.csproj']:
    ET.parse(root / file)
    print('PASS XML', file)
for owner in ['Libmem', 'ProcessInfo', 'HookHandle', 'VmtManager']:
    body = header.split('public ref class ' + owner, 1)[1].split('\n    };', 1)[0]
    declarations = re.findall(r'(?<!::)\b(\w+)\s*\([^;{}]*\)\s*;', body)
    declarations = {s for s in declarations if s not in {'get'}}
    impls = set(re.findall(r'\b' + owner + r'::(\w+)\s*\(', source))
    assert not (declarations - impls), f'{owner} unimplemented: {declarations - impls}'
    print('PASS declarations implemented:', owner, len(declarations))
for upstream in ('LM_EnumProcesses','LM_ReadMemoryEx','LM_WriteMemoryEx','LM_VmtFree','LM_GetArchitecture','LM_FindSymbolAddressDemangled'):
    assert upstream in source
    print('PASS native reference:', upstream)
expected = {
    'LM_EnumProcesses','LM_GetProcess','LM_GetProcessEx','LM_GetCommandLine','LM_FreeCommandLine',
    'LM_FindProcess','LM_IsProcessAlive','LM_GetBits','LM_GetSystemBits','LM_EnumThreads',
    'LM_EnumThreadsEx','LM_GetThread','LM_GetThreadEx','LM_GetThreadProcess','LM_EnumModules',
    'LM_EnumModulesEx','LM_FindModule','LM_FindModuleEx','LM_LoadModule','LM_LoadModuleEx',
    'LM_UnloadModule','LM_UnloadModuleEx','LM_EnumSymbols','LM_FindSymbolAddress','LM_DemangleSymbol',
    'LM_FreeDemangledSymbol','LM_EnumSymbolsDemangled','LM_FindSymbolAddressDemangled','LM_EnumSegments',
    'LM_EnumSegmentsEx','LM_FindSegment','LM_FindSegmentEx','LM_ReadMemory','LM_ReadMemoryEx',
    'LM_WriteMemory','LM_WriteMemoryEx','LM_SetMemory','LM_SetMemoryEx','LM_ProtMemory','LM_ProtMemoryEx',
    'LM_AllocMemory','LM_AllocMemoryEx','LM_FreeMemory','LM_FreeMemoryEx','LM_DeepPointer','LM_DeepPointerEx',
    'LM_DataScan','LM_DataScanEx','LM_PatternScan','LM_PatternScanEx','LM_SigScan','LM_SigScanEx',
    'LM_GetArchitecture','LM_Assemble','LM_AssembleEx','LM_FreePayload','LM_Disassemble','LM_DisassembleEx',
    'LM_FreeInstructions','LM_CodeLength','LM_CodeLengthEx','LM_HookCode','LM_HookCodeEx','LM_UnhookCode',
    'LM_UnhookCodeEx','LM_VmtNew','LM_VmtHook','LM_VmtUnhook','LM_VmtGetOriginal','LM_VmtReset','LM_VmtFree'
}
missing = sorted(name for name in expected if name not in source)
assert not missing, f'Native API references missing: {missing}'
print('PASS complete native public API reference set:', len(expected))
print('SOURCE CHECKS PASS (NOT A WINDOWS BUILD TEST)')
