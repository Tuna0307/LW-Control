# LWB317-UI-001B — runtime visual shell/navigation baseline

Date: 2026-09-29  
Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`  
Reference SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Result

Runtime post-auth shell/navigation observation is `BLOCKED` by the explicitly
out-of-scope LWBridge authentication/licensing boundary.

The exact 0.3.17 reference was launched once with Chat On Steroids Desktop
after confirming that no LWBridge or Last War process was already running. The
initial window presented the authorization/login surface rather than the
post-auth application shell. No login, registration, activation, unbind,
credential, entitlement, gameplay or feature action was invoked.

The worker therefore did not attempt to bypass the boundary and did not claim
runtime visual proof for the recovered post-auth navigation.

## Source identity and commands

Reference hash was verified immediately before launch with:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath \
  'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe'
```

Observed SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

The reference was launched through Chat On Steroids Desktop using that exact
path. The worker used Desktop only to launch, observe, activate for capture and
close the reference window.

## Direct runtime observation

Evidence state: runtime-observed boundary; post-auth shell `BLOCKED`.

Directly observed initial state:

- window title: `lwbridge`;
- selected language: `Chinese (Simplified)`;
- visible boundary title: `LWBridge 授权`;
- visible explanatory text: `使用账号登录，或通过授权码注册/激活。`;
- visible authorization tabs/actions include `登录`, `注册/激活`, and
  `解绑电脑`;
- visible version text: `v0.3.17`;
- no post-auth top-level navigation entries were visible in the accessible
  state.

Credential field contents were deliberately excluded from durable evidence.
The worker did not read, change or submit credentials.

## Window geometry

Geometry was measured from the live reference window handle
`0x0228207C` / decimal `36176636` during this observation session.

- Win32 `GetWindowRect`: `(52, 52) - (1188, 811)` = `1136 x 759` pixels.
- Win32 client rect mapped to screen: `(60, 83) - (1180, 803)` =
  `1120 x 720` pixels.
- Chat On Steroids Desktop screenshot frame: origin `(59, 52)`,
  `1122 x 752` pixels.

The screenshot-frame size differs from `GetWindowRect` because the two APIs
include/exclude different non-client/shadow extents. Both measurements are
recorded rather than forcing them into one value.

## Durable screenshot

Stored evidence:

`evidence/lwbridge-0.3.17/ui/shell-navigation/initial-auth-boundary-redacted.png`

- PNG dimensions: `1122 x 752`;
- SHA-256:
  `56E103922A95351AA7FC6E646D64C9EF31CB88DE2915B3C61810EA0F42D605A9`;
- direct capture of the initial reference window;
- only the already-populated username/password value areas were redacted
  before writing the durable file;
- all surrounding labels, controls, layout, language selector and version text
  remain visible.

Because of those redactions, this PNG is runtime visual evidence but is not
claimed as an `EXACT_BYTES` screenshot of every original pixel.

## Static comparison to UI-001A

`LWB317-UI-001A` established `EXACT_BYTES` static navigation definitions for:

1. Home
2. Automation
3. Map Data
4. Squads / AFK
5. City Layout
6. Hotkeys
7. Mini Games
8. Settings

It also established a conditional `advanced` entry immediately before
Settings, while its runtime visibility and exact English visible label remain
`UNKNOWN`.

None of those post-auth entries was reachable in this runtime session without
crossing the excluded auth boundary. Therefore their static order/labels remain
valid `EXACT_BYTES` evidence, but runtime geometry, icons, selected/hover state,
conditional Advanced visibility and post-auth shell visuals remain
`BLOCKED` / not visually validated.

## Acceptance and limits

- reference hash verified before launch: **pass**;
- initial accessible boundary captured: **pass**;
- accessible top-level navigation checked: **none accessible**;
- selected-state appearance for post-auth navigation: **BLOCKED**;
- no Last War process launched: **pass**;
- no auth bypass/reconstruction: **pass**;
- no backend/function recovery: **pass**;
- reference instance launched by the worker was closed after capture: **pass**.

The campaign may continue with its explicitly authorized static/offline page
inventory stages. This result does not authorize login/auth work and does not
turn static frontend evidence into runtime visual proof.

