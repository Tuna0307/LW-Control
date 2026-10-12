# MAP-010 — current background-acquisition continuation (2026-10-12)

**Latest independent lead decision:** MAP-011 source `0c312c75` reviewed;
LEADMAP010-01 owner correction accepted after original inverse and affected
regressions pass. R11/R12 negative/limited observations verified, no faster route
accepted. See `reviews/2026-10-12-MAP-011-LEAD-REVIEW.md`. Owner requests status
only/no new prompt; no new assignment, merge or publication issued in this review.

## MAP-011 continuation — PARTIAL, 2026-10-12

**LEADMAP010-01 fixed.** The immutable lead inverse is preserved. Actual
App-selected profile ID and generation now key the mounted Map route even
when App retains the same mapApi object. Replacement owners remount with
empty options, rows, selection, and callbacks, then read only their private
published positive server IDs. The selected Map kind survives a change of
profile via route state. No disconnected server=0 is represented as a live
connection. Explicit mounted A/B stopped 2212-to-2213, A/B/A, same-server
A/B, held options/search, late saved-server success/rejection, empty and
failed reads, and offline/online/offline transitions pass. The prior
native-name mounted lifetime and all nine clone catalogs remain supported.
No previously published Map dataset was modified.

**LEADMAP010-02 receiver R11 rejected after its full deadline.** Source
v24 WorldPointManager.HandleViewPointsReply managed token 0x06003551,
UpdateViewRequest token 0x06003565 and StartViewRequest token 0x06003568.
The task-private receiver samples native current/add/message AOI indices,
split state, and reply flags until the full 8-second deadline; unlike R10,
it does not fail at the first flags-ready but wrong-target snapshot.
Server 2212 first region (455,425) returned 30 native AOIs, 28 real
Resource rows, 3 Train rows and 27 Monster rows. The second actual
WorldScene camera tile settled at (545,495), but the native current and
add sets each retained nine AOIs that never contained that second target,
the message set remained zero, and split was false. There were 455
flags-ready pump observations without target membership and nine compact
timeline samples spanning ~34ms to ~7289ms. After ~8879ms total the
request failed as map011_receiver_deadline_no_target_owned_complete_aoi.
No second segment, final-only home restore or production speed shortcut
was accepted. Native callback/segment-to-request correlation still
requires explicit receive-side proof; helper IDs and generic traffic
cannot substitute for that authority.

**Separate known remote point R12: not qualified.** An actual archived
positive City point on server 2213 with point type 6 and world 0 supplied
one known native WorldGetDetail request while the owned session remained
on server 2212. The source-built message carries point ID/server/world.
After ~340ms, a detail with the correct point ID appeared with empty
pre-request cache, unchanged active server 2212 and unchanged camera tile.
The parsed detail reported server 0, however, not the requested 2213.
That is insufficient to establish authoritative remote lookup; it also
does not demonstrate discovery of unknown remote objects or whole-map
remote acquisition. It was rejected, without other-object enumeration.

**Safety, release scope, and next step.** R11 and R12 each used fresh
isolated task profiles; the original v24 installed scripts were hash
verified and preserved, each exact task-owned session was stopped, no
task game/app or recovery journal remained, Auto Launch/Reconnect were
disabled and zero account actions occurred. Experimental Lua/C# and
private target identity remain only in ignored artifacts/map-011.
All temporary CLI/driver source and built runtime Lua were restored
to the committed production state. Home, Auto Stop-before-return,
native names, completed datasets, shared mixed acquisition, and the
serial camera-assisted correct scanner are unchanged. No new
individual/mixed/two-server alternating speed benchmark was eligible;
prior ~207-211 second all-eight accuracy-qualified baseline remains.
To proceed: source-attribute HandleViewPointsReply target/segment and
manager view generation, establish two complete actual native target
footprints plus one exact final restoration, then benchmark. Remote
lookup separately requires a positive authoritative server/world echo.
At final release validation, an additional LastWar.exe PID 40460
appeared at 13:53:11 local; it was neither R11 owned game PID 1120
nor R12 owned game PID 23308. No current task recovery journal owned
that process, and it was **left running untouched**. Therefore zero
remaining *task-owned* processes does not mean zero game processes
on the owner's computer.
Ghost seasonal/positive evidence and account-changing actions remain
owner-deferred. Compact proof in
docs/proof/MAP-011-RECEIVER-RESULTS-2026-10-12.json. Independent lead
acceptance remains required. No merge or publication.

---

**Independent lead update:** worker `b8e53016` reviewed. Native name/feedback
correction verified; no faster/camera-free/remote full-world route accepted.
LEADMAP010-01 reproduces stale saved-server selection on same-API profile switch;
LEADMAP010-02 requires native response provenance beyond the first flags-ready
snapshot. See `reviews/2026-10-12-MAP-010-LEAD-REVIEW.md` and continue focused
`work-items/LWB317-MAP-ACQUISITION-RECEIVER-011.md`. Background authority unchanged.
The worker account below remains historical; no lead merge/publication.

