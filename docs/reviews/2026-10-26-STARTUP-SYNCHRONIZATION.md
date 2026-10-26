# Saved features initialize independently of feature pages

Owner reports needing to visit every tab/sub-tab after launching. Continue
`codex/lwtool-remaining-tabs` / draft PR #17; no main merge or release.

## Cause and change

Feature views are lazy/retained React pages, with further lazy Automation
categories. Several native configuration/status calls were view effects. App
also had a one-shot Automation and visual-metrics synchronization on connection
whose failures were swallowed until the next connection transition. Chat already
had a profile-owned background poll; Map Auto and native hotkey/chest monitoring
also already had non-page owners. Thus loading every page was not a reliable
startup contract, and mounting more pages was not an appropriate fix.

`Feature018RuntimeStartup` now runs in each retained profile runtime, using the
existing three-second background status loop. It synchronizes trusted Automation
and Chat configuration and saved visual settings, and initializes saved-enabled
Monster AFK, Stamina and Alliance Drill controllers. Each subsystem retries
independently after initialization failure. A late disabled-to-enabled saved
snapshot is noticed without visiting a page. Completed/stopped AFK work is not
reset by initialization; same-owner stamina/Drill configuration preserves their
native pending/counter history. No run-now command or gameplay request is replayed.

The coordinator captures the full profile/session/challenge/PID/path/start-time
and fresh authenticated UID/server. Each mailbox exchange is independently pinned
to that exact captured owner. A retired completion cannot continue into a new
session. Startup diagnostics and cached Chat data are also fenced to that owner.
Disabled settings stay disabled. Manual Map scans, City moves, Equipment changes
and one-shot Mini Game operations are not automatically dispatched. Their existing
providers and the existing Map Auto/hotkey/chest owners remain intact.

The saved settings for Stop are now explicitly injected into the native Stop
envelope as well as Configure/Status, completing the managed-wire side of the
earlier latest-checkbox fix. A distinguishing managed test checks that envelope.
Disabled AFK configuration is checked before the fresh-engine Start branch,
preventing stale startup work from starting an engine after a saved Off.

## Controlled checks

`Feature018RuntimeStartupChecks` exercises the actual coordinator without a UI:
no owner/no requests; all saved-enabled groups; all-off/no Start; isolated failure
and retry; same-session interruption without duplicate Starts; completed AFK;
owner retirement during an await; and a delayed saved-enabled snapshot. Existing
native Stamina/Drill/AFK fixtures retain their pending/no-replay checks. Published
Automation status reads the coordinator's fenced Chat snapshot at consumption,
so an owner change hides it immediately before the next background poll; a
distinguishing service-level test covers both the current and retired owner. Full UI
and Desktop delivery suites and the Gather lease/callback regressions pass.

## Genuine Home-only diagnostic

Native run `artifacts/map014-takeover/native-ui-20261026-121331` started directly
on Home. No feature page or category was opened. All six synchronization groups
reported applied/ready with no errors; disabled controllers were not started.
After exact approved-account admission and an idle-squad check, the isolated
profile persisted one enabled Gathering squad. No explicit Configure/Run command
or feature-page polling was used to activate it: only the background coordinator
could apply that saved change. Native read-only observations then witnessed its
new exact tracked march. Stop recalled it, and native follow-ups confirmed home.
Owned exit restored all three original game files and left no recovery journal.
This uses the owner's existing narrow main-account gathering/recall test approval;
no other main-account gameplay was tested.

An earlier read-only preparation (`20261026-121030`) synchronized successfully
but its proof assertion compared the Home button's localized text with English.
It ended/restored without sending a gathering trip. The proof now checks the
selected navigation element structurally, independent of language.

The Home-only proof is explicit isolated opt-in; it starts with every background
task off and admits the exact account before the optional single gathering test.
The normal production path contains no proof opt-in or fixed account number.

Final package source, genuine packaged proof, integrity and remaining limits are
recorded below after source pinning. Raw account/march/session receipts remain
only in ignored local artifacts.
