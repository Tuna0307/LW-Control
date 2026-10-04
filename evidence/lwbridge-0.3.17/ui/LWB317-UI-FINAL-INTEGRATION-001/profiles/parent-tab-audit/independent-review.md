# PROFILE-TABS independent review

Date: 2026-10-04. Recommendation: **ACCEPT for the assigned source/local correction**. Project-lead acceptance remains separate.

Reviewed the complete working-tree diff against `307b13ca97840077ebf649eceb1caefc670fb052` in `App.jsx`, `AutomationPage.jsx`, `SquadsPage.jsx`, and `MapDataPage.jsx`. No acceptance-blocking defect was found in this diff. No product file was edited by this reviewer.

## Original evidence and implementation assessment

Independently checked all **77 recorded UTF-8 slices**, their exact text and SHA-256, against the four recovered assets referenced by `source-contract.json` and `category-lifetime/source-contract.json`. The full asset hashes also matched. This was a read-only verification; the immutable recovery/baseline writers were not rerun.

- The original shell's parent selections are at `index-BVfnK1wp.js` bytes **363161**, **363192**, and **363223**, with their controlled child bindings at **373777**, **374455**, and **374732**. Current App keeps all three states outside the profile-keyed children and supplies distinct route-specific props. The existing local `showProfiles` capability intentionally replaces the excluded commercial entitlement gate; this correction does not manufacture an original-service entitlement.
- Automation local/default/effective/visited declarations are at `AutomationPanel-BJ0gIqFh.js` bytes **25370**, **25402**, and **25549**; the category click contract is at **42780**. Current nullish precedence, first-visited initialization, and visited-before-parent/local notification follow those contracts. Missing, null, empty-string and unknown controlled values are not silently normalized into different behavior.
- The original single Automation grid starts at **42910**. All eight current Activities retain the recovered order: resource gathering, Trade, System, Chat, Resources, the first Daily group, Alliance, the second Daily group. The Daily split preserves Training/Construction/Stamina/Treatment before Alliance and Trucks/Secret Task/Ghost Ops after it. Both Daily groups use the same selected-category and visited-category predicates.
- Squad local/visited/effective declarations are at `SquadPanel-HC3-DJei.js` bytes **191517**, **191541**, and **191602**, with selection at **192276**. Current visited initialization uses the effective controlled selection. Existing inner Activities and their children are preserved.
- Map effective selection is at `MapDataPanel-B4GXEND2.js` byte **30426**, with the recovered change callback at **39238**. Current outgoing cache storage and incoming restoration are computed before parent/local notification. Generation retirement, active-tab no-op, page/row/total/loading/error/message updates remain in their accepted order. Manual/Auto state remains independent of the table tab.
- Automation/AFK leaf functions remain unchanged under the focused contract proof. Independently parsed and compared the entire current `EquipmentContent` declaration to HEAD: its motion hierarchy, dialog, callbacks and effects are byte-identical. The four-file diff contains no additional Equipment or Map operation changes.

## Executed proof

Both requested scripts were executed against the current production files:

- `check-current-contract.mjs`: **22 cases PASS**, eight Activity groups verified.
- `run-mounted-current.mjs`: **five groups PASS**. The immutable pre-correction App/panels reproduce **three** lost selections after profile replacement; current production has **zero**. Actual local profile controls preserve the three parent selections through A → B → A and independent later category changes, while the keyed child DOM and Map keyword reset.
- Actual single-profile App retains the uncontrolled category through route leave/return. Explicit Equipment preview start, nullish fallback and controlled precedence pass.
- Actual Automation stores/subscriptions demonstrate seven initially mounted Daily cards, one active Trade subscription while Daily is hidden, preserved Daily DOM/collapse state on return, and subscription cleanup on unmount. The immutable CSS-hidden baseline instead mounts and keeps all categories subscribed. Only subscription counts were observed; actual leaf callbacks and stores were retained.
- Protected-WIP guard: **7/7 PASS**. `git diff --check`: PASS; only Git's existing LF/CRLF advisory was emitted.

No historical scripts/results or immutable recovery/baseline records were modified. The two permitted current-result files were refreshed by their current checkers.

## Current production fingerprints

| File | SHA-256 |
| --- | --- |
| App.jsx | B245218E347ECDB3EBC318133B27BDF8F5CDAF8AA5F6ED7BB2BA98E643789398 |
| AutomationPage.jsx | 3BCC104D86327BF07560850F9EEF29DA63275A324F9DB11DBAF1FBA8ACB52A4D |
| SquadsPage.jsx | 0BD3AC149618E63645954D97D4304FB3D64A8EA84559B9DE55E77D46938DDC0B |
| MapDataPage.jsx | A1CAACCB7C3F3A6658C65A819735C639F52494B6C708A33C0DE13B281FD0E974 |

## Findings and proof limits

The prior parent-tab loss and eager category-effect lifetime findings are **valid and corrected** by this diff. No further production change is recommended for this assignment. The original controlled-value contract does not automatically visit a new externally supplied category; preserving that source behavior is intentional rather than adding an inferred synchronization effect.

Mounted proof uses the actual canonical App, route modules, React 19.3.0 and jsdom 27.0.0 with an unavailable, inert bridge and forbidden network access. It establishes local React state/effect/DOM behavior. It does not prove native profile/config producers, gameplay actions, native Equipment persistence, physical HTML5 dragging, motion pixel equivalence, original protected-runtime imagery, or whole-UI parity. Production package checks and broader v3 regression replays remain separate project-lead evidence; this independent review does not relabel them as its own execution.
