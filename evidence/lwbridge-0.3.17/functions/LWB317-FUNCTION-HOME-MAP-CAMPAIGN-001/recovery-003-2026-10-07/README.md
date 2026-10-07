# RECOVERY-003 worker closeout — 2026-10-07

Status: **AWAITING_REVIEW**. Project-lead acceptance remains separate. This packet
closes only R3-01 through R3-05 in the assigned offline/inert scope; it does not
promote LIVE_PROVEN, protected-provider, whole-clone or live-game parity.

Starting checkpoint: `138ea26469b7a35c0758198c98f8c44f352037b0`.
Final implementation/proof checkpoint: `d90f764aa2a721e6bf26952a81fb1a6161bec0d4`.

## Corrected findings

- **R3-01** — shared Home runtime writes/refresh/cancellation publication now require
  exact owner identity on an exclusive handle; foreign identities fail closed.
- **R3-02** — conditional runtime-file deletion validates and disposes the exact file
  on the same protected handle, closing the validate-then-path-delete replacement race.
- **R3-03** — once local Stop commits, exact provider terminalization is independent
  of the retiring caller token and lease/run ownership is retained until termination.
- **R3-04** — confirmed global Auto Launch intent is distinct from native per-profile
  launch gates for rollback, including divergent/inverse/concurrent save cases.
- **R3-05** — Auto Scan Add/remove/type operations rebase over hydrated settings,
  preserving saved entries, ordering/deduplication, immediate editability and
  last-type protection while retaining existing save/profile fences.

Implementation checkpoints:
`c4f71437` (R3-01/02/03), `f50f04d5` (R3-04/05), and
`a03de1d5` (mounted production-handler/native-boundary proof).
Package-harness corrections needed to integrate the new inverse cases without
changing production ownership semantics are `0199fba2`, `8b578f0d`,
`453c9440` and `d90f764a`.

## Corrected mounted package evidence

Accepted worker packets are new files; no historical negative was repinned or
overwritten:

- `package-en-light-attempt5.json/.png` plus
  `package-en-light-attempt5-home-rollback.png`,
  `package-en-light-attempt5-auto-rebase.png` and the matching XLSX export.
- `package-ja-dark-narrow-attempt1.json/.png` plus matching rollback/rebase
  screenshots and XLSX export.

Both packets are `state=proven`, `externalGameActions=0`, use the canonical
packaged `LWBridge.UI-0.3.17` in native bridge mode, and bind to managed product
version `1.0.0+d90f764aa2a721e6bf26952a81fb1a6161bec0d4`.
Package hashes are identical in both runs:

- apphost SHA-256:
  `8613040D327D2F517D9F0BE0962285D7580E05D691FB57AE8450F97F02BE71CF`
- managed DLL SHA-256:
  `B26211DA46305C54EFD57CD9CB8D6B2038BFF0D12F01373ACB9BD4F6CEC22803`
- UI source fingerprint:
  `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685`
- UI artifact fingerprint:
  `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`

EN/light measured 1120x720. JA/dark narrow measured 900x720. Both retain the
expected pre-reload and queued prior-document issue sentinels while recording
`browserIssues.unexpectedCount=0`. Both close with zero active requests and
subscriptions, detached profile runtime events, removed isolated root and no
cleanup failures.

The successful packets directly record the distinguishing R3-04 divergent,
inverse and concurrent rollback states and R3-05 hydration/rebase snapshots.
They also record delayed A/B/A Map ownership by exact profile generation and
cancellation observation after owner retirement.

## Preserved new negative package history

The lead's five original `lead-review-2026-10-07` negatives are unchanged.
RECOVERY-003 also preserves four new EN package failures encountered while
integrating the extended mounted proof:

1. attempt 1 — stale proof expected the delayed request to use an older A
   generation captured before intentional A/B/A swaps;
2. attempt 2 — stale proof still expected profile B native Auto Launch=true after
   the new inverse scenario had deliberately confirmed B=false;
3. attempt 3 — the same obsolete B=true assumption remained in the reload assertion;
4. attempt 4 — the proof toggled the real theme control twice before Chromium's
   first View Transition settled, producing an unhandled rejected ready promise.

Each failure remains in its own `.error.txt` and any artifacts already emitted.
No failed record was deleted or rewritten. The fixes only align/serialize the proof
with state created through real packaged controls; they do not weaken R3 production
fences or suppress browser issue accounting.

## Verification and cleanup

See `checks.md` and `checkpoint-c/` for exact outputs. Final verification includes:

- canonical frontend `check`, `build` and `check:production-build`: PASS;
- canonical Release Desktop build/package: PASS, 0 warnings/errors;
- final Desktop.Checks Release build: PASS, 0 warnings/errors;
- `--profile-runtime-owner-check`: PASS;
- `--map317-native-boundary-check`: PASS;
- `--home-campaign-lifecycle-check`: PASS;
- `--map-auto-scan-campaign-check`: PASS;
- `--map-campaign-canonical-check`: PASS;
- `--map317-restart-check`: PASS;
- `--game-root-select-check`: PASS;
- reference executable SHA-256: exact expected match;
- legacy archive guard: PASS, `exactFiles=10`;
- final process inventory: no LastWar/LWBridge process;
- failed-attempt task temp roots: 12 found under the exact
  `lwb317-home-map-campaign-*` prefix, all removed; post-cleanup count 0;
- `git diff --check`: PASS before final staging.

## Remaining bounded limits

Treasure claim/status and Ghost plunder preparation remain unavailable
protected/current-client provider actions. Positive current Railway/Ghost/Treasure
population remains live-state gated. No live Last War launch/stop/scan/jump/claim/
share, updater, protected-service access, account or installation mutation was
authorized or executed. Exact current-game Lua engine/binding remains outside this
offline proof; Lua 5.3/5.4/5.5 isolated consumers pass and the Lua 5.1 bulk-AOI
baseline incompatibility is historical rather than introduced here.

Exact continuation is project-lead review of this checkpoint chain and corrected
packets. Do not continue worker implementation unless review returns a concrete
finding. Any protected/live proof requires a separately authorized task.
