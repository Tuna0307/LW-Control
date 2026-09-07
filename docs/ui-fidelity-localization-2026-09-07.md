# Desktop UI fidelity and localization — 2026-09-07

Scope: complete item 1 of the resumed checkpoint in the existing WinForms
desktop. Claims adapters, Tavern research, World Scan semantics, installed
game files, and game actions are outside this change.

## Recovered reference evidence

Read-only source: `.codex-live/decompiled/LWControl/LastWarControl.App.Ui.overlay.html`.
SHA-256: `d75e86d7cef945929cd3fc6af8d736f4cd3dee028f8d79d52b405edc95486e4a`.
No original executable or embedded JavaScript was executed for this recovery.

- The actual navigation array orders Home, Automation, Map & Data, Squads & AFK,
  Hotkeys, Settings. Its Chinese captions are 首页, 自动化, 地图数据, 小队/挂机,
  快捷键, 设置. Earlier notes listed a different order; the array is authoritative.
- Navigation has numbered entries and a brand above it; desktop sidebar width
  is 192 CSS pixels. The final top-bar desktop rule is 70 pixels high.
- Feature cards use two columns, a single-column responsive variant, 12-pixel
  gaps, 16-pixel padding, 8-pixel corners, and a 190-pixel minimum height.
- The Hotkeys view has six shortcut groups/cards. Keys remain literal keyboard
  identifiers; their names, descriptions, conditions, and availability translate.
- The reference contains paired English/Chinese feature names, descriptions,
  actions, navigation, and settings captions. The display-text extractor recovers
  717 generic pairs plus 189 feature-scoped entries (906 total).
- Identical English action captions are context dependent. For example,
  `secret_mobile_squad` uses 派遣 for Dispatch, while `mining_dispatch` uses
  派遣采集. The desktop uses scoped entries for every feature caption/action.

Reproduction (display text only):

```powershell
python tools/extract_reference_ui_text.py .codex-live/decompiled/LWControl/LastWarControl.App.Ui.overlay.html src/LWControl.Desktop/ReferenceUiStrings.json
```

The extractor reads literals; it does not evaluate scripts. Tests cover
context collisions, escaped display text, and ignoring executable expressions.

## Completed desktop behavior

- Full-height branded sidebar in recovered order, numbered navigation, and a
  page-specific top-bar title.
- Dark tab strips with preserved page instances; responsive feature grids,
  rounded cards, wrapped descriptions/actions, and six Hotkeys cards.
- Home displays the real implementation inventory: 42 features, one available,
  one partial, 40 pending. These are implementation counts, not live readiness.
- English and Simplified Chinese cover every static desktop control, all 42
  feature names/descriptions and 105 action labels, Home, map toolbar/detail labels, column
  headers, Hotkeys, Settings, category-status dialog, availability badges,
  appearance choices, and known runtime status labels.
- Display text recovered from the original is separated from rebuild-specific
  translations in `DesktopUiStrings.json`. New explanatory text is authored
  for this rebuild and is not presented as an original artifact finding.
- Switching language preserves active page/subtab, claim category selections,
  preview plan/creation time, search text, and selected map point. It does not
  create a fresh plan or refresh old evidence implicitly.
- Map columns retain readable headers at the minimum window size and scroll
  horizontally instead of squeezing all eleven fields into narrow columns.
- Game-supplied names, IDs, coordinates, resource identities, and source strings
  remain verbatim. Unknown diagnostic codes/messages remain verbatim; existing
  session-log entries retain their original language as historical evidence.
- Every unavailable feature action remains disabled. Availability is conveyed
  with words in both languages, as well as distinct badge colors.
- Region/Evidence placeholders and unimplemented settings/hotkeys retain their
  existing disabled behavior. No new game-facing functionality is enabled.

This completes the desktop shell/localization pass, not a migration to the
original WebView renderer or implementation of pending feature configuration
and game behavior. Native window chrome, scrolling, and combo-box popups retain
Windows behavior; the WinForms cards approximate the original CSS surfaces.

## Verification

The Release UI smoke suite checks translation coverage against the actual
control tree and every feature-scoped caption. It also checks that availability
gates and plan/map/navigation state survive repeated language changes, and that
a game-provided name equal to an English UI word is not translated.

Rendered artifacts are in `.codex-live/ui-localization-20260907`, covering all
six pages in both languages at 1320×840 and the supported 1050×700 minimum,
plus the category dialog. `DrawToBitmap` omits native combo selection text;
the selected values and language synchronization are verified programmatically.
These are UI-only artifacts, not evidence of live game actions.

Final outcomes:

- Release desktop build: **passed, zero warnings and zero errors**.
- Release executable `--smoke-test --smoke-output .codex-live/ui-localization-20260907`:
  **exit 0**. Coverage includes all 42 features and 105 scoped action captions,
  all static controls, both languages, category/navigation/map/preview state,
  literal game data, disabled actions, and minimum map-header widths.
- `missing-translations.json`: **empty list**.
- `python -m unittest tests.test_reference_ui_text -v`: **3/3 passed**.
- Existing Core checks (`--no-restore --no-build`): **44/44 passed**.
- `git diff --check`: passed.

The final screenshots were inspected for Chinese feature/action text, shortcut
cards, category status, and minimum-width map headers. No game action was sent.
