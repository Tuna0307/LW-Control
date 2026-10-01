# Home source/state audit

Baseline dd46685. This is an audit artifact, not a UI correction or native test.
Run `node evidence/lwbridge-0.3.17/ui/LWB317-PM-015/audit-home.mjs --verify-record`
from the repository root. Original source and actual clone functions are parsed
with existing Babel/esbuild dependencies. No event handler/native code runs.

`home-audit.json` contains original source identity/full function byte locators,
clone identity, 960 synthetic case totals, representative differences and direct
error/localization comparisons. Busy/error input adaptation is disclosed in the
report. Whole source-function slices intentionally retain the exact minified
contract. Recorded differences are asserted to exist; an audit success marker
means reproducible findings, not that the clone matches.

Header and button text differences are source-contract findings. Lifecycle
disabled-state differences reflect existing action fencing. Root-picker and
lifecycle-button visibility match throughout the test combinations. Original
runtime pixels, native lifecycle and reachability of future proxy/launch busy
states are not proved. See docs/reviews/2026-10-02-LWB317-PM-015-home-audit.md.
