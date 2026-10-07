# Lead preflight — owner resumed bounded live work

Date: 2026-10-07, approximately 19:40-19:45 Asia/Singapore.
Owner answer: "Resume bounded live testing and desktop control".
Scope: only the lead-reissued HOME-MAP-LIVE-PILOT-001.

- Direct delivery HEAD/origin: dfc7b1dcad3b82842527d460cc07e7caa0d35c58; tree clean.
- Filtered process-name/path inventory found no running LastWar/LWBridge at the
  lead preflight instant. Re-inventory immediately before launch; this is not a
  durable ownership guarantee. Do not reuse a later owner session.
- Current-client runtime static contract reran successfully, saved locally at
  `C:/Users/chimw/.codex/tmp/lwb317-live-preflight-runtime.json`.
- Desktop Commander get_config succeeded on WeiXuan; version 0.2.52. Registered
  tools cover filesystem/process/shell. No assumption it can capture native UI.
- Windows-MCP stdio discovery succeeded: 21 actual schemas, no guessed inputs.
  ControlStatus returned ready. First Snapshot returned USER_CONTROL with about
  10 seconds remaining; no takeover bypass was attempted. Later Snapshot succeeded.
- Screenshot display=[0], use_annotation=false succeeded via dxcam at 1920x1080.
  The saved image was decoded/visually inspected: the left display showed desktop
  wallpaper/taskbar, without a game/clone window. Evidence retained outside Git:
  `C:/Users/chimw/.codex/tmp/lwb317-live-preflight-display0-20261007.png` and the
  adjacent screenshot-result JSON. No app focus/input was performed.
- Display metadata: display 0 bounds (-1920,0,0,1080); primary display 1 bounds
  (0,0,2560,1440). Never reuse coordinates without a fresh observation.
- Snapshot returned no visible app windows. That is a tool observation, not proof
  that the primary display has no owner application. Window/input capabilities
  still need actual target observation during the pilot.

## Mandatory isolation prerequisite

Program.cs has no explicit normal-production data-root option. LWBridgeWindow
defaults the registry/profile/Map paths to LocalAppData/LWBridgeRebuild and
LocalConfigStore likewise defaults there. Existing campaign isolation selects
inert providers. Therefore a normal production launch cannot yet satisfy the
pilot's isolated-root requirement merely by supplying the old proof flags.

Before launch, implement and prove one explicit isolated root wired through real
normal production composition, including config/registry/profile Map/runtime/
evidence/backups/WebView storage and cleanup. Default product paths remain.
Review existing lifecycle injection defaults for additional owner-root leakage.
Build and test this prerequisite before any game or installation mutation.

No game/clone was launched, no owner session controlled, no game file altered,
no protected original service accessed and no LIVE_PROVEN promotion occurred.
