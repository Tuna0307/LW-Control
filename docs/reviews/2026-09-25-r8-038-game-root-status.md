# R8-038 — restore game_root_status public contract

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** replace the rebuild-specific public installation-status projection with the recovered native status schema and root-resolution behavior, without weakening the rebuild's separate launch-safety checks.

## Authority

Primary native evidence:

- `game_root_status` command handler RVA `0x15B277`-`0x15BB41`;
- root resolver / candidate assembly RVA `0x3323E7`-`0x333023`;
- candidate ingestion / ancestor walk RVA `0x33213C`-`0x332366`;
- candidate normalizer RVA `0x333302`-`0x33365F`;
- top-level status serializer RVA `0x33365F`-`0x33377A`;
- candidate serializer RVA `0x33377A`-`0x33383B`;
- native public validity predicate RVA `0x33383B`-`0x333A73`;
- selected-root projection RVA `0x333A73`-`0x333B63`;
- retained frontend wrapper `game_root_status()`, whose Overview consumer gates setup on `valid`.
## Exact public shape

Native serializes exactly four top-level fields:

```json
{
  "root": "...",
  "source": "...",
  "valid": true,
  "candidates": [
    { "path": "...", "source": "..." }
  ]
}
```

No rebuild-only `error`, `launcherPath`, `gamePath`, `xluaPath`, `is64Bit`, `gameMachine` or `xluaMachine` fields belong to this public command.

When no valid candidate exists, native returns:

```json
{"root":"","source":"","valid":false,"candidates":[]}
```

The command can fail with `STATE_UNAVAILABLE` / `path state is unavailable` when the shared path state is unavailable.
## Native public validity predicate

The public status predicate is intentionally lightweight. A candidate root is valid when both of these exist:

- `Game/LastWar.exe`;
- directory `Game/LastWar_Data/Plugins/x86_64`.

The native public predicate does not require `LastWarLauncher.exe`, a specific `xlua.dll`, PE architecture checks, or the rebuild's compatibility fingerprints.

R8-038 therefore keeps two distinct layers:

- `game_root_status` uses the recovered native public predicate;
- launch/repair admission continues using the rebuild's stricter existing validation.

This preserves safety without leaking rebuild-only diagnostics into the original public command.
## Candidate sources and selection

Static recovery exposes the native ordered sources:

1. saved selection (`saved`);
2. `LASTWAR_BRIDGE_ROOT` (`environment`);
3. application-nearby root (`nearby`);
4. nearby `bridge-root.txt` (`bridge-root-file`);
5. `%LOCALAPPDATA%/FunFly/Last War-Survival Game` (`default`);
6. running `LastWar.exe` discovery (`process`);
7. uninstall-registry discovery (`registry`).

Candidates are normalized toward an ancestor that satisfies the native public predicate and are deduplicated by normalized path. The first surviving candidate becomes `root` / `source`; all surviving candidates remain in `candidates`.

The native implementation persists the selected root through `game-root.txt` and uses PowerShell/CIM plus uninstall-registry discovery. The rebuild keeps its existing LocalConfig persistence and uses equivalent C# process/registry discovery. Those implementation mechanisms are not claimed byte-identical; the public ordering, labels, predicate and result schema are the parity target.
## Implementation and validation

R8-038 adds a separate `NativeGameRootStatus` projection in `GameInstallationService` and routes only `game_root_status` through it. Existing internal `GameRootStatus` validation remains unchanged for launch and repair code.

Deterministic checks prove:

- exact four-field top-level JSON shape;
- exact candidate `{path,source}` shape;
- empty-string invalid state;
- source ordering and first-candidate selection;
- ancestor normalization of `LastWar.exe` and registry icon paths;
- normalized-path deduplication with first source retained;
- exact native public validity predicate;
- stricter internal launch validation remains separate;
- `STATE_UNAVAILABLE`;
- backend routing to the native projection.

Validation:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.
