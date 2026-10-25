# Gathering recall with overlapping game callbacks

Branch `codex/lwtool-remaining-tabs`, draft PR #17. This continues the owner's
repeat recall-on-disable report after product `44be59d8`. No merge or release.
The owner switched to the approved sub-account and explicitly resumed live
testing. Three squads are unlocked. Main-account authority was not expanded.

## Recovered defect and correction

The Resource Map detail observer wraps
`Net.Msgs.WorldGetDetailMessage.HandleMessage`, also used by gathering.
The previous fix registered gathering/attack wrappers but omitted the Map
wrapper. Gathering therefore rejected an active underlying callback as an
unknown replacement. The Map side also required its wrapper to be the top
function, so starting gathering above it could invalidate Map results.

Map now registers the explicit predecessor link and accepts its wrapper only
when it remains in that bounded, registered chain. Unknown replacements still
invalidate readiness. Reset retires the Map generation before conditionally
restoring the top callback; an old wrapper held underneath a successor becomes
pass-through and cannot observe a later scan twice. Native reply ID, connection
generation, scan/session/challenge/server identity and frozen-detail checks
are unchanged.

The Hotkeys recall observer shares `WorldMarchChangeMessage.HandleMessage`
with gathering. Source inspection found the same missing registration/top-only
predicate there. It now joins the same explicit registry. Its original
account/formation/march and one-send reply/poststate checks are preserved.
This Hotkeys interoperability case has controlled qualification; do not call
it an independently reproduced live trigger of the owner's screenshot.

Read-only gathering observations now identify which of six callbacks is
direct, present in a known chain, or missing, plus the last refusal/Stop
snapshot. They contain no function values or extra gameplay authority.

The opt-in isolated proof can select 1-4 squads, wait before Off, and start a
real Resource Map scan before Off. Exact account/process/session admission
still precedes all test operations.

## Live evidence before packaging

Ignored local evidence root: `artifacts/owner-gather-recall-20261026/`.
Original native runs are under `artifacts/map014-takeover/`:

| Run | Native root suffix | Result |
| --- | --- | --- |
| Prior behavior, one squad | `20261025-164200` | Gather and Off recall confirmed; idle afterward |
| Prior behavior, three squads, 60-second hold | `20261025-164719` | Three gathers/recalls confirmed, including arrival clearing a creation GUID |
| Prior behavior plus Map | `20261025-165859` | One confirmed gather, cleanup not confirmed; owner matched but detail callback was displaced and unknown |
| Fixed behavior, squads 2/3 plus active Map | `20261025-170755` | Two gathers and exactly two recalls confirmed; Off disabled, no error; displaced detail callback correctly recognized |

The negative Map run also had a resource-search error before the explicit
Map start. It is not a clean throughput comparison or a proof that every
failure in that run came from Map. Its actual callback snapshot demonstrates
the incompatible chain; the paired actual-source inert test isolates the
Map/Gather collision and fails with the old Map source. The later test used
only idle squads 2/3 and preserved the earlier squad-1 gathering trip.

Every completed isolated run exited through the owned lifecycle and verified
all three original game files restored, with no recovery journal. Raw march,
account and session identities remain in ignored local evidence.

## Loader and regression checks

The additional observer code exposed the probe's existing 64 KiB LENC entry
limit. The rejected diagnostic build was never installed. Packaging now joins
already-stripped statement lines with spaces; a current-source Lua AST/literal
comparison and LENC round-trip test prove unchanged code/string semantics.
The compressed probe is 64,573 bytes, within the 65,535-byte limit, and the
native fixed run reached connected state using this packaged source.

Passed: actual Gather/Map callbacks in both installation orders, retired
generation/rearm, unknown replacement rejection, twelve Resource reply
correlation tests, Gather, Attack, idle Drill, Automation and Hotkeys Lua
checks, full UI suite, full Desktop delivery suite, and 31 package-auditor
tests. No new Drill event or live join positive is established by these
gathering tests. Fresh final-package evidence is recorded separately after
source pinning; independent lead review remains open.
