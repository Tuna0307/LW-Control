# MAP-010 independent lead review

Reviewed worker HEAD `b8e5301655968aff54895115b6713d1ed7b561ab` on
`codex/map-action-providers-007`; direct remote matches; initial tree clean.

**Decision: CHANGES_REQUIRED for saved-server ownership. Accept the narrowly
verified localization/native-name correction. Scanner optimization remains
PARTIAL, with no new qualified route or measured improvement.** No merge/release.

## Independently executed

- Canonical frontend five check groups PASS; nine effective catalogs 1,394 keys.
- Mounted native names PASS: 40 controlled calls, five groups including all nine
  languages' missing-root/loading/error/retry states and obsolete name replies.
- Mounted lifetime PASS: eight controlled cases.
- MAP-009/010 offline tests PASS: 12 tests; models are not native witnesses.
- Desktop default Release suite PASS with `SkipCanonicalProductionUiBuild=true`:
  native Home/Map/Auto/ownership/store/installed-text tests, zero game launches.
- Read raw R10 receipt and the exact ignored experimental Lua state machine.
  Second scene tile 545,495, request call/reply flags and generic network activity
  were present, but requested target membership was false; only first segment
  was captured. No successful second-target/last-only restore proof exists.
- Independently viewed actual EN/light Resource and JA/dark Monster captures.
  Real row names render (Gold Mine/Iron Mine and localized Monster labels),
  matching the corresponding stored/native-locale receipts. No new capture/live
  action was needed or performed in this review.
- Final ZIP CRC, SHA and source identity PASS: 9,590,618 bytes, 83 archive entries,
  SHA `27DB54AA59B37D79AF958320BF45199778F78BFF6FA5B8EAED240CC581B5FB0D`,
  embedded source `9f3f6db99bcc2f38d8af6e827f2eaccbd21135a6`.
  Extracted ProductionUi source/artifact checker PASS against current source.
  Later worker commits contain no product delta from that package source.
- Local default UI `dist` is stale and fails its source identity check. This is
  a build-workspace issue, not a false ZIP identity: the extracted package passes.
  Regenerate/check canonical output when producing the corrected candidate.
- Canonical scanner Lua blob equals task start
  `cd190f4294cdd9d4131180851a1164b967025bb4`; no new acquisition enabled.

## LEADMAP010-01 — saved server list does not follow actual profile owner

Reproduced using mounted actual MapRoutePage/MapDataPage with controlled providers:
stopped profile A has saved server 2212; stopped B has only 2213. Use the same
mapApi object while changing owner, matching App's module-scoped createMapApi.
The component calls savedServers only for A and keeps selector 2212 after B.
The supplied scan state stays server=0, so the effect dependencies do not change.
App's selected-profile owner changes; its mapApi identity does not.

`MapDataPage.jsx` savedServers effect depends on backendAvailable/mapApi/serverId,
not the actual profile owner. Clearing selection only after a successful reply
also leaves old-owner selection/options available during pending/failing reads.
This does not prove cross-profile DB leakage: native queries remain scoped.
It does prove wrong/missing saved-server browsing in the selected profile.

Compact reproduction is `tools/map_010_background/check_saved_profile_owner.mjs`;
immutable negative observation is `docs/proof/MAP-010-LEAD-SAVED-OWNER-NEGATIVE.json`.
Correct actual owner-generation invalidation, immediate state/cache clearing,
same-object profile changes, late replies, errors, empty stores and A/B/A.
Do not recreate the entire canonical API merely to hide this local dependency.

## LEADMAP010-02 — native receiver diagnosis is incomplete

The R10 diagnostic fails immediately when the first flags-ready snapshot does not
contain the target AOI, or generic network counters fail its check. It does not
demonstrate which native response/segment generated those flags, or whether an
unrelated/old response preceded a later fresh target response inside the deadline.
This is a source-observed limitation, not proof a delayed target actually arrived.

Keep these negative attempts immutable and the shortcut disabled. Recover the
actual native receive/window/segment/current-view transitions. For a new candidate,
ignore demonstrably unrelated/stale segments while waiting within a bounded
deadline; accept only complete target-owned fresh data. Never relax coverage to
accept flags or unscoped packet counts. Do not repeat R9/R10 unchanged.

## Answers and remaining work

- No MAP-010 per-kind, mixed or remote full-world speedup was qualified. Old
  approximate 3.5-minute mixed timings are history, not new benchmark results.
- Mixed types retain one shared spatial sweep, not a complete sweep per type.
- Camera-assisted acquisition and intermediate home return remain in production.
- Complete remote populations still use passive server-context switch/return.
  Known-target explicit-server response trials remain unverified, not impossible.
- Sampled native field checks support accuracy; no independent exhaustive census
  establishes perfect whole-world recall or globally fastest acquisition.
- Ghost/full seasonal acceptance and owner-only actions remain separate.

Continue the focused review correction and source-led native diagnosis in
`docs/work-items/LWB317-MAP-ACQUISITION-RECEIVER-011.md`. Same branch/PR; current
background-only/no-mouse and non-account-changing authority stays unchanged.
