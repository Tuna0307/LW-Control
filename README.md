# LWBridge 0.3.1 strict parity recovery

**Current checkpoint:** `LWB-R8-002` (2026-09-24)
**Branch:** `research/offline-controller`

This repository is now a strict one-to-one recovery of the verified LWBridge 0.3.1 reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

Verified SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Direction reset

The earlier R1-R7 work produced substantial reverse-engineering evidence and a functioning reconstruction, but parts of the project drifted into redesign, optimization, owner-specific feature retirement, and current-game-specific substitutions that were not first established as original LWBridge behavior.

That direction is superseded.

From R8 onward, the product authority is the original LWBridge 0.3.1 executable. We recover what the reference actually does and reproduce it one-for-one. No new product feature, removed reference feature, changed workflow, changed default, changed label, changed scan strategy, changed timing, or convenience behavior is accepted without reference evidence.

The end product must work against the current Last War client. Internal compatibility code may differ where necessary, but it must preserve the recovered original product behavior.

## Start here

1. [`AGENTS.md`](AGENTS.md) — mandatory project rules.
2. [`docs/strict-parity-recovery.md`](docs/strict-parity-recovery.md) — current one-to-one directive.
3. [`docs/lwbridge-parity-matrix.md`](docs/lwbridge-parity-matrix.md) — current completion authority.
4. [`docs/implementation-handoff.md`](docs/implementation-handoff.md) — current continuation state.
5. [`docs/README.md`](docs/README.md) — documentation index.
6. [`BACKLOG.md`](BACKLOG.md) — current parity queue.
7. [`task.md`](task.md) — durable historical requirements plus the R8 superseding directive.

The older 47-case Home/Map acceptance matrix is retained as evidence of the reconstructed implementation. It is no longer whole-product or one-to-one completion authority.

## Evidence labels

- **EXACT_BYTES** — original bytes recovered from the verified reference and preserved unchanged.
- **EXACT_CONTRACT** — original behavior recovered with source identity and durable locators.
- **EQUIVALENT_REIMPLEMENTATION** — different internals proven to reproduce an exact recovered contract.
- **DEVIATION** — rebuild behavior not supported by the reference or an original feature intentionally removed/changed.
- **UNKNOWN** — original behavior still needs recovery.

Historical labels such as RECOVERED, LIVE-PROVEN and IMPLEMENTED/OFFLINE-TESTED remain valid for the evidence they describe, but they do not by themselves establish one-to-one parity.

## P0

The highest-priority recovery target is the original protected `bridge-scripts.dat` package and its complete plaintext implementation. The project already recovered major pieces of the loader, key material, device-key, CNG derivation and AES-GCM boundary, but not the whole package contents.

Map Data is no longer allowed to consume weeks of custom redesign while original implementation evidence remains recoverable. The original script/host behavior must be recovered first, then mapped to the current client.

## Historical evidence

Do not delete or rewrite chronological evidence to fit the new direction. R1-R7 documents remain useful provenance. When old documents describe a custom rebuild decision as current product behavior, the R8 parity directive supersedes that decision without erasing the historical record.

## Build

The current source is still buildable with the existing toolchain, but a successful build is not parity proof:

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-build
```

Reference-vs-rebuild comparison and source-attributed recovery are now mandatory parts of completion.
