# LWB317-UI-MAP-TOOLBAR-VISUAL-001 — worker delivery

Status: **AWAITING_REVIEW**. Project-lead acceptance remains pending.

The recovered Map renderer exposed three bounded presentation differences in the
normal data toolbar/pagination surface. Current production rendered table/error/
pagination outside the recovered `.map-search` grid, locale-grouped normal result
totals, and omitted the recovered `map-plunderable-filter` class. Milestone A
preserved the assignment-start source and executed the required 34 EN/JA states
plus loading, error, provider and pagination boundary supplements. Milestone B
corrected only those three differences in `MapDataPage.jsx` and verified actual
local toolbar handlers through the mounted Vite application.

The six required Milestone C pairs were captured with identical CSS/input
conditions and all twelve screenshots were inspected. Every pair has zero geometry
differences. City EN/light, City JA/dark 375px and Scheduled JA/dark 375px have
byte-identical screenshots. Truck differs only at its unavailable schedule action;
Secret Task differs only at unavailable schedule/share actions; Treasure differs
only at its two disabled claim actions. Those fences are intentional and remain
disabled. Narrow layouts expose no current-only defect.

Affected current regressions pass across navigation, request lifetime, original/
current interactions, filter lifecycle, Auto controls, Manual empty selection,
48 table renderer cases, Treasure table states and row actions. The assignment's
45 toolbar/pagination translation keys exist directly in all nine catalogs. No
native export/scan/schedule/share/claim/jump/mark/Run-now/gameplay action was used
for proof.

Evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/`. Milestone A is
`4574cc8aaf5751b1089c22f997e580d0ad16dff8`; Milestone B is
`9668133737f2589c27224613d8d343c5e732e474`. Milestone C is the closeout packet
containing the six browser pairs, inspection record, difference/coverage matrices,
affected replay and read-only validator. This closes only the assigned source/local
toolbar and pagination gate; overall Map/UI status remains partial.
