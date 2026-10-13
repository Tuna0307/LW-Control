# MAP-012 — answer the owner's scanner questions and implement qualified gains

Owner assignment, 2026-10-12. Repository
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`; existing branch
`codex/map-action-providers-007`, PR #9. Starting lead checkpoint
`4daceed00a533b0a8e084bf0763722c9af55ffb1` plus this assignment commit.
Solo; no new branch/worktree, subagents, AI chats, force-push/reset/discard.

## Goal and definition of completion

The owner asks for the best supported accurate/fast acquisition, not a distinction
between Normal and Fast. Leave those controls and their existing behavior unchanged.
This assignment supersedes checkpoint-only stopping: completing a trial, package,
UI correction or test suite does not answer the overall scanner questions.

Keep an answer matrix for Q1-Q8. For each give plain conclusion, exact source
mechanism, genuine positive/negative observations, scope, uncertainty and product
consequence. An unsupported UNKNOWN or a single failed attempt is not a completed
investigation. Continue available source recovery and materially different supported
experiments. Do not assert universal optimality/impossibility from finite tests.
If a question genuinely cannot be decided with accessible inputs, say why precisely,
what available investigations were exhausted and what distinguishing evidence is
missing. Return PARTIAL with that question open; never label it all answered.

Q1: How does the game request, receive, cache and retire map objects? Which coordinate,
world/server/LOD/window/index spaces are involved and how are they connected?
Q2: Can complete fresh unknown-population discovery avoid camera movement, for each
kind and for mixed selections? Separate known-object lookup from discovery.
Q3: Can the correct camera-assisted sweep avoid intermediate home returns? If not
in the tested client, what supported lifecycle dependency establishes that limit?
Q4: Can remote known-object and regional/full-map queries avoid changing active
server/world context? Current server and remote server are separate cases.
Q5: Which supported acquisition method is fastest while preserving scope/freshness/
fields for each kind, representative mixtures and multiple servers?
Q6: What accuracy/completeness is demonstrated? How are omissions, duplicate/extra
records, stale fields, wrong-server results and legitimately changing objects detected?
Q7: Do mixed selections share discovery/enrichment work optimally; what are marginal
costs and avoidable repeated requests? No separate full sweeps unless justified.
Q8: What is actually enabled now, what remains experimental/rejected/deferred, and
what changes should an owner expect when testing the final scanner?

## Authority and preservation

Read AGENTS.md, MAP-011 independent review, MAP_010_BACKGROUND_ACQUISITION_CONTINUATION,
MAP-009 source inventory/tools and the current source engine/provider/Lua/store.
Preserve accepted Home/main/v0.6.0, owner-keyed Map browsing, installed names,
complete datasets, shared mixed sweep and Auto Stop-before-return.
Retain R4-R12 negative evidence and the original owner inverse without repinning.

Background-only remains: owned app/game may open normally behind Chrome; no mouse,
global keyboard, minimizing, foreground/focus forcing, rearranging windows, owner
Chrome capture/control or owner setting/network mutation. Game remains Chinese;
clone language changes only isolated clone settings. Background native/RPC and
non-disruptive own-app inspection/capture are permitted. No wait for another monitor.

Passive scans/read-only official lookups/passive server-view switching/return and
bounded owned Home lifecycle are permitted after current compatibility, fresh
isolation, disabled initial Auto/tasks, exact process/session/challenge identity,
backup and restoration gates. No live claims/plunder/sharing/marches/combat/
spending/relocation/updater/protected-service access. Do not stop unrelated games
or closeUnmanaged owner sessions. Respect actual denial/pause; no workaround.
Ghost positive/full seasonal Treasure stay deferred; scoped methods can still be
recovered and meaningfully controlled-tested without inventing event availability.

## A — reconstruct actual native acquisition, not another blind retry

Trace SendViewRequest/SendAoiRequest/StartViewRequest/UpdateViewRequest,
HandleViewPointsReply, ordinary camera navigation, split/segment completion and
point/march caches through actual current managed IL and decoded Lua callers.
Recover native receive entry and payload parser, routed server/world identity,
LOD/AOI index encoding and manager subscription generations. Verify calculation
spaces: scene tile, packed position, point index, AOI index, world/LOD and bounds.
Do not assume the nine-cell set in R11 is the requested LOD/target footprint.

Explain why R11's second camera tile changed but the verified region set did not.
Form explicit competing hypotheses; use actual code and counter-evidence before
changing the private trial. Capture actual request arguments/reply headers,
native segment/cache transitions and relevant camera/context events. Generic
packet counts, ready flags or helper IDs cannot supply native provenance.

Recover reflection overload/argument conversion and official manager callers where
needed. Do not guess private flags or overwrite native state to manufacture success.
Source-led simulations validate an interpretation; they are not network witnesses.

## B — qualified small trials, route alternatives and remote identity

Test genuinely distinct source-supported routes: consecutive camera regions with
one final restoration; direct supported region requests; correctly scoped native
lists/searches/details; explicit remote known-target requests. Do not repeat R11
unchanged. Two/three fresh known-positive nonoverlapping regions are the first gate.
Require complete fresh target data and exact final view/zoom/manager restoration,
owned Stop after positive acquisition, timeout/error and retired owner handling.
Partial trials must not modify completed published datasets.

R12's detail server=0 is inconclusive, not proof remote lookup cannot work. Inspect
actual response schema, nested fields and native callback/pending-request context.
An explicit serverId echo is not mandatory if authentic source association can
instead be established through source-proven request/connection/callback ownership
and distinguishing live witnesses. Merely copying requested serverId into a result
or matching a coordinate-derived point ID is not proof. Test known same-coordinate
wrong-server cases, stale cache, retired connection and mismatched responses.
Compare unique native fields against independently refreshed known target witnesses.

Track active context throughout. Explicit request server argument alone does not
prove remote acceptance; indirect navigation is still a context switch. A complete
known object is not a full remote population. Only source-supported positive
regional routing expands to remote region discovery/full coverage.

## C — all-kind/mixed efficiency and accuracy

Cover City/player, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure.
Trace list scope, paging/caps/refresh/expiry/season/family omissions. Native lists
may accelerate enrichment/scoped lookup without substituting for full discovery.
Recover complete accessible routes, or demonstrate why tested alternatives cannot
provide the required scope. Do not use a list with zero objects as a fast success.

Keep one shared spatial acquisition for compatible selections. Identify repeated
detail calls, stale whole-world caches, unnecessary per-probe restoration and
avoidable CPU/network work. Preserve required amounts, goods, timing/protection,
identity, fields and supported populations. Measure background cadence/load; do
not force focus or change owner game/OS settings for a benchmark.

For each qualified candidate, compare actual production/native paths with preserved
baseline using matched background scope and alternating repetitions (at least
three runs per route where a speed claim is made). Singles for available kinds;
City+Resource+Monster, Truck+Railway+Dispatch, City+Truck and all available kinds;
bounded two-server individual/mixed Auto cycle with exact return. Avoid unnecessary
full sweeps for candidates already disqualified at small scale. Do not rerun all
255 subsets live; maintain meaningful controlled planner combinations/adversity.

Report median/range and sample count, native work, discovery/enrichment/publish/
switch/restore costs, missing/extra/duplicate/value comparisons, result age and
Stop latency. Separate configured8/20 from actual native concurrency. Leave
Normal/Fast differentiation alone; no artificial slowing or marketing relabeling.

Use independent native known-positive anchors/callback payloads, boundary/high-density
regions and time-bracketed cross-checks. State what full-region coverage proves and
what exhaustive population recall cannot be inferred. Moving/expiring objects must
be time-bracketed, not forced into matching counts. Inject meaningful missing/extra/
duplicate/stale/wrong-owner/server/value defects to demonstrate audit sensitivity.
Never present simulator counts or source-authored values as independent accuracy.

## D — implementation, self-review, delivery and owner answers

Implement only supported gains after freshness/scope/ownership/restoration gates.
Maintain one canonical route; capability/scope-based selection is allowed when
truthful. No blind fallback accepting incomplete data. Verify cancellation after
positive data, failures, publish races, stale callbacks, successor isolation,
Auto Stop-before-return/no-next-target and stored data preservation.

Loop diagnose -> supported change -> distinguishing retest -> self-review ->
coherent checkpoint -> next ready question. Solve ordinary failures without relay;
do not stop merely because a package or finite trial finished. Keep one concise
Q1-Q8 status/next-action matrix and compact raw source locators/metrics. Checkpoint
on context/usage interruption, explicitly marked interrupted rather than done.

After settled useful changes, run affected frontend/native/store/pipe/ownership/
planner/accuracy/inverse checks. One final source-identified ZIP only when necessary,
correct native-name/browse EN/light and JA/dark verification, private-file exclusion.
Each live attempt ends with exact owned Close/Stop, scripts restored after confirmed
exit/no surviving installation process, no task journal/request/subscription, Auto
off and zero armed/new action jobs. Do not claim the owner's computer has no game
merely because task-owned processes are gone. Preserve original scripts/datasets.

Commit/push coherent work, verify direct remote SHA, update PR #9 without changing
it to imply global fastest/perfect recall/whole Map completion. No merge/publication.
Return ANSWERS_READY_FOR_LEAD_REVIEW only when Q1-Q8 have supported scoped conclusions.
Otherwise PARTIAL with exact unresolved question, accessible analysis/trials actually
performed, missing distinguishing input and next action. Lead acceptance separate.
Final report must lead with the answers in plain words, then the comparative table
and actual enabled implementation. A list of passing tests is supporting evidence,
not the completion criterion.
