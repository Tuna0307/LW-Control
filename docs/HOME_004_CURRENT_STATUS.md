# Home — one current status and next-action list

Lead inspection/tidy: 2026-10-10. **Whole Home PARTIAL.**
Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control
Branch: codex/home-complete-delivery-004, PR #6 draft.
Inspected committed source: `b91c1e24cdec355376f4216ca0cfa268e3b1235e`.
The documentation tidy commit follows this source; it does not integrate R16.
Current assignment: [Home loop campaign](HOME_004_LOOP_CAMPAIGN.md).

## What the lead checked this time

- Inspected the three dirty diffs and actual current App reorder callback.
  The pending R16 correction projects successfully saved order independently
  of a newer failed request, retaining selected profile and note metadata.
- Inspected the saved R16 actual mounted negative and final controlled EN/light
  and JA/dark JSONs. Both corrected receipts include success->failure and
  failure->success order cases, retained earlier Home cases, two inert owners
  stopped, zero requests/subscriptions and no cleanup failures.
- Executed `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: **PASS** all five
  groups and nine 1,383-key catalogs on the preserved dirty source.
- Executed `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`:
  **PASS**, including repair/adoption, recovery thresholds, held cleanup,
  ordered reconciliation, token routing and stored-data regression. The suite
  records zero genuine game launches.
- No new mounted WebView run, native live experiment, final release package or
  original-runtime comparison was executed in this tidy. Saved worker receipt
  inspection is not independent re-execution or final source/package proof.

This is a checkpoint/documentation audit, **not** whole-Home acceptance.

## Preserved unfinished R16 edits

These were present before the lead tidy and remain uncommitted, byte-preserved.
Do not discard or silently include them in an unrelated documentation commit.

| File | SHA-256 at lead inspection |
| --- | --- |
| src/LWBridge.UI-0.3.17/src/App.jsx | 7CC6458F49B7FD436A1AECC778B7565E4DC9261AB097ADE43C869E09BA1CB700 |
| src/LWBridge.UI-0.3.17/scripts/check-home-integration.mjs | 6D47E125FB75B4369F190F250DA4CF56B1655D3F93564B2BC5A518750E094B7F |
| src/LWBridge.Desktop/LWBridgeWindow.HomeR4Proof.cs | E9601F886E1425A0F6456819EEE34EBC061ED2A6894A7AE87A04A03036E727C5 |

Evidence already available in `artifacts/home-004/`:
- `r16-reorder-success-then-failure-before-fix-en-light.json.error.txt`:
  actual mounted timeout because successful first B,A order never appeared
  after the later requested reorder failed.
- `r16-reorder-both-failure-orders-final-en-light.json` and
  `r16-reorder-both-failure-orders-final-ja-dark.json`: controlled positives;
  genuineGameLaunches=0, mapScanStarts=0. Do not relabel them live proof.

First worker action: verify current source/receipt identity and finish this
candidate, then the single systematic pass in the campaign. Do not restart R4.

## Remaining work, separated by cause

| Category | Concrete next action | What does not close it |
| --- | --- | --- |
| Pending local correction | Integrate verified R16 and complete the finite selection/note/reorder/Start/Stop concurrency pass. | Merely building or adding a source-text assertion. |
| Required native verification | Prioritise genuine still-running offline-only recovery and pending-recovery user Stop; revalidate changed runtime composition and repair/root paths where safely available. | Repeating the earlier successful hang, or using two inert owners as two real games. |
| Original recovery | Trace remaining available 0.3.17 native/asset behavior and exact launcher/transport error/timer producers relevant to H-06–09/H-27–29/H-44/H-46. Record source-backed corrections and limits. | Calling available but undecoded source an absent external dependency. |
| Exact missing original inputs | For a required included behavior, identify the missing protected controller/lease/finalizer input and legitimate available alternative. Historical boundary names a signed package-key.envelope and matching persisted CNG key as absent. Verify applicability before using that claim. | Guessing controller semantics, a generic “needs decryption” report or a protected-service bypass. |
| Genuine client capability | Establish supported independent real installations/capacity before genuine multi-game proof or adverse outdated-build repair. Continue inert architecture checks separately. | Inventing a commercial limit or claiming fixture capacity establishes actual client support. |
| Original conditional comparison | Record the remaining included EN/JA condition whose original behavior/pixels are not known and the exact source or observation needed. | Treating source/local renderer equivalence as protected original-runtime evidence. |

The authoritative finite inventory is H-01–H-47 in
[current obligation queue](HOME_004_R4_QUEUE.md), with original locators in
[contract matrix](HOME_004_CONTRACT_MATRIX.md) and
[later matrix](HOME_004_R1_CURRENT_MATRIX.md). Later receipts may supersede older
statuses. Resolve each row once into implementation, verification, source
recovery, exact missing input, or owner-excluded commercial feature.
Excluded login/licensing/account behavior is not required product scope; a
background dependency matters only where its included behavior is traced.

## Repository tidiness and reporting

At this inspection, local and remote branches are exactly main, research archive
and the active Home branch. There is one worktree. No obsolete branch/worktree
requires removal; keep the incomplete Home branch.

Four overloaded instruction/status documents were copied byte-identically to
dated history files before their current versions were condensed. Detailed
receipts, old negatives, packages and original matrices were not deleted.
Current documents link the history. No product code was changed by the lead.

Use one current continuation, one compact correction receipt and one final
candidate after the ready pass. Do not create another report/ZIP per permutation.
Ordinary failures remain in the diagnose/fix/retest loop. Finish ready work before
returning exact missing inputs; do not falsely declare Home complete. Independent
lead acceptance and approved main merge/publication remain separate.
