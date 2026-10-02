# LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1 — worker delivery

Status: **AWAITING_REVIEW**. Parent `LWB317-UI-MAP-FILTER-LIFECYCLE-001`
remains CHANGES_REQUIRED until the project lead reviews this focused correction.

## Correction

PM-027 exposed a combined-effect gap rather than an options-handler-only gap.
In exact `MapDataPanel-B4GXEND2.js`, original `C` is the parent `scanState` prop,
so `C.serverId` maps to canonical `scanState.serverId`. Original local state `R`
is the data/browse server and maps to canonical `browseServerId` / derived
`dataServerId`.

The original options effect can redirect local `R` to `reply.serverId`, but the
separate `[R,C.serverId]` effect then returns it to the still-current scan server.
Canonical now has the same resynchronization. With scan server 321 and an
options(321) payload naming 322, exact original and corrected current both make
options requests `[321,322,321]`, discard the mismatched lists and settle at 321.

The R1 deferred loss case also confirmed a source detail in the same server
effect: a positive -> 0 scan-server loss clears search/cache/page/rows/loading,
but does not itself advance the original options generation. Canonical transition
invalidation therefore advances options generation on positive server transitions;
backend-availability disposal/unmount fencing remains separately enforced. The
lead's backend-loss/unmount cases continue to pass.

Successful Clear already writes both canonical scan state and browse server from
the acknowledgement. Its recovered ownership is unchanged: delayed Clear(321),
real transition 322, then acknowledgement 321 still reloads options as `[322,321]`.
Same-server Clear, pending/failure retention and stale search/options/finally
fences remain covered by the unchanged parent assertions.

## Executed comparison and regression evidence

Primary original authority is exact `MapDataPanel-B4GXEND2.js`, SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
The target EXE identity remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

`LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-cases.mjs` executes exact
original and current production callbacks/effects for five cases: same-server
reply, redirected reply, obsolete redirect after a newer server generation,
actual positive server transition, and positive-server loss. Result: 5/5 pass.
PM-027's unchanged independent checker now reports 6/6 pass, including exact
`[321,322,321]` original/current behavior.

The unchanged parent filter checker passes through the R1 output-redirect adapter,
which stores fresh current results under R1 and byte-preserves the historical
parent result. Exact source-lifecycle recovery passes. Request lifetime reports
38 scenarios / 0 current failures; navigation replay reports 0 current failures
and 17 requests; exact interaction comparison reports 38 scenarios / 0 current
mismatches; strict integration reports 14/14. The maintained historical replay
passes filters, tables, Treasure Checking, row actions and Map states.

The two legacy standalone FILTERS scripts still fail if invoked directly because
their old source extractors omit newer production constants/helpers
(`DEFAULT_RANDOM_DELAY_TEXT` / `selectionMembershipKey`). The maintained
`replay-historical.mjs` adapter is the repository path that adapts those historical
scripts and it passes their filter/table/Checking cases. No production defect was
found from those direct legacy harness failures.

Frontend `npm run check`, production build and production-build validation pass.
Built fingerprints are
`df8d3132fea74cdbbe3b8ee9cff964e5f3fcf76c7edf935fff565cb2d4566e9c` /
`5e4b411c08e9e69a28d75cdfccc36a751ddea0f5407c30c915ae9c83f8310589`.
New R1 evidence validation, protected-WIP guard and `git diff --check` pass.

Fresh offline browser smoke used the deterministic `map-filter-lifecycle` fixture
at server 321. A real City alliance select changed `all` -> `name:none` and the
result count changed 55 -> 1. After clearing only the two task preference keys,
Treasure initialized `includeForeignRadarTreasures=false`, `luckyFirst=true`;
real checkbox clicks persisted `true` / `false`. The keys were removed again and
captured console errors were empty.

## Evidence and limits

New evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/`.
It contains the exact original/current case runner/results, output-redirected
parent replay, browser smoke JSON and evidence validator. Historical PM-027 and
parent lifecycle result JSON remain unchanged.

This is source/component-runtime and deterministic offline-preview UI evidence.
No native Clear, Treasure, scan or gameplay operation was executed. It does not
establish original post-auth pixels, native provider parity, full Map parity or
global UI acceptance. Protected AFK/scratch/CORRECT-003 WIP remains unchanged and
unstaged. Project-lead acceptance is still required.
