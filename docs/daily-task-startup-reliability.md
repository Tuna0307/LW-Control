# Daily Task runtime startup reliability

## 2026-09-06 investigation

One normal Last War launch was observed with the persistent Daily Task runtime
installed but its heartbeat remained stale. The installed script package still
matched the verified runtime package exactly, the launcher reported content
version 12 and the expected modified package size/CRC, and the game process was a
fresh process. No Daily Task command was sent while the heartbeat was stale.

The launcher log around that anomalous launch contained a distinctive earlier
state:

`Prepared game relaunch: pid=2992, outcome=ExitedGracefully`

That state has not been reproduced. Starting the launcher while an already healthy
game was running only found and focused the existing Last War window; it did not
replace the game process.

`tools/run_daily_task_startup_reliability.py` was added as a claim-free regression
check for the persistent install. It refuses to run when a Daily Task command file
exists, never creates a command, verifies the installed runtime against its
manifest, requires a newly advanced heartbeat, and verifies all protected game
hashes remain unchanged after every cycle.

Eight clean cold-start cycles were exercised after the anomaly. All eight loaded
the same `lwcontrol-daily-task-runtime-1` package and registered through
`UpdateManager.AddUpdate`. The heartbeat appeared approximately 13.7 to 15.0
seconds after launcher start in the five-cycle repeat run. Across the tests there
were zero created Daily Task commands and zero reward sends, and the installed
`LWScripts.data`, `LWScripts.txt`, `version.txt`, and `BaseUtils.rdl` hashes remained
unchanged.

Current classification: the earlier missed startup is **not reproduced and its
root cause remains unknown**. Cold startup of the currently installed runtime is
repeatably proven across eight consecutive claim-free cycles. The runtime client
continues to fail closed whenever the heartbeat is missing or stale, so an
unexplained future startup miss cannot submit a claim.

Reproduction command:

```powershell
python tools/run_daily_task_startup_reliability.py --cycles 5 --timeout-seconds 60 --json
```
