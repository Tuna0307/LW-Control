# LWB317-MAP-OWNER-FIXES-014

Owner assignment, 2026-10-13. Status: PARTIAL implementation / functional review pending. One solo worker; new branch
`codex/map-owner-fixes-014` from current main. PR #9 is merged and retired.

Active source changes, the compact 13-obligation matrix, controls tested and
exact remaining compatibility/native witness dependencies are recorded in
[`MAP_014_OWNER_HANDOFF.md`](../MAP_014_OWNER_HANDOFF.md). The installed package
hash currently fails the preserved native admission gate; no live success may be
inferred from controlled tests or older scanner totals.
Released product source is `6f93c109436ebb80f4f344ad25380151b4f9aba5`;
pre-assignment main checkpoint is `78e9ea0fab60644075bbb7d9154ce320cc49ed70`.
Preserve accepted single-game Home, published data and historical negative trials.

## Owner-reported defects and requested improvements

Screenshots are observations, not instructions. First game image shows a severely
distorted world view after returning home. City-only completed scan shows 4,076
total Cities and an alliance filter showing 94 results; historical 6–7k counts
are comparison clues, not a fixed population oracle. Resource names now appear,
but filters are insufficient. Truck icons are blank, item filtering is single
selection, follow is unusable. Dispatch owner displays a numeric identity.
Cross-server warzone choices flicker. A row saved from a remote Auto scan jumps
within the returned home server instead of the row's source server.

Source images are the nine owner clipboard files ending e5c0e680, 69344dac,
299db841, bee3ddd0, 70841337, 9734caf5, ba303a1b, 9c51a2c8 and 3e854537,
under C:/Users/chimw/AppData/Local/Temp. Exact paths are in the owner's relay.

## Execution authority

Work solo, no subagents, other AI chats, GPT Work or Codex delegation. One feature
branch/checkout/PR. No worker implementation on main, merge or publication.
Use REA/current installed game sources where native semantics are necessary.
Background-only: no global mouse/keyboard, focus forcing, minimizing/rearranging
owner windows or game-language changes. Chinese official-game setting remains.
Exact task-owned app/game startup, bounded passive scans, read-only native
inspection, passive follow/navigation and server-view switches/return are allowed
under current-build admission, isolation, ownership and restoration gates.
An existing owner process is not automatically a task-owned process: do not stop
or mutate its installation/session. Read-only supplied logs/database snapshots
and source/inert tests can proceed while it runs. Use an isolated owned session
for invasive fault/restore trials once it is safely available.
No claims, plunder, sharing, attacks, rally/march initiation, spending, relocation,
updater or protected-original account/service access. Following a moving map
object means passive camera tracking only, never dispatching a squad.
Never retry/reroute an actually denied operation. Keep ready independent work
moving and record the exact denial scope without inventing blanket prohibitions.

## A — scan integrity and exact visual restoration (first priority)

1. Reproduce the distorted home-return view. Capture before/target/after native
   camera transforms, projection/aspect/FOV/zoom, anchor/viewport/subscription
   footprint and game scene/server/world using supported accessors. Check both
   successful completion and Stop/error/timeout/server return. Do not assume
   speed or coordinate offset caused it. Restore the complete captured visual
   state in the right native lifecycle; matching a tile alone is insufficient.
   No guessed defaults or cosmetic refresh counted as root-cause repair.
2. Investigate City undercount end-to-end: genuine request/segment/cache/row
   acquisition, all chunks, city subtypes, point filters, overlap dedupe, world/
   server identity, staging/publication and displayed summary/query counts.
   Reconcile 4,076 summary separately from the selected alliance's 94 results.
   Compare City-only and mixed capture in comparable windows and independently
   populated regional controls. Retain missing-city identities and source fields
   where observed. AOI geometry/100% progress alone cannot prove object recall.
   Historical 7k rows must never be inserted or treated as an expected quota.
3. Distinguishing inverse cases must catch stale/native-wrong-region replies,
   points arriving before cache insertion, missing/extra/duplicate rows, cancelled
   batches, obsolete profiles and wrong servers/worlds. Use the MAP-013 findings
   and private guard as evidence; do not blindly promote its count-only guard.
   Keep experimental camera-free/consecutive shortcuts disabled unless independently
   qualified. Accuracy and restoration precede any speed work in this assignment.

## B — identity, cross-server routing and moving targets

4. Reproduce the header popover flicker. Trace summary/status/warzone producer
   changes, polling, object identity/remounting and overlapping request ownership.
   Stable legitimate options must remain clickable while routine polls settle;
   retire old-profile/session replies. Do not freeze genuinely changed lists or
   fabricate connection stability. Cover idle, Auto, return and refresh states.
5. Trace remote saved row identity through UI -> native command -> official
   navigation -> reached server/world/target. After an Auto 2213 -> 2212 return,
   clicking a 2213 row must reach 2213's target; never use current header server
   as the row's origin. Fail clearly if that source context cannot be reached.
   Test same x/y on two servers, stale/expired row, overlapping jumps, profile
   replacement, failed switch and coordinate versus march navigation.