**PARTIAL.** Continue on existing branch codex/map-action-providers-007, PR #9,
from lead checkpoint 8e2c51df54c9147c9c2e1b9135464cdb48266386.
The lead-accepted limited MAP-009 offline inventory/CPU grouping, MAP-008
accuracy safeguards, completed data, native names, Home and Auto
Stop-before-return were preserved. This is the single current MAP-010
continuation; independent lead acceptance remains separate.
Compact evidence: docs/proof/MAP-010-BACKGROUND-RESULTS-2026-10-12.json.
Raw native JSON, screenshots, task backups and failed-run receipts remain
private under ignored artifacts/map-010/native-names-pilot; nothing is bundled.

## A — owned observability and genuine native evidence

Installed game: LWLF content 24, original package SHA-256
d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495.
Original installed scripts were verified before and after owned attempts.
Task app/game ran in isolation without mouse, global keys, focus forcing,
window rearrangement, Chrome interaction or changing the Chinese game locale.
Only task-owned native/RPC requests and task WebView DOM/CapturePreview were
used. Task owner session, challenge, exact PID, live server 2212, helper
request correlation, native AOI union, reply flags and camera restore were
recorded. A helper-generated request ID is NOT an authoritative native
server echo. Background frame cadence and foreground-vs-background timing
equivalence were not independently measured.

The first native AOI trial correctly rejected a non-WorldScene request.
WorldReady returned before the home camera settled on the next attempt;
pre/post camera mismatch triggered a restore timeout. The existing
production scanner's supported passive WorldReady and three-second settle
then yielded a genuine 30-AOI, 22-Resource target with response flags,
stable camera and home-view restoration in 1.073 seconds (R4). This is a
**small baseline witness**, not a complete scan or a new speed claim.

## B/C — native alternatives and decision

| Attempt | Actually observed | Verdict |
| --- | --- | --- |
| Direct SendAoiRequest (R4) | 8.059s, 8 AOIs, no accepted resource data, native response timeout | Reject |
| SendViewRequest(tile, LOD0, explicit same server), no camera movement (R5) | 8.047s, no fresh native response flags, timeout | Reject |
| Two regions (455,425) then (545,495), return only at end (R6) | First region captured 28 actual Resource rows; next target AOI absent immediately after send | Unqualified |
| Staged-file R7 | Old Lua revision accidentally staged; same prior native error | Not a distinguishing native trial |
| Deferred second-AOI verification (R8) | First 28 rows; requested second index 4954 absent from post-reply native current AOI set (nine cells observed) | Reject |
| **Scene-settled second target (R9)** | In a later game-pump callback, second native camera tile **545,495** settled. The following UpdateViewRequest still failed authoritative target AOI/network corroboration. | Reject; excludes only same-tick camera update as an explanation |
| **StartViewRequest after scene settle (R10)** | Actual second camera tile **545,495**; native StartViewRequest succeeded; unscoped network packets sent **202 to 203**, received **228 to 233**. Both reply flags observed, but native nine-cell current AOI set **excluded the second target AOI**. | Reject; transport activity and reply flags do not establish target completeness |

No positive second-region native footprint and no exact final-only restore
receipt exist. All candidates were fail-closed; no partial/empty region was
promoted to complete. Experimental native acquisition modes were removed
from the release surface. The canonical Lua scanner exactly matches the
starting checkpoint by git blob ID cd190f4294cdd9d4131180851a1164b967025bb4;
the research-only C# driver is archived as the noncompiled
tools/map_010_background/Map010SmallNativeTrial.reference.txt.
The existing serial camera-assisted method stays in production.

**Further R9/R10 diagnosis:** No game process was present at preflight.
Each attempt used a fresh isolated profile, current v24 client, original
script hash gate/backup, a task-only Lua probe staged into a temporary
Desktop runtime, and exact owned Home Stop. The experimental chain2
Lua and C# driver remain solely in ignored
artifacts/map-010/chain2-r9 and artifacts/map-010/chain2-r10,
not compiled/shipped or tracked. Afterward the temporary host CLI was
manually returned to its exact committed blob, both staged Lua sources
were restored from the canonical file, and the original installed
game scripts were intact. R10 exercised a materially distinct native
lifecycle: StartViewRequest after the real second WorldScene camera
tile settled, then UpdateViewRequest(true). Successful calls, reply
flags and unscoped network send/receive increases **cannot** replace
authoritative second-target AOI payload coverage. Identify
HandleViewPointsReply payload source, reply-window/segment provenance
and actual second-target current-view-index transitions before further
live trials. Do not repeat R9/R10 unchanged.

MAP-009 recovered that known point detail and Railway march-position
serializers carry explicit server/world/point or UUID fields. That proves
message arguments, NOT an accepted remote reply, remote authority, safe
context independence or discovery of unknown objects. No new live
known-target remote response was qualified. No full remote-server region
was accepted without the established passive server-view switch/return.

