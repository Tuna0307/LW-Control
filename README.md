# LW-Control

Independent Last War controller research, a C# Windows planning application,
and the earlier Python offline prototype.

## Run the C# Windows application

Requires the .NET 10 SDK on Windows.

```powershell
dotnet run --project src/LWControl.Desktop/LWControl.Desktop.csproj
dotnet run --project tests/LWControl.Core.Checks/LWControl.Core.Checks.csproj
```

The desktop app saves settings, imports observations, previews daily-claim
decisions, and exports plans. It has no live game adapter and sends no actions.
See [implementation details](docs/csharp-implementation.md).

## Run the earlier Python prototype

Requires Python 3.10 or newer; no third-party dependencies.

```sh
python lwcontrol.py --scenario examples/demo.json
python -m unittest discover -s tests -v
```

The example uses invented daily-claim and resource-batch state. It prints JSON
activity events while demonstrating settings, cooldowns, health checks,
request/result correlation, effect checks, and stop/resume behavior.

This is not yet a working Last War bot. The only adapter is an in-memory mock;
there is no game connection, game-file access, or executable launch.

## Contents

- `lwcontrol.py`: independent scheduler, mock adapter, and scenario CLI.
- `examples/demo.json`: synthetic scenario, not a game protocol.
- `tests/test_controller.py`: controller decisions and failure-handling tests.

All source code here is newly written. The Python prototype is synchronous and uses
process-local state. The C# application has a Windows Forms interface. A live adapter is not implemented.
Live integration will require bounded I/O, persistent request tracking,
stronger result verification, and Windows testing.

## Reconstruction direction

C# is the chosen language for the planned Windows application because LWControl
retains named .NET assemblies and managed method bodies. The current Python code
remains an offline prototype. The first C# desktop shell and independent
daily-claim planning library are now in `src/`.

The first feature selected for detailed study is daily free claims. Missing
developer documentation does not block static analysis of its decision logic.

## Research

- [Initial architecture assessment](docs/architecture.md)
- [Artifact hashes and marker evidence](docs/evidence.json)
- [Managed-code recovery assessment](docs/recovery-assessment.md)
