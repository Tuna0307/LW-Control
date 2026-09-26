# LWB-R7-143 — Built Release normal-user restart walkthrough

**Date:** 2026-09-22
**Base revision:** `817c4bca4441d212104e90289e0b3bf27f5292b7`
**Acceptance case:** F04
**Status:** PASS_CURRENT_NORMAL_USER

## Goal

Close F04:

> Packaged app restart after completion -> normal user flow works from the built executable without relying on application test modes or manual console commands.

R7-143 exercises the actual Release executable and the shipped sidebar controls.

No production behavior change was required.

## Verification boundary

The available remote connector still has no native screenshot/mouse/keyboard API. R7-143 uses Windows `user32` foreground mouse input as the external input mechanism.

That external automation stands in for a human hand, but the application under acceptance is not placed into a test flow:

- both acceptance launches use **zero application arguments**;
- no `--view` shortcut is used;
- no capture/probe/replay mode is used;
- no backend command is invoked directly by the verifier;
- no DOM script is injected into the acceptance runs;
- the existing Presentation profile is not isolated between run 1 and run 2.

A separate read-only calibration launch uses `--owner-evidence` only to prove the physical Map Data click coordinate reaches the real Map Data path. It is not one of the two acceptance runs.

## Maintained verifier

`tools/check_normal_user_restart_walkthrough.ps1`

Worktree SHA-256:

`CEDD2EDFFB5C2159300A6116E49ADE29839F96EF5915A857B6818F5E26E73DC4`

Default durable output:

`.codex-live/normal-user-restart-walkthrough.json`

## Physical click calibration

The shipped single-profile layout has a 164px side navigation rail. Map Data is the third 38px navigation button.

The verifier uses:

- Overview client coordinate: `(94,143)`
- Map Data client coordinate: `(94,229)`

The read-only calibration launch clicked `(94,229)` and recorded:

- `session-start`
- `owner-command-blocked` for the read-only history-import side effect
- `city-search-response`
- `city-render-observation`
- `session-end`

Thus the click coordinate is correlated to the actual production Map Data saved City search/render path.

## Acceptance run 1 — zero arguments

Release executable SHA-256:

`5415D9179ED8AA0F9A94122E12A832969590F517B88CE3948936A29E5D5E897C`

Process ID: `40568`

Launch arguments: **none**

Initial Overview capture:

- 1120 x 720
- active Overview indicator pixel: `(0,113,227)`
- Map Data indicator pixel: `(235,235,237)`

After physical Map Data click:

- active Overview indicator pixel becomes non-blue `(255,255,255)`
- active Map Data indicator pixel becomes `(0,113,227)`
- rendered capture SHA changes from the Overview capture

After physical Overview click:

- Overview active indicator returns to blue `(0,113,226)`
- Map Data indicator returns non-blue
- rendered capture changes again

The window then closes through normal `CloseMainWindow`.

## Restart boundary

After run 1:

- the first desktop process exits normally;
- no LWBridge desktop process remains;
- the existing Presentation profile is retained rather than replaced by a fixture profile.

The executable is then started again normally.

Run 2 uses a different process ID: `8696`.

## Acceptance run 2 — zero arguments after restart

Launch arguments: **none**

Initial Overview:

- active Overview indicator `(0,113,226)`
- Map Data indicator non-blue

After physical Map Data click:

- Overview indicator non-blue
- Map Data active indicator `(0,113,227)`

After physical Overview click:

- Overview indicator returns `(0,113,226)`
- Map Data indicator returns non-blue

The second window also closes normally.

## State integrity

Across calibration + both acceptance runs:

- `config.json` SHA-256 unchanged
- `config.backup.json` SHA-256 unchanged
- installed Last War package identity unchanged
- installed package version remains 20
- final LWBridge desktop count: 0
- final Last War count: 0
- final launcher count: 0
- final Overview helper count: 0

Installed package hashes remain:

- `LWScripts.data`: `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`
- `LWScripts.txt`: `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`
- `version.txt`: `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`

## Safety

The walkthrough performs navigation only.

It does not invoke:

- map scanning
- Follow
- attack or plunder
- claim or collection
- messaging

The current profile has `gameRoot=null`, so normal startup cannot launch Last War from this profile during the F04 walkthrough. F04 itself is a desktop restart/navigation acceptance case, not a live-game population gate.

## Prior authorities

R7-143 composes with, but does not duplicate:

- **LWB-R7-128** — normal Release window responsiveness and close-during-start owned-process cleanup;
- **LWB-R7-135** — normal production-service composition and saved City search/render smoke, explicitly without a click-through.

R7-143 supplies the previously missing physical user navigation + normal close + restart + repeat path.

## Acceptance effect

**F04 -> PASS_CURRENT_NORMAL_USER**

After R7-143 the 47-case matrix contains **zero ordinary `partial` rows**.

Population-, authorization-, implementation- and owner-retired categories remain explicit and are not promoted by this finding.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-normal-user-restart-walkthrough.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7143.json`
