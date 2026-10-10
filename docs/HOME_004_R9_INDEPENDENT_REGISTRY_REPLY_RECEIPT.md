# HOME-004 R9 — independent registry mutation acknowledgement across selection

2026-10-10. Target: original LWBridge 0.3.17; original EXE SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
R8 starting source `a1a7254b2248e2cefbb610890c054a9cc7dfcbde`,
branch `codex/home-complete-delivery-004`, existing draft PR #6.
**Whole Home remains PARTIAL_NEEDS_INPUT**; no lead acceptance or main merge.

## Investigated boundaries and original authority

Continued the H-01–H-47 queue instead of treating R8 as final. Inspected
original 0.3.17 `Yr` profile sidebar in the archived identified frontend
`index-BVfnK1wp.js` (source hash and locators in R8), including original
`r.reorder` callback, profile note editing, 3-second native status polling
and sequential batch Start/Stop. Inspected production `ProfileSidebar.jsx`,
`App.jsx` native profile snapshot/revision and retained owner callbacks,
`ProfileRegistryCommandService` / SQLite registry, and actual Windows
`LWBridgeWindow.cs` WebView request/reply generation gate. The original
`profile_note_set` validates exact profile identity before note in the
recovered native original contract (original function 0x1a4d26, H-39);
`profile_reorder` is a global profile-list mutation. Unlike a selected
Home status read, these mutations do not target the currently displayed
Home runtime. The original asynchronous reply ordering in this exact
interleaving is not independently witnessed; classify the correction as
a **controlled current-application consistency defect**, not a proven
original 0.3.17 simultaneous-client outcome.

## Before-fix native/WebView negative

`src/LWBridge.Desktop/LWBridgeWindow.HomeR4Proof.cs` exercises production
App + real native dispatcher with two inert retained owners. Initial A
running/selected; hold `profile_note_set` targeting explicit A before
native dispatch; select B through the actual sidebar, then release.
The SQLite registry **durably commits A's note**, but the old dispatcher
applies the selected-view generation fence *after committing* and reports
`PROFILE_GENERATION_RETIRED` to the real WebView request. The note's
successful persistence is reported as failure. This is not a protected
service, commercial profile or game launch.

Immutable local ignored before-fix failure:
`artifacts/home-004/r9-note-switch-inverse-en-light.json.error.txt`:
`A profile note was committed but its acknowledgement was retired by
unrelated B selection`, native code `PROFILE_GENERATION_RETIRED`.

## Cohesive correction and distinct inverses

Narrow native dispatcher change in `LWBridgeWindow.cs`: both explicit
registry mutation commands `profile_note_set` and global
`profile_reorder` now keep their completion replies independent of
selected Home view generation. The original guard remains for selected
`get_status`, Home/proxy and all other current-view commands; explicit
Home per-owner lifecycle, global update/restart and `profile_select`
existing paths remain unchanged. The frontend already revision-fences
out-of-order selected/profile metadata snapshots; do not relax this.

Actual final EN/light and JA/dark mounted tests:

- A's held native note write **persists to exact A** and replies success
  with currently selected B snapshot; backend stays B until explicit
  return to A.
- Global registry reorder held while A selected, then B selected, commits
  exact B,A order and returns B-selected success rather than a spurious
  retired error. The fixture **restores exact A,B order** before other
  Home tests and selects A with the actual UI.
- Existing R4 retained independent A/B native commands, R5 poll revisions,
  R6 reverse-priority error H-46 and global H-18 versus selected H-23 repair,
  R7 per-owner busy and R8 A Close after B/ABA preserve their assertions.
- Both inert owners finish stopped; no temporary roots, listeners or native
  requests survive; zero genuine game processes or Map scans started.

The first English after-fix run reached the existing R8 ABA test, where a
script checked native selected B before React had re-enabled the A selector.
The mounted harness now waits for B's **visible active row** and the
actual A enabled selection control, instead of removing assertions or
disabling the race check. Final EN/light and JA/dark both pass.
Final ignored receipts:
`artifacts/home-004/r9-registry-owner-final-en-light.json`,
`artifacts/home-004/r9-registry-owner-final-ja-dark.json`.

## Acceptance and missing inputs

This corrects H-39 and protects H-40/H-45 selected versus independent
profile operation replies. Source/inert Windows UI/native evidence is
bounded and does **not** establish actual licensed multi-instance
operation. Reassess alongside the 47-row queue:

- H-05/H-13/H-22/H-28/H-38: authorized successful protected original
  ticket/lease/finalizer/entitlement/capacity outcome absent.
- H-06–09/H-18–20/H-44/H-46: typed original/current genuine official
  launcher restart, update/Lua, descriptor/mutex/error producers absent.
- H-24/H-29/H-37–40: genuine independent supported installations,
  original 0.3.17 encrypted transport timing and multi-owner admission
  unavailable. Current adapter 30 s reader timer remains 0.3.1-derived.
- H-01–04/H-10/H-12/H-14–17/H-23/H-25/H-30–36/H-40–47:
  original/current paired fault, offline-only recovery, pending Stop,
  repeated maintenance/failed repair/rollback, conditional original EN/JA
  pixels and other explicitly row-listed adverse witnesses still missing.

Existing bounded genuine single-owner Launch/Connected/Close/adoption,
unexpected-exit and hang recovery positives remain intact. Legitimate
alternatives were original archived frontend/native sources, real native
WebView/SQLite command inverses with two **inert** profile owners,
recovered original helper tables, previous genuine bounded receipts,
actual Windows pipe and packaged source identity. They do not supply
protected decryption keys/responses, authentic simultaneous-client support
or adversarial original-side outcomes. No access bypass, spoofed entitlement,
Map expansion, dangerous updater or real-game experiments were performed.

Final source SHA, direct origin, ZIP/EXE hashes, extracted Home smoke,
normal GUI PID and exact cleanup are recorded in generated ignored
`artifacts/home-004/r9-final-package-receipt.json` after delivery.
