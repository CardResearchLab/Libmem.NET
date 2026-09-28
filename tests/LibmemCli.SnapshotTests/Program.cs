using LibmemCli;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void CheckGetterOnly(Type type, params string[] propertyNames)
{
    foreach (var propertyName in propertyNames)
    {
        var property = type.GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{type.Name}.{propertyName} was not found.");
        Check(property.CanRead, $"{type.Name}.{propertyName} must be readable.");
        Check(!property.CanWrite, $"{type.Name}.{propertyName} must not expose a public setter.");
    }
}

Console.WriteLine("LibmemCli immutable snapshot runtime tests");

using var session = Libmem.Attach((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not attach to the current process.");

CheckGetterOnly(
    typeof(ProcessSnapshot),
    nameof(ProcessSnapshot.Pid),
    nameof(ProcessSnapshot.ParentPid),
    nameof(ProcessSnapshot.Architecture),
    nameof(ProcessSnapshot.Bits),
    nameof(ProcessSnapshot.StartTime),
    nameof(ProcessSnapshot.Name),
    nameof(ProcessSnapshot.Path));

CheckGetterOnly(
    typeof(ModuleSnapshot),
    nameof(ModuleSnapshot.Base),
    nameof(ModuleSnapshot.End),
    nameof(ModuleSnapshot.Size),
    nameof(ModuleSnapshot.Name),
    nameof(ModuleSnapshot.Path));

var processSnapshot = session.Snapshot;
Check(processSnapshot.Pid == (uint)Environment.ProcessId, "ProcessSnapshot.Pid does not match the attached process.");
Check(processSnapshot.StartTime != 0, "ProcessSnapshot.StartTime should preserve process identity.");
Check(processSnapshot.Bits == session.Bits, "ProcessSnapshot.Bits does not match ProcessSession.");
Check(processSnapshot.Architecture == session.Architecture, "ProcessSnapshot.Architecture does not match ProcessSession.");

var mutableInfo = session.Info;
var originalProcessName = processSnapshot.Name;
mutableInfo.Name = (mutableInfo.Name ?? string.Empty) + ".mutated";
Check(processSnapshot.Name == originalProcessName, "Mutating ProcessInfo changed an existing ProcessSnapshot.");

var modules = session.Modules.Snapshot();
Check(modules.Count > 0, "ModuleManager.Snapshot returned no modules.");

if (modules is ICollection<ModuleSnapshot> collection)
{
    Check(collection.IsReadOnly, "ModuleManager.Snapshot must expose a read-only collection.");
    var addThrows = false;
    try
    {
        collection.Add(modules[0]);
    }
    catch (NotSupportedException)
    {
        addThrows = true;
    }
    Check(addThrows, "The snapshot collection accepted mutation.");
}

var first = modules.First(m => !string.IsNullOrWhiteSpace(m.Name));
Check(first.Base != 0 && first.Size != 0, "ModuleSnapshot does not contain usable module metadata.");

var found = session.Modules.FindSnapshot(first.Name);
Check(found is not null, "ModuleManager.FindSnapshot could not find a snapshotted module.");
Check(found!.Base == first.Base, "FindSnapshot returned a different module base.");

var mutableModule = session.Modules.Find(first.Name)
    ?? throw new InvalidOperationException("ModuleManager.Find could not find the snapshotted module.");
var originalModuleName = first.Name;
mutableModule.Name = mutableModule.Name + ".mutated";
Check(first.Name == originalModuleName, "Mutating ModuleInfo changed an existing ModuleSnapshot.");

var savedModules = session.Modules;
session.Detach();

Check(processSnapshot.Pid == (uint)Environment.ProcessId, "ProcessSnapshot became unreadable after session detach.");
Check(first.Base != 0, "ModuleSnapshot became unreadable after session detach.");

var sessionSnapshotThrows = false;
try
{
    _ = session.Snapshot;
}
catch (ObjectDisposedException)
{
    sessionSnapshotThrows = true;
}
Check(sessionSnapshotThrows, "ProcessSession.Snapshot should reject access after detach.");

var managerSnapshotThrows = false;
try
{
    _ = savedModules.Snapshot();
}
catch (ObjectDisposedException)
{
    managerSnapshotThrows = true;
}
Check(managerSnapshotThrows, "ModuleManager.Snapshot should reject use after its session is detached.");

Console.WriteLine("SNAPSHOT RUNTIME TESTS PASS");
