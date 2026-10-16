# MAP-017 worker report — owner Map feedback round 2

Work item: `docs/work-items/LWB317-MAP-OWNER-FIXES-017.md`. Branch: `codex/map-owner-fixes-017`. Draft PR: #13. **Status: READY_FOR_LEAD_AUDIT**, subject to the stated distinction between the owner-observed original zero and controlled causal reproduction. The reviewed implementation checkpoint is `5abfc7ad`; see the final integrity receipt for the exact final commit and packaged binary identity.

## Zombie Boss — owner reproduction, source cause and correction

The owner reported a real **negative** on server 2212: a Zombie Boss-only manual scan completed 100% in approximately 20 seconds with **zero** results while a live level-80 `喪屍首領` (recommended power 33,000,000) was present. A later separate Zombie Boss scan found 131. This owner observation is the original real-world reproduction; no worker-produced genuine zero-result reproduction is claimed. The deterministic delayed-queue reproduction is explicitly **controlled/inert** (`tools/check_map017_zombie_source_lua.py`), not a substitute for that owner receipt.

The scanner previously took `WorldPointManager.isRecvViewPoints` as sufficient to snapshot *both* point records and Monster/Zombie Boss march records. The game processes the companion `world.march.get` response in another main-thread queue (`WorldMarchDataManager.HandleWorldMarchGetImpl` / `ParseDataWorldMarchGet`). Its `_lastGetBlockMsgTime` reception marker can precede `_lastHandleBlockMsgTime` processing. During a first Boss-only scan, an empty March collection could therefore be classified and committed as a complete but empty selected kind while all 10,000 point AOIs were marked covered. A missing `WorldMonsterSpecialType.MonsterInvasionBoss` discriminator could independently misclassify Bosses as ordinary Monsters.

The MAP-017 source stages the already-captured point rows, waits for the game-native `DateTime.CompareTo` proof that the *new* March reply was received **after** the request and handled **at or after** that reception, and only then reads Monster/Boss records and advances the 63 AOI chunks. It separately records the actual post-point March observation time. An unavailable classifier, missing timestamp, stale/unhandled March reply, or timeout now fails explicitly; a zero Boss source with Boss selected also refuses silent successful publication, including when unrelated kinds have positive rows. The native source never re-reads evictable point rows during this wait. Recovery and game session ownership remain unchanged.

### Fresh packaged Boss-only verification

Each row below used the mounted production Manual selection picker and Start Scan in a separately launched exact-owned Chinese game session on 2212. A read-only independent auditor reconciled the native camera-free source, 10,000/10,000 AOIs, 2,500/2,500 committed SQLite blocks, Boss UUID identity/count, current saved Map UI query and screenshot. Each session ended with exact owned game Stop, unchanged v26 original Lua hashes and no recovery journal. Counts are snapshots of the moving live population, not fixed quotas.

| Fresh-game proof root under `artifacts/map014-takeover/` | Bosses | Auditor | Game session |
| --- | ---: | --- | --- |
| `native-ui-20261016-171615` | 137 | **PASS** | `b09e5090667e45f181c800cb3b0c0ec4` |
| `native-ui-20261016-171849` | 136 | **PASS** | `8451a7fcaca24de7a4b65c2b86f92c89` |
| `native-ui-20261016-172035` | 133 | **PASS** | `d8a55769768642f1a121cb72413c65aa` |

The three source-probe SHA-256 values are identical (`06f3578cc76a7c20e2be4dd499a2a96d26133753056e040bbfdd6e72b5125fe4`), as are their packaged Desktop assembly hashes (`83c94647ff583475934e87888f0377a1e44e93d75d986029e9a6bad3f25503c2`). The native records' actual World March receive/handle timestamps were preserved, with `receivedAfterSend=true` and `handledAtOrAfterReceived=true`; the first proof includes real per-batch game-native `System.DateTime.CompareTo` evidence. This meets the three independent fresh-launch positive gate for the measured current client; it does not prove that intermittent failures can never recur in future game versions.

## Category picker and schedule-off Run now

The Manual and Auto pickers now allow deselecting the final category. With the list empty, the respective Start Scan / Run now button is disabled with a short localized hint. Native configuration distinguishes an absent `selectedTypes` field (legacy defaults) from an explicit empty array (preserved empty choice). A direct native Run now request with empty selection is rejected with `MAP_SCAN_TYPES_REQUIRED`.

The native Auto service admits an explicit Run now even when its schedule is disabled; the one-off captures its requested servers, scans once and returns to the original server without arming `nextRunAt` or enabling automatic scanning. Scheduled cycles continue to defer their first run by the full interval when enabled; Disable still ends scheduled cycles between target servers.

## Controlled checks

- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release` — passed including `MAP017_SCAN_CONTROLS_OK`, `MAP014_ZOMBIE_BOSS_CHECKS_OK`, `MAP016_LAUNCH_SCHEDULE_OK` and existing scanner checks (no game).
- `python tools/check_map017_zombie_source_lua.py` — passed delayed/reordered/absent/unavailable March source and staged-point preservation under Lua 5.3 (inert).
- `python tools/test_map_017_audit.py` — 18 adversarial receipt tests passed; `python tools/map_014_lead_native/test_package_review_candidate.py` — six passed.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check` — passed including MAP017 mounted UI controls and complete localized key checks; `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build` and Windows x64 `dotnet publish` — passed.

## Safety and limits

All live sessions use the isolated task owner, the already-verified exact original official settlement marker (zero updater action), a hidden/background UI, and bounded passive native Map reads. The runner independently audits the process/game identities, original script backup/restore and journal before admitting any proof. No claims, attacks, plunder, marches, relocation, sharing or spending actions were used. MAP-017 item 4 remains dropped by owner; the accepted switch→fast scan→return flow is retained. Historical real owner zero-result reproduction and the controlled timing reproduction are labelled separately; an exact old-build zero-result worker replay is not claimed.

## Additional genuine packaged regressions

All the following are independent, fresh isolated task-owned game/app sessions using the **same** Desktop assembly and native Lua source hashes as the three Boss-first runs. They ran mounted production UI interactions and passed `tools/map_017_audit.py`, including source/release-identity, owned-session and restoration checks.

| Work item | Genuine receipt under `artifacts/map014-takeover/` | Observed outcome |
| --- | --- | --- |
| Item 1, after Resource-only | `native-ui-20261016-172239` | Resource-only completed first, then Zombie Boss-only found **134**; exact native, SQLite and saved UI Boss identities matched, and 10,000 AOIs / 2,500 blocks passed. |
| Item 2, Manual and Auto empty picker | `native-ui-20261016-172507` | **PASS**: after selecting one category, unticking the last item left zero selected, disabled Start Scan / Run now and showed the localized hint. Both snapshots persisted after 2.5 seconds and Auto's native configuration retained `selectedTypes=[]`. Four actual WebView screenshots. |
| Item 3, two-server one-off | `native-ui-20261016-172607` | **PASS**: schedule off, `nextRunAt=0`, Railway selected, explicit one-click Run now scanned **2211 and 2213**, completed two 2,500-block SQLite runs, restored original server **2212**, and left schedule disabled. Actual saved Railway had **1** on 2211, **0** on 2213. The valid empty 2213 had no phantom saved-server option. Screenshots before Run now, after home return, positive saved 2211 and valid-empty 2213. |

The 2211 Railway positive and 2213 verified empty illustrate why saved-server options are based on *retained records*, not merely completed scan history. The one-off workflow does not activate future scheduling. Owner-dropped remote no-switching work was not reopened.

## Final packaged all-nine and release handoff

`artifacts/map014-takeover/native-ui-20261016-172917` passed the **real packaged Manual all-nine** 2212 scan: 10,000 AOIs in 63 native replies, 2,500/2,500 blocks complete, zero failed, source and camera restored, and all nine exact selected kinds with screenshots of every result tab. Saved results were City **4,210**, Resource **8,004**, Monster **14,348**, Truck **318**, Railway **2**, Dispatch **472**, Ghost **0** (valid absence), Treasure **1**, Zombie Boss **140**. The independent `tools/map_017_audit.py --mode all-nine` admitted the exact original script restoration and native provenance. The previously accepted fast Truck/Railway sweep remained operational (625 valid pushes and one repair pass); no fake positive was used to fill Ghost.

The combined independent auditor was run over **all seven separate packaged task-owned sessions**, with its full ignored artifact at `artifacts/map017-final-packaged-audit.json`. It reported `state=PACKAGED_GATES_PASS` and all seven gates true: `threeFreshBossFirst`, `sameFirstScanBuild`, `afterResource`, `allNine`, `emptyPicker`, `scheduleOffTwoServers`, `sameAcceptedBuild`. This receipt checks actual session identities and traces; the auditor explicitly flags that it cannot itself establish an earlier genuine zero-result reproduction or infer the root cause, which are therefore grounded separately above. The old real negative originates from the owner's observed merged MAP-016 test; our controlled delayed March queue reproduces the mechanism, while current corrected live scans gave four distinct positive Boss results.

The local final unmerged ZIP is generated **once** after committing this report and rebuilding the identical tested product code with the final commit's version identity, using `tools/map_014_lead_native/package_review_candidate.py --label MAP017`. Its location, full SHA-256, embedded source commit, CRC and file-byte allowlist audit are in the ignored `artifacts/release-map017-*/release-integrity.json`. The release is an unpublished review candidate; draft PR #13 requires independent lead audit. No merge, release publication or force-push is part of this handoff.
