# Owner Launch failure — stale recovery record, 2026-10-15

Owner reproduced Launch failure in the extracted e6cafd52 Map RC. The normal
profile's actual helper and host evidence show that session cea17d7c launched
and authenticated a real game, then stopped it during startup rollback roughly
two seconds after host-start. The normal runtime still contained a valid adoption
record for a different October 13 session whose captured game had ended.

Root cause: CommitAdoptionRecord's exclusive write accepted only the new session
and challenge. It refused the dead previous session, so the successful new game
could not commit its recovery record and was stopped. Earlier clean isolated
proofs lacked this persisted owner state and missed the defect.

## Correction

- Preserve the exclusive OS file transaction and current-owner nonce check.
- Permit a different old session only if its DPAPI identity is valid, profile
  and normalized executable match, and a query/synchronize-only process handle
  proves exit, PID absence or a different parsed UTC creation instant.
- Live, inaccessible, unknown, malformed, foreign-profile/install and same-session
  wrong-nonce records remain untouched. No process is killed by this classifier.
- Add specific localized recovery-record-save failure feedback in all nine app
  languages, retaining the recovered catalogs and generic unknown-error policy.

Actual production lifecycle tests cover stale-record Start/commit/Stop, exact
rollback preserving protected records, PID reuse versus equivalent timestamp
formatting, query errors and a racing file replacement. Full Release/inert and
frontend suites pass. The initial test lock exception was corrected for Windows
UnauthorizedAccessException without changing production ownership protection.
One initial frontend run hit a transient missing dependency; the unchanged
dependency tree passed the complete rerun. Raw logs remain under artifacts.

## Selected-profile native path defect

A fresh normal-data WebView trial `normal-20261015-034154` independently failed
with `launch report registration not confirmed before the bridge-connect
deadline`. The configured primary ID and selected controller profile differ.
Actual selected-profile composition passed sibling managed runtime/evidence/
backup directories, but Python and injected Lua derive them below the profile's
`current-client` application root. Registration/control/heartbeat therefore did
not share a path. The window factory now lets the existing lifecycle defaults
derive those paths from the same root as Python/Lua. Explicit custom test API
overrides remain supported; no old files are deleted or migrated blindly.

Earlier deep-artifact copy trials encountered an independent Windows path-length
limit before readiness; those are failed diagnostics, not successful fixes or
proof that the normal owner path has the same length problem. Normal-data
trials temporarily disable and restore the exact saved Auto preference, require
zero armed jobs, preserve original scripts and record actual native errors.

## Final normal-profile delivery — verified from the extracted review ZIP

**Product source commit:** `4666a8bfdec239efdcee0598eb6646df469cfdf2`.
This product-source SHA remains embedded in the executable even if a later
documentation-only commit moves draft PR #10's head. Do not rebuild only to
change documentation. Both corrected Home launch paths and the localized
save-failure feedback were included in the one previously validated Release
publish at `artifacts/map014-takeover/publish/` (not rebuilt here).

The originally tested normal persisted profile proof is
`artifacts/home-launch-owner-repro/normal-20261015-035236/`: actual
Home Launch to independently authenticated stable Connected (15 samples,
15,220 ms), Home Close, exit code 0, unchanged Auto preference, restored v26
scripts, no new Map scans/gameplay. This is not isolated dummy-provider evidence.

The exact product publish was packaged with the established source-identity
and private-file allowlist script, without a second build:

- **Canonical ZIP:** `artifacts/release-map014-4666a8bf/LW-Control-MAP014-OWNER-RC-4666a8bfdec2.zip`
- **Size:** 10,861,804 bytes
- **SHA-256:** `1cbb9c8404350bd14dc521acd179325c1c3ba7d3fdf4dd553870f238d55abbcf`
- **Source and EXE ProductVersion:** `4666a8bfdec239efdcee0598eb6646df469cfdf2` and
  `1.0.0+4666a8bfdec239efdcee0598eb6646df469cfdf2`
- **ZIP/extraction:** exactly 81 allowlisted Windows payload members, full
  ZIP CRC, per-member SHA-256, extracted byte-for-byte identity, production UI
  identity and private-file exclusion all **PASS**; `published:false`.
  Windows-only binaries retained; debug, non-Windows runtime and generated
  Python cache artifacts excluded. Manifest at
  `artifacts/release-map014-4666a8bf/release-integrity.json`.
- **Fresh extraction:** `artifacts/map014-takeover/extracted-review-4666a8bf/`.
  Its `LWBridge.Desktop.exe` SHA and product version match this ZIP exactly.

**Genuine extracted-executable normal-profile replay:**
`artifacts/home-launch-owner-repro/normal-20261015-055825/`.
The documented `run_normal.py` used that extracted executable, the actual
normal persisted owner-data root and task-owned WebView controls, with no
mouse/global keys/focus operations or fabricated native provider.
`ui-result.json` records actual Launch, authenticated
`bridgeConnected:true` / `connectionState:connected` /
`identityConfirmed:true`, game PID 35328, exact instance
`7bbda6ffa0594c62b0f4c0324f99c2c2`, **15 Connected samples over
15,217 ms**, `nativeErrors:[]`, and actual Home Close `homeStop:true`.
`final.json` records `pass:true`, host exit code 0, all three official
v26 scripts restored, and saved Auto preference restored **byte-for-byte**.
The selected owner's persisted Auto `config.enabled=true` is intentionally
unchanged; its restart/active state is **false**. Post-proof inspection found
no LastWar, launcher or LWBridge.Desktop processes and no selected-profile
pending recovery/adoption file. No new Map scans or account-changing actions.
Actual Home before/Connected/stopped screenshots and trace remain alongside
the original source and post-extraction receipts.

**Delivery verdict:** the corrected Home Launch → stable authenticated
Connected → Home Close **passes from the new exact ZIP extraction**.
The previous `e6cafd52` extracted-owner Launch failure, stale-record and
secondary path negative trials remain preserved as history, not erased or
relabeled. This acceptance covers the bounded Home correction only. The whole
Map remains **PARTIAL** with its existing Ghost/Treasure provider, natural
population, live owner-only action and fresh positive Truck-witness
dependencies. Keep `codex/map-owner-fixes-014` / draft PR #10 unmerged,
unpublished and within the existing background-only restrictions.
