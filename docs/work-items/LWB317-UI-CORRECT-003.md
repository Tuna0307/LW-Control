# LWB317-UI-CORRECT-003 — review recovery and finish Automation/AFK UI branches

Project-lead assignment: 2026-10-01. State: PARTIAL — active scope split.
Owner: fresh worker chat, manually dispatched by the owner.

PM-008 inspected pushed implementation 5204f67 and unfinished evidence/WIP.
The owner requested smaller tasks after another apparent interruption.
Active continuation is LWB317-UI-CORRECT-003A (weekly quality controls only).
Preserve this document as the parent backlog; do not execute its full scope now.

## Fresh-chat context and goal

You have no assumed prior memory. The owner wants a one-for-one reproduction of
LWBridge 0.3.17's in-scope post-auth product, without the original login/account/
commercial licensing UI and without a legacy product fallback. Preserve the
eight-page goal and original workflows, wording, defaults, conditions and timing.
Current work is UI completion; native Home/gameplay integration is separate.

The previous worker was interrupted. The project lead recovered its uncommitted
work into `de6c0755d7270d43b3a84eb315202aa7b62effe2` (CORRECT-002), which is
AWAITING_REVIEW. Its targeted Construction/Training/Gather/Train, AFK profile
drafts and read-only Map fixture checks pass locally. This is not full UI parity.
First independently review that checkpoint, then complete the remaining
source-recoverable Automation and Squads/AFK UI branches identified below.
Do both stages under this assignment; do not return only a plan or inventory.

## Workspace and inputs

- Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
- Branch: `research/offline-controller`; remote: `origin`.
- Reviewed implementation baseline: `de6c0755d7270d43b3a84eb315202aa7b62effe2`.
  Start from the latest pushed lead checkpoint containing this assignment;
  never reset newer work to this baseline. Inspect/preserve existing WIP.
- Exact reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
- SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Canonical frontend: `src/LWBridge.UI-0.3.17`.
- Exact assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.
  In particular AutomationPanel-BJ0gIqFh.js, SquadPanel-HC3-DJei.js,
  index-BVfnK1wp.js, GameAssetImage-Diy9VTIr.js and hash-protected CSS/locales.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/strict-parity-recovery.md and this assignment before editing. Read the
current UI master/parity matrix/feature ledger/project status/implementation
handoff. Read CORRECT-002's dated review and its complete evidence directory,
especially coverage-matrix.md, source-contracts.json, browser-qa.json and
verification.json. PM-006/PM-007 and CORRECT-001 are historical review inputs,
not current-code facts. Revalidate claims against this checkout and exact assets.

## Stage A — independent checkpoint review

Review the CORRECT-002 diff and actual source/handlers/state, not just passing
marker checks. Re-run applicable checks and independently reproduce representative
Construction keyboard/choices/validation, Training activation/defaults, Gather
drafts, Train clear/reward order, distinct AFK target/squad/join/member round trips,
new-profile independence and save error/retry/discard. Verify Map applied filter/
sort/final-page/zero matches and native-mode/action fencing. Inspect concurrent
edit/old-ack behavior in the actual store and default/fixture separation.

Write baseline review findings with source/file locators and real reproductions
before fixing defects. Give a recommended disposition for the focused CORRECT-002
scope; project lead owns final acceptance. Correct any demonstrated regressions
in these touched UI/preview components while preserving production Map contracts.
Existing tests/50 browser records are evidence to inspect, not automatic proof.

## Stage B — remaining source-backed UI implementation

Recover each branch's exact source identity/locator, producer, consumer, defaults,
limits, conditions, validation and state transitions before implementing it.

1. **Automation drafts:** connect weekly Trucks/Secret Task quality choices and
   remaining uncontrolled settings to their recovered config model. Verify dirty,
   validation, save/error/retry/discard and source-prescribed view-change behavior.
   Audit Gather and Trade's current local state against the original retention
   contract; correct mismatches rather than imposing a new persistence policy.
2. **Trade Station:** implement positive currency/goods selection, item/offer
   detail, filtering, purchase-history rows and source-proven empty/loading/error/
   disabled branches. Purchase/game actions remain fenced; fixture presentations
   must not report a real transaction.
