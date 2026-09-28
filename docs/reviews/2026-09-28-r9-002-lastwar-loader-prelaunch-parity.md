# R9-002 — LastWar.Loader 0.3.29 → 0.3.34 pre-launch trust-boundary parity

**Date:** 2026-09-28
**Status:** STATIC VERSION-PARITY RECOVERY

## Purpose

Compare the native start-game authorization/materialization wrapper in 0.3.29 and 0.3.34 without entering the virtualized bodies and without modifying authentication state.

## Recovered call chains

### 0.3.29

```text
startGame adapter
  0x1AE010–0x1AE4AE
      ↓ call 0x1AE201
pre-launch handler
  0x1ADB10–0x1AE00D
      ↓ call 0x1ADD36
LocalLoaderStore::StartGame
  0x1EA180–0x1EBE7A
```

### 0.3.34

```text
startGame adapter
  0x1AFCD0–0x1B016E
      ↓ call 0x1AFEC1
pre-launch handler
  0x1AF7C0–0x1AFCCE
      ↓ call 0x1AF9F0
LocalLoaderStore::StartGame
  0x1EA7F0–0x1EC4EC
```

The `StartGame` call setup is almost instruction-for-instruction identical.

## Global singleton parity

The local-store singleton moved from:

- 0.3.29: `0x5F43F8`
- 0.3.34: `0x5F5440`

The coordinator-like singleton used for the security-sensitive pre-launch helpers moved from:

- 0.3.29: `0x5F4100`
- 0.3.34: `0x5F5100`

The exact concrete C++ type for the second global is not asserted because no direct class RTTI is emitted for the object itself.

## Pre-launch helper mapping

The wrapper structure is preserved one-for-one:

| Role | 0.3.29 | 0.3.34 | Result handling |
|---|---:|---:|---|
| local preparation | `0x1E8950` | `0x1E8FC0` | returns pointer/material used by next helper |
| protected helper A | `0x182810` | `0x186F30` | boolean; false aborts |
| protected helper B | `0x17F800` | `0x183F20` | boolean; false aborts |
| local boolean check | `0x13C750` | `0x13B0F0` | true skips helper C |
| protected helper C | `0x184690` | `0x189110` | boolean; false aborts |
| failure/result projection | `0x1844A0` | `0x188F20` | same relative post-failure role |

The call offsets relative to the pre-launch handler are also nearly unchanged.

## Data-flow parity

Both versions implement the same observable flow:

```text
local preparation
      ↓ result pointer
protected helper A
      ↓ success
protected helper B
      ↓ success
local boolean check
      ├─ true  → continue
      └─ false → protected helper C
                    ↓ success
LocalLoaderStore::StartGame
```

Protected helper B consumes the incoming start-game request plus multiple output buffers. Helper C receives an output/string buffer and a local result object. Exact semantic names for those buffers are not asserted without additional evidence.

## Protected-section comparison

0.3.29:

```text
section: .'*
RVA:     0x636000
size:    0x65D56C
entropy: 7.339
flags:   executable
```

0.3.34:

```text
section: .E"w
RVA:     0x638000
size:    0x62D2F8
entropy: 7.332
flags:   executable
```

Examples:

- 0.3.29 helper A enters protected target `0xC60023`; 0.3.34 counterpart enters `0xC31D42`.
- 0.3.29 helper B enters `0x83B591`; 0.3.34 counterpart enters `0x7F9547`.
- 0.3.29 helper C enters `0x7D46F4`; 0.3.34 counterpart enters `0x9C9DF7`.

The changed protected-section name/addresses are consistent with re-virtualization/repacking.

## Interpretation

0.3.34 did **not** introduce an additional top-level pre-launch authorization gate. The same wrapper topology, singleton roles, pass/fail sequence, and `StartGame` ABI are preserved from 0.3.29.

The protected internals were re-virtualized and may contain implementation changes, so this does not prove the checks are semantically identical. It does prove the high-level trust boundary is stable across these versions.

No bypass is claimed. No login, session, subscription, license, device identity, executable bytes, or protected runtime state was modified.

## R9-004 correction: local boolean branch is constant false

Follow-up decoding established that the structural `local boolean check` is a constant-false stub in both retained versions:

- 0.3.29 `0x13C750`: `xor al, al; ret`
- 0.3.34 `0x13B0F0`: `xor al, al; ret`

Therefore, although the wrapper contains a conditional branch that would skip helper C if the check returned true, the observed implementation never takes that skip through this function. Protected helper C is effectively mandatory before `LocalLoaderStore::StartGame` in both builds.
