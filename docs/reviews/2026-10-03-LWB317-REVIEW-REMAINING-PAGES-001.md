# Independent lead review — four-page campaign

Decision: **REVIEW_COMPLETE / CHANGES_REQUIRED** for submitted revision
`afd65b64808e0fd43175dbbaa5afc36e32def9ef`. All six milestones were committed,
pushed and returned. This is a completed delivery, not acceptance of all four pages.
Do not restart A–F. Preserve passing helpers and existing page corrections.

The lead inspected the production diff, reran focused/canonical/integrity checks,
inspected the twelve-image contact sheet and enlarged narrow City/wide Settings
captures, and independently reviewed the three page lanes. The lead-owned runner
reproduces twelve distinguishing failing cases through actual original/current
renderers and callbacks. It uses inert hooks/providers only, no native/gameplay/
service calls or new browser interaction. Subagent findings were reviewed and
reproduced by the lead; they are not accepted on assertion alone.

## Findings requiring correction

| Finding | Original source | Current observation / implication |
|---|---|---|
| P1 Mini Games crosses preview boundary | HotkeyPanel-XA8idRHB.js `z` byte3161 length291, `B` byte3452 length244 await real providers | With empty previewState and native connected online=true, Land/Sheep actions enable. Land's actual callback claims cell17 sent without a provider; Sheep settles silently. Native and native-unavailable also display invented Sheep level4. App clears preview state outside preview but supplies online, so this is reachable |
| P1 Settings crosses preview boundary | SettingsPanel-DqxIWv_E.js `p` byte241 length1557; index-BVfnK1wp.js `Rn` byte210389 length2833 use provider results | Default native/native-unavailable Export and Check enable. Actual callbacks invent a 4,404,019-byte Preview/diagnostics.zip success and upToDate status; no archive/update service is called |
| P2 Settings initial metadata is fabricated | Index `Ln` byte210230 length158 initializes empty currentVersion/directory and null latestVersion | Default non-preview uses previewUpdateStatus and displays current/latest .17 and Preview/updates before an acknowledgement |
| P2 City null-layout states are missing | CityLayoutPanel-DoNWkywK.js `ue` null-layout branch byte19149 selects loading/error/noData | Recognized city-layout-loading and city-layout-error both get a populated synthetic layout, so the workbench renders instead of the original empty/loading/error branch |
| P2 City drag omits grab offset | Original grab offset producer byte15627; `it` byte7661 consumes it | Hospital down/move at the same (84,108) coordinates shifts to point404; original stays at403 with no placement. Dragging from a non-origin occupied tile must preserve pointer-to-anchor offset |
| P2 City invalid-target movement loses valid-axis fallback | Original `at` byte8219 and `tt` validate/fall back | Barracks down(108,36) -> move(180,84) reaches valid target505 in original; current rejects the whole move. Current clamping/release validation is not the original pointer model |
| P2 City Apply preparation misses busy gate | Original expression `U=!!ze||Oe` byte2648, `ct` byte9199 enters preparation busy | Original pending validation disables Undo/Refresh. Current derives busy only from running/cancelling jobs and opens local confirmation while Undo/Refresh remain enabled. Lead inert test directly reproduces Undo mismatch |
| P2 event-time error text lifetime differs | Settings `m` byte1798 length1848; Hotkey `L` byte2818 length304, failure assignment byte3035 length26 | After an English failed save then Japanese locale switch, original retains English error text until another operation changes it; current stores a key and retranslates the retained error. Source load effects have [] dependencies, so a locale switch does not reload/clear it |

These are in-scope source/local UI defects or violations of the assignment's native
availability boundary. Correcting them does not require a native provider. Do not
fix false success by implementing gameplay, archive export or an updater service.

## Passing proof and limits

Lead execution passed City935, Hotkeys70, Mini86, Settings71, Equipment105,
Equipment-R1 61 and AFK729 checks, canonical check/current production-package,
four-page integrity validator and protected hashes7. Package source/artifact:
`bbfcfbf09c7fa1a6fd849fa3fcb3a7921cef2aea6947b7518a1053e73962b573` /
`6675c3003439f849ae20e57a77ad9a7b80cf70ca0b42e3467a074fb3ea1afdb7`.
The worker build was inspected; the lead changed no production code.

The current integrity packet validates EXE, four assets, fifteen byte slices, two
product files, thirteen JSON files and twelve screenshots. This validates evidence
identity, not omitted behavior. The immutable sixteen-case dispatch baseline uses
source-marker predicates, not sixteen executed original/current behavior cases.
The final closeout checker similarly counts the recorded inventory and checks
markers/catalog/image records. Mini proof executes original renderers but mostly
guards current production through substrings; its native boundary was untested.
Passing these checkers cannot establish all 54 inventoried branches as validated.

The independent shared Hotkey/Mini reviewer executed forty supplied-state
original/current panel comparisons plus two save callbacks with no mismatch in
the tested static branches/save merge/rollback. The later locale-lifetime and native
cases distinguish untested boundaries. City pure placement/occupied/validation
helpers match; failures are in page state and pointer/apply callbacks.

The screenshots support settled preview layouts and recorded 784x415 CSS captures;
they are not original-reference pixel comparisons or native proof. Worker browser
console records are clean; no fresh lead browser interaction was performed.

The separately scoped top-level shell unmount/Activity retention gap remains
known and unaccepted. It is not the reason this page campaign requires changes.
Native providers, game hotkeys, City apply, Land/Sheep producers, diagnostics/
updater/preferences and original pixels retain their previous limits.

## Continuation

LWB317-UI-REMAINING-PAGES-001-R1 is READY: A preview/default-native boundaries,
B City null-layout/pointer/preparation corrections, C event-time feedback lifetime,
D regression/browser/evidence closeout. Use the lead's immutable twelve-case
reproduction; add corrected distinguishing cases rather than rewriting it.
Keep parent CHANGES_REQUIRED until lead acceptance; no shell or native campaign.

Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-REMAINING-PAGES-001/`.
