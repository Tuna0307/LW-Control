# LWBridge 0.3.1 post-login UI reproduction

Updated: 2026-09-08

## Authority

The source is the externally supplied `lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
Its recovered React/Vite frontend is preserved in `evidence/lwbridge-0.3.1/frontend`.
The manifest is checked before generation; `.gitattributes` preserves the asset bytes.

## Implemented

The desktop app now renders the recovered React components in WebView2. The previous
handwritten Overview/Map Data HTML and placeholder pages have been removed. The
original stylesheet, icons, nine language bundles, and every feature chunk are
copied byte-for-byte. Only the main entry and API boundary are transformed.

The normal navigation preserves the target's eight visible entries:

1. Overview
2. Automation — six categories
3. Map Data — manual/automatic scanning and nine data tabs
4. Squads / AFK — tasks and equipment presets
5. City Layout
6. Hotkeys
7. Mini-games
8. Settings

**Correction to earlier static notes:** the shipped main bundle calls
`An(!1, o.state.accessRole)`, so Advanced is hard-hidden even before role evaluation.
The reconstruction preserves that normal navigation. `--view advanced` exposes the
recovered Advanced component explicitly for future feature research.

Login, registration, activation, renewal, unbinding, logout, account dialog, and the
top-bar account button are removed from the active presentation. The desktop has
no authorization screen or auth startup request. Historical extracted code and
screenshots remain evidence, not a login feature in the rebuilt application.

Removing the two-line account button reduced the original header by 2 px. The only
presentation stylesheet override sets `.top-bar { min-height: 54px; }`, preserving
the original desktop feature viewport geometry. Other colors, dimensions, icons,
responsive rules, component structure, and controls use the original assets.

## Display state and feature boundary

`WebUi/preview-host.js` supplies explicit local, stopped/disconnected state. This
is not a live account, game connection, or recreated game backend. The source's
own disconnected states are retained, including the City Layout connection message.
Game-dependent inventories, skins, equipment, map rows, and the connected city
editor are not fabricated. Their populated/runtime states need later recovery.

Navigation, theme switching, nine languages, tabs, filters, switches, and local
configuration work through the recovered components. Settings are stored only in
the rebuild's isolated WebView profile. Game launch, scanning, Lua execution,
authentication, feedback export, and other unwired actions reject at the local
boundary; they cannot report a successful game operation. No native IPC handler is
installed. The WebView host blocks external resources, navigation, and new windows.

The provider keeps the original auto-launch switch's default visual state; it does
not launch a game. The normal eight-item single-profile shell is the default.
`?profiles=2` supports a synthetic profile layout for further visual inspection,
not actual multi-account operation; it is not part of the acceptance matrix below.

## Repeatable build and verification

```powershell
python tools/build_lwbridge_frontend.py
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
./tools/capture_lwbridge_ui.ps1
# Requires Node's playwright package and installed Microsoft Edge:
node tools/check_lwbridge_frontend.cjs
# Requires Pillow:
python tools/compare_lwbridge_frontend.py
```

Generated assets are included with the application source. End users do not need Python,
Node, or an HTTP server to launch the built executable. The desktop serves its
packaged files through a local WebView virtual origin. For frontend edits, change
`local-providers.js`, `preview-host.js`, `presentation.css`, or the explicit generator
transformations; do not edit immutable evidence or generated chunks manually.

Capture options:

```powershell
src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.exe --capture .codex-live/map.png --view map-data --language en --theme dark
```

`--view` supports all eight normal pages and `advanced`. Captures reset a separate
capture-only browser profile and save a PNG plus JSON diagnostics. Ordinary app
preferences are isolated from capture preferences. Capture errors exit nonzero
and write an error file rather than leaving a blocking dialog.

## Verification evidence (2026-09-08)

- Release build: 0 warnings, 0 errors.
- All nine desktop pages captured successfully, with no JavaScript errors or auth requests.
- Eight normal feature views × light/dark × Simplified Chinese/English: 32 paired
  screenshots against the **unmodified extracted frontend** running independently
  with synthetic native responses. All feature text and viewport rectangles match.
- 31/32 feature screenshot pairs are byte-for-pixel identical. The remaining pair
  has mean absolute RGB difference `0.0000034106877310740937` on the 0–255 scale;
  zero pixels differ by more than 30 in any channel. Acceptance threshold is MAE
  <= 0.5 and high-delta pixel rate <= 0.005.
- Nested navigation: all 6 Automation categories, 9 Map Data tabs, and 2 Squad tabs.
- Theme change, hotkey preference persistence after reload, all nine language loads,
  and 900/1440 px layouts checked. Unimplemented auth/game/Lua commands reject.

Full local evidence is under `.codex-live/lwbridge-ui-verification` and
`.codex-live/lwbridge-desktop-captures`. Persistent representative screenshots and
pixel metrics are in `docs/ui-reproduction/`. The CI workflow runs generation
verification, desktop capture checks, browser comparison, and pixel thresholds.

The reference harness uses only extracted static UI code with synthetic state.
It does not log into, patch, or launch the original executable or connect to a
license/game service. This proves recovered-component fidelity in the tested
states; it is not a direct authenticated runtime comparison. Account-dependent
values, populated game states, and platform window chrome remain outside that proof.
