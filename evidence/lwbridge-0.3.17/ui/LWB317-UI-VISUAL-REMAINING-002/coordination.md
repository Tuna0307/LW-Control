# Coordination — LWB317-UI-VISUAL-REMAINING-002

Coordinating worker owns integration, all production/shared files, master documents,
Git index/history, milestone commits and remote verification.

Startup baseline: `6919816b` on `research/offline-controller`, clean and equal to
`origin/research/offline-controller`. Reference executable SHA-256 is
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The archive validator and AFK editor read-only packet pass. Port 4319 is listening
under PID 56880 and is treated as owner-owned; ports 4335, 4336 and 4370 are not
currently listening. No worker may control or stop the owner listener.

## Active bounded workers

- `AFK/Squads inventory`: worker startup failed before work began. Ownership returned
  to the coordinating worker; no child edits or browser/process ownership exist.
- `Automation inventory`: worker recovered useful reconciliation notes but went idle
  before creating its assigned file. The directory was verified empty; ownership is
  returned to the coordinating worker. No child production/Git/browser changes exist.

Milestone-2 follow-up used two independent read-only reviewers. Their original review
files are intentionally preserved as pre-fix findings; coordinator corrections after
those reports close the listed blockers and are covered by the fresh task-local
validator/browser/composition-pair packets. Attempts to wake the sleeping reviewers
for a post-fix pass were temporarily rejected by the worker runtime during the chat
handoff transition, so those historical reports must not be mistaken for the current
production verdict.

The coordinator independently verified all worker-reported facts against parent Unit C
and the exact recovered Automation source before taking over the empty evidence path.
