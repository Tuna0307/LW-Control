# WebView2 reference UI verification — 2026-09-07

This checkpoint completes the renderer migration for the rebuilt desktop. It
separates recovered reference evidence from rebuild-specific safety behavior and
does not use the UI smoke suite as evidence for live game execution.

## RECOVERED

- The preserved Build 189 client uses WebView2 and ships
  `LastWarControl.App.Ui.overlay.html` as its application UI resource.
- The recovered resource SHA-256 is
  `d75e86d7cef945929cd3fc6af8d736f4cd3dee028f8d79d52b405edc95486e4a`.
- The resource contains the six top-level destinations, all 42 recovered feature
  descriptors, English and Simplified Chinese text, five themes, and the original
  responsive CSS/breakpoints.

## IMPLEMENTED

- Normal desktop launch now hosts the recovered resource in WebView2 through
  `ReferenceWebViewForm`.
- The exact recovered HTML is embedded in `LWControl.Desktop`; it is not a
  hand-written recreation of the React/CSS shell.
- `--legacy-ui` keeps the previous WinForms reconstruction available as a
  fallback.
- A rebuild capability layer is added after the recovered UI mounts. It marks
  implementation state and fails closed on execution paths that are not proven:
  `World Scan` is AVAILABLE, `Daily Free Claims` is PARTIAL, and all remaining
  execution surfaces stay PENDING unless separately proven.
- The only enabled Daily Free Claims execution action is the recovered
  `run_once` route relabelled `Daily Task only`; the host independently gates it
  to the proven Daily Task runtime. The other Daily Free Claims actions remain
  disabled.
- Fixture smoke mode blocks every game command and never launches Last War.

## VERIFIED

The final fixture-only WebView matrix is under
`.codex-live/ui-one-to-one/rebuild-final` and contains 127 PNG captures. Its
contract reports:

```text
exactReferenceHash       d75e86d7cef945929cd3fc6af8d736f4cd3dee028f8d79d52b405edc95486e4a
shell                    true
tabCount                 6
featureCount             42
availabilityCount        14
unsupportedExecutionEnabled 0
dailyTaskOnlyEnabled     true
captureCount             127
```

The reference matrix in `.codex-live/ui-one-to-one/reference-full` also contains
127 captures. The comparison report at
`.codex-live/ui-one-to-one/visual-diff-report.json` confirms 127/127 matching
filenames, no missing or extra images, and no dimension mismatches. For the 117
desktop-width captures, the recovered 192-pixel sidebar and 70-pixel top bar have
117/117 significant-pixel matches. Inspected overlays show the remaining page
content differences in the intended rebuild layer: availability badges, disabled
unproven controls, the `Daily Task only` label, and run-time timestamp changes.

This means the underlying renderer/resource is the recovered UI byte-for-byte,
while the rebuild deliberately does not claim pixel identity for controls whose
safety/availability state is annotated or disabled.

Validation completed without live game actions:

- Release desktop build: 0 warnings, 0 errors.
- Core checks: 44/44 passed.
- Python unit tests: 151/151 passed.
- Legacy WinForms `--smoke-test`: passed, including all 42 feature localization
  and availability-gate checks.
- Final WebView contract: 127/127 captures, Daily Task action enabled, zero
  unsupported execution controls enabled.

## REMAINING UNKNOWN / PENDING

UI recovery does not prove the game behavior of PENDING features. Their recovered
cards, dialogs, labels, and controls can be rendered, but their execution remains
disabled until the corresponding current-build contract is independently
recovered and verified. The capability annotations are therefore intentional
differences from the preserved reference screenshots.

