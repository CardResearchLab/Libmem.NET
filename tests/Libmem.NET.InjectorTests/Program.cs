using Libmem.Net;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string CreateFixtureCopy(string directory, string source, string suffix)
{
    var fileName = $"Libmem.NET.InjectorFixture.{suffix}.{Guid.NewGuid():N}.dll";
    var destination = Path.Combine(directory, fileName);
    File.Copy(source, destination, overwrite: false);
    return destination;
}

Console.WriteLine("Libmem.NET Injector runtime tests");

using var session = Libmem.Attach((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Could not attach to the current process.");

var injector = session.Injector;
Check(injector is not null, "ProcessSession.Injector returned null.");

var sourceLibrary = Path.Combine(AppContext.BaseDirectory, "libmem.dll");
Check(File.Exists(sourceLibrary), "libmem.dll was not copied next to the test executable.");

var fixtureDirectory = Path.Combine(Path.GetTempPath(), "Libmem.NET.InjectorTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDirectory);

string? manualFixture = null;
string? disposeFixture = null;

try
{
    manualFixture = CreateFixtureCopy(fixtureDirectory, sourceLibrary, "manual");
    var manualName = Path.GetFileName(manualFixture);

    using var manual = injector.InjectLibrary(manualFixture)
        ?? throw new InvalidOperationException("Injector.InjectLibrary returned null for the manual-unload fixture.");

    Check(manual.IsActive, "InjectedModuleHandle should be active immediately after injection.");
    Check(!manual.IsDisposed, "InjectedModuleHandle should not be disposed immediately after injection.");
    Check(string.Equals(manual.RequestedPath, Path.GetFullPath(manualFixture), StringComparison.OrdinalIgnoreCase),
        "InjectedModuleHandle did not preserve the normalized requested path.");

    var moduleSnapshot = manual.Module;
    Check(moduleSnapshot is not null, "InjectedModuleHandle.Module returned null.");
    Check(string.Equals(moduleSnapshot!.Name, manualName, StringComparison.OrdinalIgnoreCase),
        "Injected module name does not match the fixture file name.");
    Check(session.Modules.Find(manualName) is not null, "Injected fixture is not visible in the target module list.");

    Check(manual.Unload(), "InjectedModuleHandle.Unload failed.");
    Check(!manual.IsActive, "InjectedModuleHandle should be inactive after Unload.");
    Check(manual.Unload(), "InjectedModuleHandle.Unload should be idempotent.");
    Check(session.Modules.Find(manualName) is null, "Manual-unload fixture is still present after its owned reference was released.");

    disposeFixture = CreateFixtureCopy(fixtureDirectory, sourceLibrary, "dispose");
    var disposeName = Path.GetFileName(disposeFixture);

    var disposable = injector.InjectLibrary(disposeFixture)
        ?? throw new InvalidOperationException("Injector.InjectLibrary returned null for the dispose fixture.");

    Check(disposable.IsActive, "Dispose fixture should be active immediately after injection.");
    Check(session.Modules.Find(disposeName) is not null, "Dispose fixture is not visible in the target module list.");

    ((IDisposable)disposable).Dispose();
    ((IDisposable)disposable).Dispose();
    Check(disposable.IsDisposed, "InjectedModuleHandle should report disposed after repeated Dispose calls.");
    Check(!disposable.IsActive, "InjectedModuleHandle should be inactive after repeated Dispose calls.");
    Check(disposable.Unload(), "InjectedModuleHandle.Unload should remain idempotent after successful Dispose.");
    Check(session.Modules.Find(disposeName) is null, "Dispose fixture is still present after Dispose.");

    var missingThrows = false;
    try
    {
        _ = injector.InjectLibrary(Path.Combine(fixtureDirectory, "missing.dll"));
    }
    catch (FileNotFoundException)
    {
        missingThrows = true;
    }
    Check(missingThrows, "Injector should reject a library path that does not exist.");
}
finally
{
    foreach (var fixture in new[] { manualFixture, disposeFixture })
    {
        if (fixture is null)
            continue;

        try
        {
            if (File.Exists(fixture))
                File.Delete(fixture);
        }
        catch
        {
            // Keep cleanup from hiding the injection test result.
        }
    }

    try
    {
        if (Directory.Exists(fixtureDirectory))
            Directory.Delete(fixtureDirectory, recursive: true);
    }
    catch
    {
        // Best effort test cleanup.
    }
}

Console.WriteLine("INJECTOR RUNTIME TESTS PASS");
