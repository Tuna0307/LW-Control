# Milestone D — post-C host reconciliation

Status: **RECORDED / VERIFIED**

This directory contains the fresh post-C host verifier, its frozen result and manifest. Historical evidence remains unchanged; the closeout packet records only current host reconciliation and dependency hashes.

`host-reconciliation.mjs` is derived from the accepted `LWB317-UI-VISUAL-REMAINING-002/milestone-5/run-full-app-current.mjs` host logic, but it is deliberately narrower than the old M5 producer. It does not repeat the accepted finite page-local matrices. Instead it treats those packets as immutable dependencies and rechecks the App/shared-host boundaries that can be invalidated by Milestones A-C.

## Inherited page-local authority

The verifier pins and hashes these accepted sources/evidence at the start and end of a run:

- Map / Scheduled Plunder: `LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/results.json` and `current-results.json`. The inherited authority remains 10,482 Scheduled renders, 51/51 detected mutations, and 75 integrated Map cases. Current `MapDataPage.jsx` is the later accepted corrected SHA-256 `0619C0589FED7A6DA37C7018D021D890A2A2396AC77D21A64E6D77CE348A5BD5`; `ScheduledPlunder.jsx` is `9D6814A70A62C9198720CE15FAA84403F43E1DE172A88E285179151D017B5534`.
- City Layout / Hotkeys / Mini Games / Settings: the accepted Unit E-H `current/inventory.json` packets. Their page-source pins are City `C2C3E9445F607C82954A4319E19C6A8A1BE64368038293D7B1FADC8BE9372061`, shared Hotkeys/Mini `5DAE6E4FFE8875BCEBC366109AD4C1E3FFF690EA17273EBB50226E9107551634`, and Settings `8622BE39E06FE82FCF31BD2E092358987F3F304D447ADA061685727436737771`. The inherited paired-state counts are 30 / 18 / 78 / 63. Milestone D only rechecks host retention, the changed shared `PanelTitle` default through Hotkeys/Settings, required viewport/locale/theme modes, and one browser-observable hidden-effect owner boundary.
- Home: `REMAINING-002/milestone-4/home-source-render/render-results.json`, with `HomePage.jsx` SHA-256 `F140264E693E89F37CBB357470F58FE5DCA46F3DFDA29A8A5D2C7918CE2393FF`. The older Home packet included the pre-C shared UI file, so D does not claim its complete dependency closure is unchanged; D rechecks Home route retention and selected-profile keyed remount through the live current host.
- Shared shell structure: `REMAINING-002/milestone-4/shell-source-render/dependencies.json`, including the accepted current App return-expression anchor (8,525 bytes, SHA-256 `B28E3437E4D3374EC2E574ED09595991F60A42470D65B41C64CD7801A415DB8B`). D exercises the live host rather than repinning the historical shell packet.
- Squads / AFK: closeout Milestone B `whole-afk-composition-results.json`, `join-modal-contract-results.json`, and `mounted-afk-interactions-results.json`. The verifier requires current `SquadsPage.jsx` `A5A56CCDB896AED61C194230C2C7E32156650BFEB948F51A7597FF1092FC55C2` and `RallyJoinSettings.jsx` `1579FA189EB6F5A34F62AF211662CC995FE314C6C13C8681C29A85B6A7A6F11A` to remain the checkpointed Milestone B sources. It rechecks only host/subtab/profile retention; the 30 whole-`I` cases, 14 paired browser cases, Join dialog contract, and 25 mounted AFK interactions are not duplicated.

- Shell/Home ownership: closeout Milestone A `shell-home-corrections-results.json`. `mapBackend.js` remains byte-identical to that packet. Current `App.jsx` differs from Milestone A only by the source-proven Automation `profileId: selectedProfileId` handoff added later in this closeout; D removes exactly that one expression and requires the normalized App bytes to hash back to Milestone A before running the live-current host journeys.
- Automation: closeout Milestone C `conditional-composition-results.json`, `targeted-corrections-results.json`, and `browser-current-results.json`. The verifier requires the 24-case conditional marker, the targeted-correction marker, the current-browser marker, the conditional-to-browser result hash binding, and both C packets' `AutomationPage.jsx` / `sharedPageUI.jsx` dependency hashes to equal live current. This lets D consume the actual post-C authority without hard-coding a pre-C Automation hash.

## Host cases implemented

The verifier is read-only and uses only existing application preview states/providers. It covers:

