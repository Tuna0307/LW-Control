# HOME-004 R7 — independent sidebar owner busy indicators

Date: 2026-10-10. Previous verified source/origin:
`f5dc452cc84dcdc5d594e470fe8769ddf9bc3049`.
Target: original LWBridge 0.3.17 Home H-38/H-45/H-47 retained
profile lifecycle. Whole Home **PARTIAL_NEEDS_INPUT**; draft PR #6.

## Actual production UI inverse, not a guessed original service response

R6 proved that native actions of different retained owners A/B must be
independent while the same owner's Home/sidebar commands are serialized.
A follow-up R7 inspection found `ProfileSidebar.jsx` stored one
`runBusyId` even though `profileActionsRef` already tracked independent
profile operations. The completion of B could clear A's busy ID while A's
native Start was still held. This left A's sidebar button enabled despite
the command already pending; the native exact-owner admission still fenced
duplicate Start, but that did not repair the visible UI state.

Distinguishing actual production WebView + native dispatch sequence:

1. Start A from the expanded sidebar, holding its actual native
   `profile_instance_start` command before producer dispatch.
2. Independently Start B and Stop B in the same sidebar.
3. Require B to be stopped and its UI action complete, with **A's
   Start button still disabled** until its pending command is released.
4. Attempt Home A Start while sidebar A remains held; it must not
   dispatch another A Start. Release A, check exactly one native A Start,
   Close A, and confirm both owners and all listener/root resources retire.

Before-fix actual mounted EN/light negative (preserved in ignored artifacts):
`artifacts/home-004/r7-busy-indicator-inverse-en-light.json.error.txt`.
Exact failure:

```text
System.IO.InvalidDataException: Completed B sidebar action cleared still-pending A sidebar busy indicator.
```

## Narrow repair and passing evidence

`src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx` now represents pending
run buttons as a Set of exact profile IDs (`runBusyIds`). The existing
`beginProfileAction` adds only its owner and `endProfileAction` removes
only that owner; neither B success nor failure can prematurely enable A.
Action and per-profile polling revision fences from R5 remain intact.
`scripts/check-home-integration.mjs` also guards the source-level
per-owner disabled-button binding.

After-fix mounted EN/light and JA/dark production-WebView runs:

- `artifacts/home-004/r7-busy-indicator-corrected-en-light.json` — PASS.
- `artifacts/home-004/r7-busy-indicator-corrected-ja-dark.json` — PASS.

The same extended R4-through-R7 mounted fixture still checks R6
bidirectional Home/sidebar A deduplication, independent B lifecycle,
original manual global repair-error behavior, exact Stop/cleanup,
selected-view ownership and native registry admission.
Canonical frontend checks and Release native Desktop checks pass.

This is a **current rebuilt application** defect reproduced through the
production UI/native boundary, not a claim that protected original 0.3.17
commercial multi-profile capacity is now observed. R7 fixtures launched
zero genuine Last War games and no Map scans. Previous genuine
single-profile positives and the R6 original error oracle remain unchanged.

## Whole original Home input gates

The full 47-row [queue](HOME_004_R4_QUEUE.md) remains authoritative.
Authentic successful/failing original protected ticket/lease/finalizer
responses (H-05/13/28), correctly signed original controller authority
(H-33/36), official launcher restart/update/error producers (H-06–09/
18–20/44/46), legitimate licensed simultaneous genuine-game installations
(H-22/24/38–40), and paired original/current adverse offline-only,
maintenance, pending Stop, rollback and conditional EN/JA states
(H-34–36/40–47) are still unavailable or unproved. No fabricated
entitlements, unsafe live fault or protected service access is authorized.
Continue only genuinely ready source-backed corrections. **Whole Home
PARTIAL_NEEDS_INPUT**, not accepted for main merge or publication.
