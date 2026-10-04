# LWB317-UI-HOME-PREFERENCE-LIFETIME-001B

Status: COMPLETE / ACCEPTED after project-lead takeover, 2026-10-04.
Medium worker unit. Parent HOME-PREFERENCE-LIFETIME-001 is complete for its two
source/local preference units; overall UI remains PARTIAL.
Unit A is COMPLETE / ACCEPTED at `c0d9082ee8f8b4b8985e8025657967a6898a4097`;
see `docs/reviews/2026-10-04-LWB317-REVIEW-HOME-PREFERENCE-LIFETIME-001A.md`.

## Goal

Recover and reproduce Automatic Reconnection's profile-owned draft, immediate
visible edit, concurrent-save and save-error lifecycle. Correct its remaining
saving-only disabled state and acknowledgement-before-visible-value behavior.
This is phase 2 UI, not a native automation implementation assignment.

## Start and inputs

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/lwbridge-ui.md,
docs/UI_FINISH_CHECKLIST.md and the parent work item. Confirm branch/status and
actual HEAD; do not reset to the submitted A revision if the lead has advanced it.
Work alone, without subagents or delegation. There is no fixed elapsed-time stop.

Target: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`,
SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered main: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`,
SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

Read actual App.jsx, HomePage.jsx, ShellPresentation.jsx, previewConfig.js,
backendBridge.js and the existing native SetAutomation/local-config result mapping
in src/LWBridge.Desktop/LWBridgeBackend.cs. Read the A packet, existing Home/root
acknowledgement and shared draft/save-error packets, and OFFLINE-VISUAL-001 Home
counter-evidence. Search narrowly; avoid printing entire minified bundles.

Source seeds, verified by the lead as UTF-8 byte offsets in the exact main asset:
config draft T 191297; profile-keyed store re 193714; subscription me 195132;
shared Bn 213332; Home qr 336694; Home consumer de=ue.draft 362521;
flag setter It 366485; ordered four save-error banners 372793.
Recover complete producer/read/write/default/confirmation/Retry/Discard contracts
and exact slices before editing. It uses selected-profile flag lookup,
edit(value, false), then flush; the source draft failure semantics differ from
Auto Launch's clone rollback adapter. Do not combine the two preferences.

## Allowed scope and boundaries

Canonical App/Home preference state and a focused helper if needed; bounded
existing shared save-error wiring when required by the recovered source. Prefer
the existing source-derived createConfigDraft engine; do not duplicate it or
change shared semantics without separately proving every affected caller.

Preserve Auto Launch A, root/action error independence, picker cancellation and
acknowledgements, global/local profile and retained-panel lifetimes, Map ownership,
lazy/Activity/motion, unrelated automation, localization and provider fences.
Do not populate unsupported flags just to fill the four-slot banner list. Preserve
the source banner order, explicit fixture injection, translated Retry/Discard and
its existing saving predicates.

The existing bridge captures a bootstrap profileId and injects it into profile
commands, rejecting missing profile before dispatch. The current native adapter
stores AutoReconnect in global clone config, not a recovered per-profile native
store. Inspect and document this boundary. Never allow a selected-profile UI store
to silently write another bridge profile; test missing/mismatched ownership and
obsolete acknowledgements with inert responses. Do not invent profile getter
commands, native schemas, per-profile persistence guarantees or fake success.
If the adapter cannot supply a recovered state, record that concrete boundary and
prove the local contract with an explicit controlled adapter. No C# provider work,
new transport/provider, game launch/control, auth bypass, login UI or fallback.

Protect all assignment-start dirty/untracked paths. Pin them before work, including
the existing Map result WIP, previewAfkFixtures.js, .scratch-lwb317/, CORRECT-003
screenshots and untracked equipmentMotion317 LICENSE/NOTICE files. Preserve others'
servers/tabs and owner sessions. Do not click destructive browser confirmations.

## Sequential milestones and acceptance

1. Recover exact source slices and execute the original contract; preserve an
   immutable actual-dispatch baseline that distinguishes the current defects.
2. Correct the canonical consumer/store/adapter. Exercise actual production App,
   Home, shared store and banner callbacks with controlled inert responses.
3. Verify and document the full owned diff, commit/push coherent checkpoints and
   directly verify origin/research/offline-controller at the full delivered SHA.

Required distinguishing cases: initial source/default and adapter provenance;
immediate off/on/off while a first write is deferred; a second edit during saving;
confirmed versus draft values; older success/rejection; latest failure and recovered
error presentation; actual Retry and Discard; incoming config polls while dirty or
saving; profile replacement/return and unmount; missing/mismatched profile
pre-dispatch rejection; source Home/Bn editability and unavailable-provider fencing.
Use recovered generation/queue semantics rather than a guessed latest-wins policy.

Run A's current mounted/preservation regressions (use narrow current adapters if
the App hash/shape advances), affected Home/root/save-error/shared draft checks,
canonical npm check/build/check:production-build, evidence validator, protected-WIP
guard and git diff --check. Preserve historical records and pinned baseline hashes;
do not rewrite old assertions or record writers to manufacture passes.

Real inert browser QA must exercise pending/concurrent edits and Retry/Discard,
EN/light and JA/dark labels, retained profile ownership where supported, and capture
settled screenshots plus fresh console results. Preview actions remain fenced.
Distinguish controlled responses from actual native persistence and original pixels.

Write under evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/
and a dated review; update this delivery record and relevant handoff notes. Keep
global/parent acceptance with the lead. Explicitly stage owned paths, never force
push or discard unrelated work. Return AWAITING_REVIEW with exact checks, source
locators, limitations, full commit/remote SHA and continuation. Stop at B; do not
start another preference, page, visual campaign or native implementation.

## Lead takeover delivery — 2026-10-04

The interrupted worker's saved implementation/source/baseline/browser records
were preserved. Lead review found and reproduced an omitted reconnect status-event
producer; the correction shares acknowledgement across polls/events and fences
obsolete profile/unmount inputs. New keyed stores initialize from incoming state.
Actual mounted App replacement/return coverage now includes obsolete success,
rejection, read/poll and retired listeners. Reconnect 13/13, Auto Launch 7/7,
preservation, original/current identity differential, browser EN/light + JA/dark,
canonical check/build/package and protected-WIP 10/10 pass. Independent follow-up
review found no remaining assigned-scope blocker.

Detailed acceptance: docs/reviews/2026-10-04-LWB317-UI-HOME-PREFERENCE-LIFETIME-001B.md.
Evidence/validator: evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/.
No native persistence, per-profile native store, gameplay or original runtime/pixel
parity is established. This task is finished; do not restart it. Next is the broader
offline visual queue under a separate lead assignment.