1. All eight top routes in source order and reverse return under EN/light desktop, JA/dark desktop, EN/dark narrow, and JA/light narrow. Each visited page is tagged in-memory and must retain DOM identity through React `Activity` hiding/return. Narrow modes must not create document horizontal overflow.
2. Automation parent category ownership plus a local editable draft that survives category hiding and a top-route round trip. No Automation run/action provider is invoked.
3. Squads AFK ↔ Equipment ownership. An AFK editor draft survives Equipment and a top-route round trip. The live Equipment Alt-key listener must add exactly one browser-observable `window.keydown` owner while visible, clean it while hidden, and restore exactly one owner on return.
4. Representative Map host behavior: a non-default data tab, keyword, quality/plunderable filter, Manual/Auto selection and 75-minute local Auto interval survive ordinary route hiding; the existing preview keeps Run Auto fenced. A separate `map-city` journey proves 52 rows, page 2 and route-return retention. A `map-scheduled-populated` journey enters Scheduled through the real Map tab and requires all three recovered groups/tables without clicking any action.
5. Profile ownership: an uncached profile switch must cross the live loading boundary and remount the keyed Home subtree while preserving the App-owned top route. A later cached profile return must bypass loading; App-owned Automation category, Map data tab, and Squads subtab persist, while Map child-local keyword/filter/Manual-Auto state resets and the replacement profile reads its own Auto interval.
6. Home host behavior: ordinary top-route hide/return keeps the same Home instance; profile replacement remounts Home through the keyed page boundary.
7. Shared shell behavior: one header, one profile sidebar, eight navigation buttons; Cross-server popover open/focus/Escape-inert/outside-close behavior; profile note dialog focus containment and focus restoration; live EN/light → JA/dark change while a retained Map instance remains mounted; idle/busy exit dialogs remain shell siblings and honor their Escape fence.
8. Hidden-effect ownership: City Layout and Squads Equipment remove/reinstall their `window.keydown` owners across `Activity` hide/return. Mini Games and Settings additionally remove and reinstall their page-owned one-second intervals while hidden.

These are host reconciliation cases. They do not replay Map's 10k differential, the full Map 75-case matrix, Units E-H finite state spaces, Equipment's lower source replay, or Milestone B's whole AFK matrix.

## Source-closure and dependency fencing

The verifier copies the accepted M5 local-import closure collector into this new closeout file so historical scripts remain untouched. Before opening the browser it records:

- every served App dependency path and SHA-256;
- a SHA-256 digest of that complete closure;
- every referenced inherited proof-file SHA-256;
- the fixed inherited live source pins listed in the script;
- the verifier's own SHA-256.

After all browser contexts close, it recomputes the full served App closure and inherited proof hashes and requires exact start/end equality. A source edit, import-closure edit, or inherited evidence edit during the run therefore fails the verifier rather than producing a mixed checkpoint.

## Execution contract after Milestone C settles

The script assumes the coordinator already owns a current UI server. It never starts or stops one. Default URL is `http://127.0.0.1:4391/`; another existing port can be selected with `--port=<port>` or `LWB317_MILESTONE_D_PORT`.

The accepted local Playwright package and Chrome locations are defaults. They can be overridden with `LWB317_PLAYWRIGHT_PACKAGE` (path to a `package.json` usable by `createRequire`) and `LWB317_CHROME_PATH`.

Recording is explicit and verification is rewrite-free:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-d/host-reconciliation.mjs --record --port=<owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-d/host-reconciliation.mjs --verify --port=<owned-port>
```

`--record` writes only the task-owned `host-reconciliation-results.json` and `host-reconciliation-manifest.json`; `--verify` rewrites nothing. Verification rejects drift in the full source closure, inherited proof files, live source pins, Node/Playwright/Chrome identity, verifier bytes and recorded result hash.

## Preconditions for the eventual D run

- Milestone C is stable and its final Automation/shared-UI/config-store result hashes are frozen into the D manifest.
- The current UI served on the selected port corresponds to the same workspace source closure the verifier hashes.
- The checkpointed Milestone B Squads/Rally files and inherited Map/E-H/Home/shell pins have not changed. If one changed intentionally, D must stop and reassess whether the corresponding page-local proof is still inheritable instead of refreshing the old hash mechanically.

No native Last War action, updater action, server jump, Map scan/plunder action, OS hotkey action, or protected-original runtime is required or invoked by this host verifier.
