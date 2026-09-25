# R8-039 — restore game_root_select public contract

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the native cancel/invalid/valid result envelope and persistence boundary for selecting the Last War installation root.

## Authority

Primary native evidence:

- `game_root_select` handler RVA `0x1437DB`-`0x144B76`;
- candidate normalizer RVA `0x333302`-`0x33365F`;
- public root-validity predicate RVA `0x33383B`-`0x333A73`;
- root persistence helper RVA `0x333C6A`-`0x334007`;
- native error strings `INVALID_GAME_ROOT` and `select the folder containing Game\\LastWar.exe`;
- retained frontend wrapper `game_root_select()`, which consumes `canceled` and `valid`.
## Exact public result

Native returns exactly three fields:

```json
{
  "canceled": false,
  "path": "C:\\...",
  "valid": true
}
```

Cancel is a normal success result:

```json
{"canceled":true,"path":null,"valid":false}
```

A user-selected but invalid directory is also a normal result:

```json
{"canceled":false,"path":"<normalized-path>","valid":false}
```

An ordinary invalid choice is not persisted and is not converted into an application error.
## Valid selection and persistence

The selected path is normalized with the same native normalizer used by R8-038. In particular, a selected `Game` directory or `Game\LastWar.exe` path resolves to the installation root when possible.

Validity uses the recovered public predicate:

- `Game/LastWar.exe` exists;
- `Game/LastWar_Data/Plugins/x86_64` is a directory.

Only a valid selection enters the shared path-state persistence path. If that state is unavailable, the command fails with:

- code: `STATE_UNAVAILABLE`;
- message: `path state is unavailable`.

If path normalization/revalidation cannot produce a valid root at the persistence boundary, native uses:

- code: `INVALID_GAME_ROOT`;
- message: `select the folder containing Game\LastWar.exe`.
The original persists the saved root through `game-root.txt`. The rebuild preserves its existing LocalConfig storage, so storage is an equivalent reimplementation rather than byte-identical native plumbing.

R8-039 deliberately does not route the public command through the rebuild's stricter `SaveGameRoot` lifecycle-rebind method. That method remains for internal launch/repair admission, where launcher/xLua/PE/fingerprint checks are intentionally stricter than the original public picker predicate.

## Folder-dialog boundary

The observable cancel/selection result semantics are recovered, but R8-039 does not claim the dialog implementation itself is byte-identical. The original uses its native/Tauri dialog stack; the rebuild continues to use `FolderBrowserDialog`.

The current rebuild dialog description is therefore treated as compatibility plumbing, not as a recovered native string.
## Implementation and validation

R8-039 adds `NativeGameRootSelectionResult` and a separate native-selection persistence path, and routes `LWBridgeWindow.SelectGameRootAsync` through that path.

Deterministic checks prove:

- exact `canceled/path/valid` field set/order;
- cancel = `true/null/false`;
- ordinary invalid selection = normal `false/path/false` result;
- invalid selection does not overwrite the saved root;
- a native-minimal root can be selected and persisted even though stricter internal launch validation rejects it;
- `Game` and `LastWar.exe` selection normalization;
- exact `INVALID_GAME_ROOT` code/message for un-normalizable input;
- exact `STATE_UNAVAILABLE` behavior before persistence;
- backend cancel/invalid/valid routing;
- existing launch/rebind lifecycle regression coverage remains green.

Validation:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.