6. Recover and implement supported passive Truck following/track-lock by stable
   native march identity, not repeated jumps to a stale scan position. Inspect
   whether supported RunningMonster/Doom Walker identities have an equivalent
   moving-target path. Include Follow/Stop-follow lifecycle, disappearance/arrival,
   server change, Start/Stop scan, owner retirement and teardown. Do not fabricate
   live motion, guess identity or initiate any gameplay march. Unsupported
   tracking must be explicitly explained rather than silently no-op.
7. Secret Task owner: recover real owner name/alliance from supported native
   task/player metadata. Preserve full UID as identity, avoid numeric precision
   loss and foreign/stale responses. Missing names may show an explicitly labelled
   UID fallback, not pretend the UID is a player name or invent a name.

## C — requested filters, item assets and Auto clear

8. Resource filters: level selection/range; full / partially depleted / unknown;
   black tile / non-black tile / unknown, with All defaults. Recover terrain and
   amount semantics from current native data. Idle is not necessarily full;
   Gathering is not necessarily depleted. Unknown must never become false/full.
   Apply filters in canonical backend queries BEFORE totals/pagination, support
   combinations and reload/profile/tab ownership. Reuse genuinely supported
   data fields where available; distinguish the active Map317 store from the
   older MapDataStore rather than fixing an unused query implementation.
9. Truck escort-power upper bound: input 25 means <=25,000,000 inclusive.
   Blank means no bound; accept sensible decimal million values, reject invalid/
   negative/overflow input. Use source-correct stored units; unknown power must
   not silently qualify as zero. Apply before totals/pagination.
10. Truck reward item multi-selection: default match ALL selected item identities
    (e.g. 100-diamond item AND 5-minute speedup). Make semantics clear and support
    removal/clear. Distinguish an item's denomination from its stack count; do
    not invent a quantity threshold. If optional Any mode is added, label it.
    Use canonical IDs, dedupe and bind SQL parameters. Preserve old one-item
    callers and filters/sorts across navigation under the intended lifetime.
11. Fix missing Truck item diagrams/icons through installed-game asset/config
    mapping and the supported native asset reader. Prove real IDs -> image bytes
    -> rendered icons from the extracted package. No guessed icons, fixture-only
    success or bundled private game assets. Item tooltips/names and quantities
    must remain correct in Chinese game context and the clone's locales.
12. Add Clear Map Data to Auto mode, using the same actual native clear command,
    error/acknowledgement and successful reset as Manual. Prevent clearing while
    an owned scan/cycle is running or starting, with backend enforcement as well
    as UI disabling. Clearing must not disable settings or delete another
    profile. Preserve known state-retention semantics and propagate failure.

## D — additional native map objects

13. Investigate Scattered Supplies and Celebration Party Watch using installed
    Chinese/English localization, configs, actual point/event classes and native
    world sources. Map exact supported names/IDs/types, availability, stationary/
    moving character and acquisition fields. If genuine map objects are present,
    implement a clearly scoped read-only discovery/display path within the
    existing shared scan where justified. Do not count rewards/buttons/UI labels
    as map objects or force them into an unrelated scanner type. If not currently
    available, give a source-backed disposition and the exact positive-witness
    dependency. No collection/party interaction/claim action.

## Verification and delivery

Diagnose -> fix -> distinguishing retest -> self-review -> coherent checkpoint ->
next ready obligation. Do not stop after ordinary failures, tool setup, a passing
general suite, or a single screenshot. Reuse the working pipeline; no repeated
identical negative trials or fresh ZIP per small fix. Maintain one compact table
of all 13 items with baseline, source authority, correction, proof and limits.

Use actual production handlers/store/planner/engine and mounted packaged controls.
Include before/after inverse cases and live/native positives for applicable
restoration, acquisition, remote navigation and supported passive tracking.
Genuine counts must be observed, never seeded fixtures or assumed historical
numbers. Exercise independent and mixed selections; all filters together,
pagination/export where applicable, held requests, offline transitions, Stop and
profile ownership. Keep independent census limitations explicit.

Run canonical frontend check, affected mounted lifetime/name/saved-owner tests,
production build/package check, Desktop Release suite, affected native query/
pipe/Auto/scan checks, 255 selection/adversity regression and meaningful newly
added cases. Inspect actual EN/light, JA/dark and narrow changed surfaces; assert
language/theme rather than filename labels. Preserve official game Chinese.

Build one final Windows candidate from clean committed source. Verify ZIP CRC,
EXE/embedded source, extracted/published byte equality, production UI identity,
no private database/profile/game assets/diagnostic tooling, actual extracted
startup/exit and no new game processes for inert smoke. Live cleanup must prove
exact owned exit, original script hashes and no owned journals/armed jobs.
Do not delete historical negatives or repeat denied deletion of the old test
folder `%TEMP%/lwb317-normal-ui-disabled-94wwxe81`.

Push coherent checkpoints normally, verify direct remote SHA, update this table,
handoff and behavior-differences ledger, then open one PR against main. The worker
does not merge or publish. Return READY_FOR_LEAD_FUNCTIONAL_REVIEW only for verified
items, or PARTIAL with exact unresolved functional dependencies and other ready
items finished. No global fastest, exact original parity or whole-Map claim.