3. **Dispatch Assist:** implement the recovered allied-task list, selection and
   schedule/status branches, including negative states and existing delay/quality
   validation. No real dispatch or scheduling service may be added.
4. **Remaining Automation cards:** audit every card against its original render
   and implement omitted summary/result/error/timing/shield presentation. Preserve
   precise status precedence, conditional controls, formatting and labels. Do not
   substitute generic fields or a permanent empty state for recoverable branches.
5. **AFK targets and members:** recover target grouping/option construction,
   undiscovered and searchable attack-range warnings, custom/list restoration,
   filter restrictions and all recovered member loading/failed/left/self/missing
   variants. Preserve complete per-ID drafts, target metadata and new defaults.
   Exact original option rules are contracts; synthetic QA entries are not the
   real current-game target inventory. Record unavailable data/assets distinctly.
6. **AFK toolbar panels:** audit and complete source-recoverable Potion/Master,
   Alliance Drill, Garrison and Zombie Bus forms/status/conditional states. Retain
   already useful Garrison/member/Equipment work. Equipment changes are limited
   to demonstrated regressions exposed by Stage A, not a separate redesign.

Use disclosed deterministic preview fixtures to expose positive and negative
states. Cover actual field composition and payload normalization where locally
recoverable; native providers/writes remain unavailable. Fixture IDs/values are
never asserted as original/current-game facts. Keep fixture selection preview-only.
Original CSS/assets must remain byte/hash protected; follow recovered DOM hierarchy.

## Allowed scope and boundaries

UI implementation, exact local artifact/helper/state-dependency inspection,
isolated preview adapters, read-only offline QA and documentation. Required local
auth-produced dependency tracing follows AGENTS section 6; do not bypass original
authentication/entitlement or reconstruct commercial UI. Authorized original UI
observation is allowed only through the documented access boundary.

No Last War launch/control, live scan/server jump, native Home lifecycle, new
gameplay/backend/provider family, updater installation or protected-service
access. Preserve existing native Map and live-proof dispositions. No new fallback,
legacy retirement task, worker/subagent dispatch or unrelated cleanup. Other
pages retain their existing states; this task does not shrink the overall goal.

## Acceptance and delivery

Deliver `docs/reviews/2026-10-01-LWB317-UI-CORRECT-003.md` and evidence under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/` with:

- Baseline CORRECT-002 independent findings and recommended disposition, kept
  separate from subsequent implementation results.
- Per-control source contracts, source/screenshot hashes and complete affected
  branch matrix. Mark implemented, tested, unreviewed, unknown and concretely
  blocked branches separately. Missing recoverable UI is an implementation gap.
- Actual browser interactions: weekly edits/discard, Trade positive/empty/error
  and history views, Assist task selection/schedule presentation, representative
  runtime summaries/shield timing, AFK target/range warnings, member negative
  branches and toolbar forms. Verify two distinct profiles and navigation round
  trips again after changes. Cover desktop/responsive and light/dark, English plus
  relevant zh-CN/ja samples. Disclose DOM/harness versus physical drag proof.
- Targeted actual state/adapter/handler tests for meaningful edge cases. Run
  canonical npm check/build/check:production-build, evidence hash validation and
  git diff --check. Run other checks only when affected; no gameplay tests.
- Conservative UI master/matrix/ledger/status/handoff updates with exact remaining
  gaps. Do not upgrade full-product status, accept Map's Goal or self-declare full
  pixel/live parity. Do not delete prior evidence or rewrite project policy.

Continue until the assigned recoverable UI scope is implemented and verified, or
a concrete blocker is documented. No 20-minute block/deadline. Preserve coherent
milestones frequently: review diff, applicable checks, commit, push to
origin/research/offline-controller and verify remote identity. Clean only owned
QA tabs/processes. If interrupted, leave a precise durable continuation point.

Return AWAITING_REVIEW with facts, files/evidence, checks, commit SHAs, exact
local/remote identity, clean/dirty state, real remaining gaps and continuation.
Do not automatically start native functionality after this UI task.
