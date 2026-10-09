# Home completion and branch lifecycle

Owner direction recorded 2026-10-09: finish Home before starting another feature.
The published v0.3.0 Home Launch/Connected/Close and stored Map-data subsets remain
available with their documented limits. They are not full Home or Map acceptance.

Next assignment is LWB317-HOME-COMPLETE-DELIVERY-004. Read its full work item via
`git show origin/research/offline-controller:docs/work-items/LWB317-HOME-COMPLETE-DELIVERY-004.md`.
The worker starts only after manual owner relay and works solo. Create one
main-based Home branch in the existing checkout; earlier linked-worktree paths
are retired and must not be recreated blindly.

For each required Home behavior verify original input/state and observable
outcome against production, then verify actual packaged native operation.
Unknown required behavior and current-game successes without original contract
proof stay incomplete. Lead independently reviews the final diff/evidence and
merges only the explicitly accepted working original-equivalent scope. Whole
Home requires every in-scope obligation, not only a basic launch flow.

After accepted integration, the lead deletes merged local/remote task branches
and retires task worktrees after checking ancestry, activity and preserving
needed local evidence. Preserve unique unfinished commits. Build dependencies
can be regenerated; screenshots/receipts and private local snapshots should be
preserved outside Git where needed, never bundled as product data.

Keep main for delivered product, research/offline-controller for recovery
authority, and at most the current necessary Home feature branch. Technical
identity/isolation/backup/restoration, protected-access limits and explicit owner
pause/stop still apply. This policy itself changes no product behavior.
