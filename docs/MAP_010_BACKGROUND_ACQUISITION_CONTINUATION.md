# MAP-010 — current background-acquisition continuation (2026-10-12)

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

No positive second-region native footprint and no exact final-only restore
receipt exist. All candidates were fail-closed; no partial/empty region was
promoted to complete. Experimental native acquisition modes were removed
from the release surface. The canonical Lua scanner exactly matches the
starting checkpoint by git blob ID cd190f4294cdd9d4131180851a1164b967025bb4;
the research-only C# driver is archived as the noncompiled
tools/map_010_background/Map010SmallNativeTrial.reference.txt.
The existing serial camera-assisted method stays in production.

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
MapStore checks passed; MAP-009 offline model tests 11/11 passed.
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
