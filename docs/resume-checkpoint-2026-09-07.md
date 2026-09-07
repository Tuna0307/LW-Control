# Resumed UI and Daily Free Claims checkpoint — 2026-09-07

## Starting state

The disconnected conversation supplied by the user was read in full. The local
checkout was clean on `research/offline-controller`, HEAD `9fa434b`. No changes
from the interrupted claims investigation had been saved to tracked files.

The committed World Scan v9 acceptance is historical evidence, not a new live
run in this session. See `current-world-map-record-recovery.md`, especially the
uncapped v9 acceptance. The installed game was not started, stopped, patched,
or used to claim rewards during this checkpoint.

## RECOVERED: original UI appearance

Source: `.codex-live/decompiled/LWControl/LastWarControl.App.Ui.overlay.html`.
SHA-256: `d75e86d7cef945929cd3fc6af8d736f4cd3dee028f8d79d52b405edc95486e4a`.
This is the previously extracted UI resource of the supplied original bot,
not the current official game UI. No original scripts or assets were copied
into the rebuild.

The theme-picker array `Pb` declares Cyan, Gold, Indigo, Rose, and Emerald.
The Settings component reads `lwc-theme`, defaults to `theme-indigo`, and stores
changes. The CSS uses these palette values:

| Name | CSS class | Primary 500 | Highlight 300 |
| --- | --- | --- | --- |
| Cyan | `theme-cyan` | `#06b6d4` | `#67e8f9` |
| Gold | `theme-amber` | `#f59e0b` | `#fcd34d` |
| Indigo | `theme-indigo` | `#5e6ad2` | `#aeb5ff` |
| Rose | `theme-rose` | `#f43f5e` | `#fda4af` |
| Emerald | `theme-emerald` | `#10b981` | `#6ee7b7` |

The Indigo picker swatch is `#6366f1`, but the actual theme CSS is `#5e6ad2`.
The rebuild follows the CSS. `overlay-shell`, `app-main-column`, and
`app-topbar` use `#0b0c10`; body text uses `#dce2f2`. Active navigation uses
the primary color with transparency and its 300 highlight.

## Implemented in this checkpoint

- Dark desktop shell with the five recovered palettes and active navigation.
- Working accent selection with automatic persistence alongside the existing
  settings file, in `settings.json.appearance.json`.
- Persistent language selection; Settings and top-bar selectors stay in sync.
- Invalid or unreadable appearance settings fall back to defaults with a log
  message. Claim-policy settings remain in their existing separate file.
- Daily Free Claims has a read-only **View categories** action. Its Run Once
  and Pause actions remain disabled. The category view distinguishes original
  recovery from implementation and does not report current reward availability.
- Export of this desktop session's log through a Save dialog. This is not a
  full export of the original bot's diagnostics bundle.

WinForms opaque surfaces, native widgets, and layout remain an approximation
of the original WebView interface. This is not a pixel-identical reproduction.
The existing interface is only partly translated; persisting a language does
not imply every recovered label has been translated.

## RECOVERED: unresolved original reward categories

Source: `.codex-live/decompiled/Game/LastWarControl.Game.Lua.LWC2DailyFreeClaims.lua`.
SHA-256: `fd96de7215ceb3a88403c32bfb09c5feb5601fbcfbc40e1db41e9b2286f87cd2`,
matching `daily-free-claims-evidence.json`.

Lines 440–443 explicitly add weekly-task and generic login categories as
unconfirmed. Therefore those categories are incomplete in the supplied bot;
their presence in its options is not evidence of a working collector.
The existing recovery notes identify the other five collectors. Only the
Daily Task category has an implemented runtime in this checkout.

## REPORTED in the disconnected conversation; not reverified here

The handoff reports static inspection of the official content-v12 client:

- VIP: a parameter-name interpretation was corrected after examining the
  serializer; do not carry forward the earlier assumption.
- Store: configuration reward identity is local metadata, and daily
  availability comes from authoritative receive-time state.
- Tavern: the original selector's interpretation differs from the current
  client's returned values. This remains the primary compatibility unknown.
- Campaign idle: preview and claim behavior were distinguished, with an
  authoritative post-state update identified.
- Generic login: multiple activity families exist, so a single generic reward
  identity was not established.
- Weekly tasks: a negative symbol search was reported. Such a search is not
  proof that a feature cannot exist under another name.

These are leads preserved from the user-supplied text, not new PROVEN
current-build findings. No additional current-game adapter or live acceptance
is delivered by this checkpoint. No claim is made that the native lwbridge
map trigger or the Tavern selector has been resolved.

## Remaining work

1. Finish remaining UI fidelity and localization against the extracted visual
   reference; retain explicit availability labels.
2. Resolve Tavern semantics through permitted static analysis or documented
   interfaces before treating the old collector as compatible.
3. Keep new claim functionality unavailable until its supported integration
   and before/after acceptance evidence exist.
4. Exact resource gather-end time, remaining missing World Scan details, and
   the native map-scan trigger remain open.

## Validation

- `dotnet run --project tests/LWControl.Core.Checks/LWControl.Core.Checks.csproj --no-restore`:
  **44/44 passed**. These are local checks, not live-game acceptance.
- Desktop Debug and Release builds: **zero warnings, zero errors**.
- Release executable `--smoke-test --smoke-output .codex-live/ui-resume-20260907`:
  **exit 0**. Checks cover every navigation page, each accent button and its
  persistence, language synchronization in both directions, reopening settings,
  preservation of claim settings, seven category rows, and disabled claim actions.
- Corrupt JSON, `null`, unknown accent, and unknown language each exercise
  appearance fallback. The first test exposed an uncaught `InvalidDataException`;
  explicit handling fixed it, and the final smoke suite passed.
- Rendered Home, Settings, Automation, and category-view artifacts are retained
  under `.codex-live/ui-resume-20260907`. Layout inspection corrected inconsistent
  section widths, missing literal ampersands, and unreadable disabled-button text.
  Native tab chrome remains native; `DrawToBitmap` does not fully capture combo
  selection text. Language-selection state is checked programmatically, so these
  images are not complete visual proof of native drop-down rendering.
- An initial pair of simultaneous builds collided on the shared Core output;
  sequential builds passed. Use sequential builds for these projects.

All code changes in this checkpoint are desktop UI changes. Game integration
code, installed packages, and the prior World Scan checkpoint were not changed.
