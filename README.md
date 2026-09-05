# LW-Control

An independent **offline controller prototype** for early development.

## Run

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

All source code here is newly written. The prototype is synchronous, uses
process-local state, and has no desktop UI. A live adapter requires a defined,
developer-supported test interface, bounded I/O, persistent request tracking,
and stronger result verification.