## D/E — accuracy, eight kinds and why no large benchmark ran

City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure
individual/mixed selections retain shared fresh AOI acquisition with
native per-kind hydration and detail, expiry/amount/protection semantics.
Scoped native lists are not exhaustive substitutes: Resource collection
range APIs are scoped, Train list missed genuine Truck populations in
an earlier native trial, Doomsday and other per-kind sources are activity/
cache scoped, and Ghost plus full seasonal Treasure lack positive broad
coverage. No source-proven complete paginated all-object list is enabled.

Previously accepted alternating all-eight Normal/Fast observations:
210.651 / 207.424 / 207.905 / 206.860 seconds; 10,000 AOIs, 340
serial native probes per run, zero failed logical blocks, strict positive
seven-kind field audits, Ghost absent. The repeated home-view component
was measured at 89.981–90.460 seconds per scan. Its removal remains a
**hypothesis, not an established saving**. Because none of the new routes
passed even two distinct target regions with native coverage/restoration,
there were zero qualified MAP-010 alternating individual, City+Resource+
Monster, Truck+Railway+Dispatch, City+Truck, all-types or two-server
production comparisons. No new per-type/mixed scan-time improvement,
missing/extra/duplicate equivalence, Stop latency distribution or global
optimality can be claimed. Production Normal/Fast stays as before.

## F — completed ready release findings

**LEADMAP008R5-02 completed:** loaded/missing-root/error/retry feedback
is now translated for all nine app languages. The original recovered
catalog files remain verbatim; the explicit translation overlay is
checked by the regular locale completeness test and the mounted Map
route tests, including native error/retry and retired-owner responses.

**LEADMAP008R5-01 completed:** actual settled, isolated Windows production
WebView states EN/light and JA/dark; not preview data, filenames or
synthetic ids. Read-only copied R5 SQLite has genuine 7,989 Resource and
8,830 Monster records. Each state rendered 50 of each kind on the
current page and matched 4/4 Resource plus 6/6 Monster filter names
against installed current game-text build 23555. Both were captured
using the app's own background-safe WebView API. Earlier incorrect
Chinese/light/empty captures are retained as history.

An associated genuine-data **browse** correction is complete: when
disconnected, server=0 is honest even with saved server-2212 records.
The new read-only map_saved_servers service enumerates exactly
published positive SQLite server IDs (no invented server, upper-bound
or 32-server cap), allowing disconnected browsing. Changing backend
owner resets saved server selection to that profile's store. Inert
managed native store tests verify empty, zero sentinel, multiple
positive IDs, caller-supplied context ignored and reopened DB.
This does NOT improve live scan speed or invent fresh connection status.

## Tests, cleanup and continuation

Managed Desktop Release/Home/Map/Auto/ownership/pipe/scan-publish and
MapStore checks passed; the MAP-009/R10 offline body/model tests now pass 12/12.
One post-R10 adversarial model regression rejects a second-target AOI
gap even if reply flags and unrelated network counters are positive.
This test does not impersonate a native server callback.
The unchanged final Windows package remains source-identified to
9f3f6db99bcc2f38d8af6e827f2eaccbd21135a6 at
artifacts/map-010/release-final-20261012/
LW-Control-MAP010-BACKGROUND-RC-9f3f6db99bcc.zip (SHA-256
27DB54AA59B37D79AF958320BF45199778F78BFF6FA5B8EAED240CC581B5FB0D).
R9/R10 added **only research receipts, documentation and an inert
regression**, not new production source; no duplicate or misleading
post-trial release package was built. Its embedded source commit remains
the correct identity of the shipping code.
Frontend static/integration/completeness/drafts, MAP-008 mounted
native-name and lifetime, and release UI smoke pass. Validate exact
source/build package and provenance separately in final handoff.

All live attempts ended with exact owned Stop and original script
restoration. Cleanup receipt reports no remaining **task-owned**
game/launcher/application process, no task recovery journal,
Auto Launch/Reconnect/desired-running off, and zero game account actions.
A later unrelated LastWar.exe PID 39284 started at 12:29:06 local time,
was NOT in any recorded task-owned trial PID or task recovery journal,
and was left running untouched. Do not close an unrelated game or
claim that the owner's system has no active game.
No game/application process was present at the final R10 cleanup check.
The unrelated PID above is historical, not a process that this task
owned or closed.

**Next concrete technical dependency:** recover a source-supported
second-region native callback/segment identity with a complete fresh
AOI set and point/march fields, then prove exact final home restoration
with adversarial timeout/retirement/Stop. Use non-overlapping positive
native witnesses and independent differential baseline reads before
any larger individual/mixed/two-server benchmark. For remote point
queries, prove an authoritative response source server/world and
object-field match without silently switching context. Do not
repeat unchanged failed R4/R5/R8 hypotheses. Ghost positive and full
seasonal Treasure remain owner-deferred; plunder, claim and sharing
live actions remain owner-only.
