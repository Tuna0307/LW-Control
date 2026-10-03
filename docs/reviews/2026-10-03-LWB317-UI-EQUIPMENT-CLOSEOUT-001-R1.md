# Equipment Schemes R1 correction worker delivery — 2026-10-03

Status: **AWAITING_REVIEW**. This is the bounded worker correction for
`LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1`. Implementation/evidence milestone:
`2e4accef7b0709a903034293126cb63fe389b105`. The parent
`LWB317-UI-EQUIPMENT-CLOSEOUT-001` remains **CHANGES_REQUIRED** until the project
lead reviews this packet.

## Corrected behavior

Visited AFK and Equipment content now uses installed React 19 `Activity` with
`visible`/`hidden` modes. The hidden component retains selected/dirty/renamed/
moved state while its effects are cleaned up. Mounted browser proof shows Equipment's
Alt shortcut active while visible, absent and non-preventing while AFK is visible,
then restored as exactly one listener after return. Rename dialog state survives the
hidden interval while the native dialog effect closes/reopens correctly; toast
cleanup pauses across the hidden interval and restarts on visibility, the 450 ms
drop cleanup completes without stale highlighting, and an AFK Escape effect is
likewise suspended and restored.

Equipment rename now shares a controlled offline acknowledgement path with the
save-only consumer. Blank and unchanged-clean behavior stays source-shaped. Changed
or already-dirty drafts enter busy `rename`, pending acknowledgement keeps the
dialog disabled/open, success confirms the full draft and closes, and rejection
clears busy while retaining the dialog. The recovered Equipment-prefixed save error
shows Retry and Discard; Retry reuses the same draft flush and Discard restores the
last confirmed draft. No native provider or live success path was added.

## Executable and browser proof

- `check-r1.mjs --record`: **61/61** focused assertions across Activity retention,
  actual Alt effect cleanup/reinstall and exclusions, recovered/current rename
  acknowledgement states, full-draft dirty rename, guards and save-only reuse.
- Parent Equipment `check-closeout.mjs`: **105/105**.
- Affected Automation/AFK `check-closeout.mjs`: **729/729**; exact Join and Assist
  renderer recovery checks pass.
- `npm.cmd run check`, `npm.cmd run build` and
  `npm.cmd run check:production-build` pass. Production build/package markers are
  `0b034f19f3b214a018d6f984d3291570aed6d6b76067c2aac22d303a088b5ee7`
  and `37f525ebfdc557a83c8e2f128cfa5e08230956f057b30fad37d0a59a587303fc`.
- Real mounted browser QA at 1920x855/DPR 1 covers pending/error/success in
  English/light and Japanese/dark, Equipment -> AFK -> Equipment keyboard/lifecycle,
  retained moved/renamed state, dialog/timers and affected AFK Escape handling.
  Fresh console capture contains zero warnings and zero errors.
- Inspected screenshots:
  `rename-pending-en-light.jpg` SHA-256
  `6A9ED4045DB4C99361EBE3B88B843053A55FE13DC40F43BDECCD8083592D8725`;
  `rename-error-ja-dark.jpg` SHA-256
  `AA6B732369947D9491A7C3303BB2D5E95CFF5BC77141ED5F5C204D11FDA60067`.

## Evidence integrity and preservation

`validate-evidence.mjs` passes **71 assertions**. It verifies the reference
LWBridge 0.3.17 EXE hash, recovered frontend asset hashes, exact Activity/Equipment/
rename/error byte slices, corrected current production/evidence hashes, browser
JSON/screenshots, the immutable submitted failure packet and the seven protected-WIP
paths. The lead failure checker/results remain byte-for-byte unchanged. The parent
`source-manifest.json` intentionally retains its historical pre-R1 current
`Pages.jsx` hash; the R1 validator owns the corrected current hash instead.

The protected-WIP guard passes with all seven paths unchanged. Existing Map WIP,
`previewAfkFixtures.js`, `.scratch-lwb317` and CORRECT-003 screenshots remain
outside this delivery and unstaged.

## Limits and continuation

This is source/local offline-preview proof. It does not prove native Equipment
providers/persistence/gameplay, physical connector-driven HTML5 drag, Last War
runtime behavior, or original post-auth pixel equivalence. No City Layout, Hotkeys,
Mini Games, Settings, shell/native phase, login/account/licensing work or next page
was started.

Exact continuation: project-lead review of this R1 packet. If accepted, the lead may
update the parent Equipment decision separately. This worker stops at R1.
