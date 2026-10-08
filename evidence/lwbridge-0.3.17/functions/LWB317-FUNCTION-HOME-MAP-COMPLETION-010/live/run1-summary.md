# Live run 1 (2026-10-09 02:15-02:21 +08) — task-owned isolated root ISO1

- Gate: preflight-1.json (compat ok, 0 processes, 3 originals copied+hash verified).
- Packaged Release app 1.0.0+aea1f15d, `--isolated-root ...\LWB317-COMP010-20261009-ISO1`,
  host PID 65992 (created 2026-10-08T18:16:07Z). New profile defaulted Auto Launch ON ->
  one real game launch: PID 65196, created 2026-10-08T18:16:42.5422114Z, path = installed LastWar.exe,
  session 76cf2c5496954b4ab50f9641d65e4fe2. Home showed Connected / game running (zh-Hans, light):
  run1-connected-zh-light.png. Real-launch count this task: 1.
- Desktop input: first Windows-MCP Click -> transient CONTROL_PREEMPTED (waited, re-observed ready);
  the retry was DENIED by the Claude Code auto-mode classifier. No input workaround attempted;
  screenshots (capture) were allowed. => language switch, theme toggle, Home Stop click and the
  Map pilot UI steps were NOT performed in this run.
- Cleanup: host closed with Process.CloseMainWindow (graceful, exit confirmed). Owned exact-identity
  Stop through run_overview_bridge_current.py was then attempted (first call: empty challenge,
  rejected; second: "active Overview recovery journal is missing"). At that point no LastWar process
  existed and the three installed script files already matched the preflight SHA-256 values.
  Who removed the journal/exited the game between host close and the helper call was NOT proven
  (heartbeat last updated 02:20, pipe adapter state error:host_lease_stale). Recorded as unexplained
  but safe end state. No restore was ever run underneath a live game by this task.
