# PM-021 — accept independent Home busy presentation review

Date: 2026-10-02. Reviewer checkpoint:
ff787e31acd048df8091a74da18e9ee3b595eb93.
Decision: COMPLETE / ACCEPTED for LWB317-UI-HOME-BUSY-001 focused recovered-source/
current-local presentation and predicates, through LWB317-REVIEW-HOME-BUSY-001.

## Basis and verification

Lead inspected the worker commit, independent checker, recorded cases and input
mapping: only review/evidence files changed, no product edits. The checker extracts
actual Kr/qr and current HomePage/App expressions, executes 17 distinguishing
cases with source-derived expectations and checks six assigned source anchors.
The recovered start/stop predicates are compared with the current expressions
using an isolated available-provider input; actual production availability remains
false and controls remain disabled. This does not prove an implemented provider.

Source index-BVfnK1wp.js SHA-256:
44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 bytes: Kr 336469, qr 336694, Home caller 373291, proxy state
361529, folder state 361581, derived launch-busy property 248345.
Exact target executable hash was checked:
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

Lead replayed check-review.mjs --verify-record and validate-evidence.mjs: PASS.
The historical busy checker was replayed without --record/--verify-record: PASS
for 13,824 renders, 384 predicates, 27 preference and 63 preview cases.
Historical original-source anchors and saved images are checked by the new review
harness. The old App hash is intentionally historical after the accepted root
correction; neither old reports nor validators were rewritten to erase that change.

Root R1 acknowledgement checker/validator, accepted translation checker/validator
and switch-localization checker/validator all pass with their saved records.
Canonical npm check, check:production-build and protected-WIP/source validator
pass. Package fingerprints remain 1280d8a4df7aec2f81260a8081c0dda7626bad06e8771d25cea4e3ae0c49daf2 /
b2a903176188b57cd02f281ffaded42aa05a0d6503595e25ee3e5fcfa09127ae.
Current App normalized-LF hash remains BA4A300D525A271C61A8061406CA81DE2414AACC15DF10A728B0E1A8641B2701.
Direct reviewer remote revision matches the submitted SHA.

## Reachability and visual limits

App's actual homeBusy writes are empty, gameRoot, autoLaunchGame and autoReconnect.
homeState does not pass proxyBusy or gameLaunchBusy. Those flags are supported
by the recovered formatter/preview fixtures but have no current production
producer. lifecycleProviderAvailable remains false. These are explicit remaining
input/lifecycle gaps, not a defect in the independently reviewed formatter.

Reviewer browser DOM evidence covers five English/Japanese fixtures, disabled
lifecycle controls and zero captured console errors. Lead opened the exact saved
home-launching-ja.jpg and verified that the Japanese header and both disabled
button labels are visible. Its recorded hash is
1F190D6484F2777D78E93D461C70434626A8D3D2737DA0DCD68FD854F6B0101B.
Lead did not rerun the browser or access the protected original runtime.
This image is clone-preview evidence, not original pixel parity.

No native folder picker, game launch/close/repair, native persistence, lifecycle
reachability or complete Home UI parity is accepted. Overall Home matrix status
remains IMPLEMENTED_NOT_VALIDATED with original-pixel validation BLOCKED.

## Integration and continuation

Updated current UI master, lead sheet, matrix, ledger, work items and handoff.
The named Home presentation corrections (switch, errors/channels and busy display)
are accepted for their focused source/local scopes. This closes that correction
queue, not all Home requirements. Protected AFK/scratch/parent screenshots remain
unchanged and unstaged; no product rebuild is needed for this documentation review.
Lead verification: evidence/lwbridge-0.3.17/ui/LWB317-PM-021/lead-verification.json.

Next sole worker task: LWB317-REVIEW-MAP-TRANSPORT-001, a medium read-only review
of Truck/Train table formatting, eligibility and retained goods. Treasure Checking
producer, other Map tables/interactions, Trade 003E and native lifecycle stay separate.
