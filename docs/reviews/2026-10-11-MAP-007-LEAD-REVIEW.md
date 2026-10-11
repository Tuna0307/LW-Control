# MAP-007 — independent lead review

Reviewed source: `fbbfa98305fc241df78dfddb7b092830920ce834`, PR #9.
**CHANGES_REQUIRED** for the scoped preparation/inspection/preview delivery.
No merge/publication. The accepted v0.6.0 product is unchanged.

The worker's important scope qualifications are correct: Ghost execution and
Treasure reward claiming are unavailable, not live proven. This delivery validates
stored Ghost fields/returns a preparation plan, separates future result channels,
and inspects/previews Treasure through existing read-only producers. It does not
complete either native game action. No claim/plunder/share was executed by the lead.

## Reproduced findings

**LEADMAP007-01 (P1): busy state survives retired preview/preparation.**
MapDataPage.jsx:876-879 invalidates actionGeneration on mapApi/dataServerId changes
but never releases/reset the retired operation's busy state. Both new callbacks
skip their finally cleanup on generation mismatch. Start a deferred preview then
change browse server/API: the old reply is discarded but busyKey remains
preview-treasure, disabling the replacement owner's controls indefinitely.
Fix with explicit operation ownership and retirement cleanup, without allowing
old completion/finally to clear a successor's busy state. Cover held success,
rejection, profile/server replacement and successor operations.

**LEADMAP007-02 (P2): late result resurfaces on another tab.**
Action generation excludes tab lifetime; changeTab clears the action message but
does not retire these operations. Start a deferred Treasure preview, switch to
City, resolve: City receives map.treasurePreviewReady and a search revision.
Ghost preparation has the same completion pattern. Fence tab/request ownership,
clear obsolete busy state and preserve any successor's own result/busy state.
Cover tab leave/return and overlapping/deferred acknowledgements through mounted
controls, not just synchronous preview fixtures.

Executable negative probe: tools/map_007_lead_review/check-ui-lifetime.mjs.
It executes current production callback/effect source with inert state setters;
this is not a mounted browser or live proof. Immutable negative JSON is
docs/proof/MAP-007-LEAD-LIFETIME-2026-10-11.json. A zero probe exit here means
the two negative cases were reproduced, not that the product passed.

## Fresh checks and artifact audit

- npm canonical check: PASS, all five groups, nine 1,389-key catalogs.
- Desktop Release default suite: PASS, including new Map007 handler/store and
  dedicated controlled Ghost correlation tests, accepted Home/Map regressions.
- Current canonical production build identity check: PASS.
- Candidate ZIP independently opened/hash-checked: 9,573,115 bytes / 83 entries,
  embedded source fbbfa983... exact, no matching private script/DB/XLSX/profile/
  evidence/PDB entries. SHA-256 7AB40A4BF2CB573078A1F4B778952FBE047769F38A4FEB1867368F78094AA703.
  ZIP integrity does not close the two production UI findings.
- git diff --check: PASS before review additions. No new game launch/native action.

## Scanner finding and next priority

**LEADSCAN008-01:** Normal and Fast retain requested limits 8/20, but the actual
engine serially awaits captures and the current full-world batch predicate does
not inspect ScanMode. Inert actual-engine replay observes one active capture in
each mode; both admit the same full-world path. This is an existing scanner gap,
not introduced by MAP-007 and not a reason to call the accepted data fictitious.
It does mean the mode selector has not demonstrated its promised speed distinction.

Replay: dotnet run --project tools/map_007_lead_review/Probe.csproj -c Release.
Negative snapshot: docs/proof/MAP-SCAN-MODES-LEAD-2026-10-11.json. The probe's
initial project reference produced an unused WebView2 WPF reference warning;
the same exclusion used by the canonical Checks project removed it. Final replay
passes without that warning. This is a controlled concurrency proof, not game
throughput. Full-world camera/response state must not simply be parallelized.

Owner's next priority: faster accurate multi-server Truck acquisition, whether
switching server/camera is necessary, real Normal/Fast behavior and a verified
candidate before personal testing. Assigned continuation is
../work-items/LWB317-MAP-SCAN-METHODS-008.md, on the existing PR #9 branch.
Correct these two small MAP-007 defects first, then prioritize scanner work.
Preserve negative history and available main release. Lead owns acceptance.
