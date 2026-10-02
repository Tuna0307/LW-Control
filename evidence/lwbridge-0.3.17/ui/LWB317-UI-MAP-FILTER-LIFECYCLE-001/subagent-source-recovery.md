# LWB317-UI-MAP-FILTER-LIFECYCLE-001 — independent source recovery

Worker lane: original-source/component-runtime recovery only. No production or master-document edit.

## Reference and method

Primary source: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`, SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089` (`EXACT_BYTES`). The readable locator companion is the existing layout-only reprint `LWB317-UI-MAP-INTERACTIONS-001/reference/MapDataPanel.pretty.js`; its identifiers remain the original ones except collision suffixes documented by that campaign.

Executed confirmation uses `subagent-source-lifecycle.mjs`, which drives the existing exact original-component runner `LWB317-UI-MAP-INTERACTIONS-001/original/original-runtime.mjs`. That runner evaluates the original component bytes with inert backend/native stubs and persistent React-like hook/effect state. It does not execute native/gameplay operations. Command:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/subagent-source-lifecycle.mjs`

Result: `LWB317_SUBAGENT_SOURCE_LIFECYCLE_OK`.

## Refreshed options and filter validation

The successful options handler is bytes `34871-35650`, pretty lines 398-415. It increments `O.current`, drops the response unless its captured generation still equals `O.current`, and, if `reply.serverId !== R`, changes the data server to that reply server and returns without applying lists or filter validation. A current same-server reply replaces options/count-side state and then validates the four selected filter families.

- City alliance: `kt(e=>{if(e===\`none\`)return t.noAllianceCount>0?e:\`all\`;let n=tt(e);return!n||t.alliances.some(e=>e.name===n)?e:\`all\`})`. Raw `none` stays selected only while `noAllianceCount > 0`; otherwise it becomes `all`. A decodable named value stays selected only when the exact decoded name is in `alliances`; a missing named alliance becomes `all`.
- Resource/Monster name selection: each selected key is kept only when the refreshed per-kind name list contains the same key by strict equality. The handler always creates a new `{resource,monster}` object, including when both effective values are unchanged. Because that object is a search-effect dependency, every applied options response causes a follow-up search.
- Secret Task level: `qt(e=>!e||t.dispatchLevels.includes(Number(e))?e:\`\`)`. Empty remains empty; a selected value stays only when its numeric form is present; otherwise it becomes empty. An empty refreshed list therefore clears every nonempty level.
- Treasure type: `Vt(e=>!e||t.treasureTypes.some(t=>t.key===e)?e:\`\`)`. Empty remains empty; a selected key stays only on an exact key match; otherwise it becomes empty. An empty refreshed list clears every nonempty Treasure type.

The executed checker confirmed valid selections remain `name:Foo`, `"5"`, `gold`; after the options remove them they become `all`, `""`, `""`. It also confirmed an old server-321 options response is ignored after a server-456 generation starts, while the current server-456 response applies.

The search effect dependencies are bytes `34677-34870` and include alliance, level, Treasure type, name-selection identity and rows/options-adjacent state. Search requests themselves use generation ref `T` in `rr`, bytes `38513-39238`; only the current request may write rows/total or clear loading in `finally`.

## Alliance sentinel representation and decoder edge cases

Decoder `tt`, bytes `9486-9594` (pretty 182-188), is exact:

`function tt(e){if(!e.startsWith(\`name:\`))return\`\`;try{return decodeURIComponent(e.slice(5))}catch{return\`\`}}`

The City select renders the two sentinels as raw `all` and raw `none`, while real alliance options use `name:${encodeURIComponent(e.name)}` at the option expression around byte `51738` (pretty 713-715). Query builder `nr`, bytes `37497-38513`, decodes `Ot` with `tt` and emits `alliance` only when the decoded result is nonempty; `withoutAlliance` is true only when the stored selection is exactly raw `none`. City export reuses the same `nr(1,200)` projection, so it has the same identity rules.

Executed cases establish:

| selection/value | decoder result | City query |
|---|---|---|
| raw `all` | `""` | neither alliance field |
| raw `none` | `""` | `withoutAlliance: true` |
| `name:none` | `none` | `alliance: "none"` |
| `name:all` | `all` | `alliance: "all"` |
| `name:%E8%81%94%E7%9B%9F%20%CE%A9` | `联盟 Ω` | `alliance: "联盟 Ω"` |
| encoded `A/B? C#D%` | `A/B? C#D%` | exact decoded alliance string |
| malformed `name:%E0%A4%A` | `""` | neither alliance field |
| arbitrary non-prefixed value | `""` | neither alliance field unless it is raw `none` |

The malformed case is a source counterexample to a tempting normalization rule: the options validator tests `!decodedName || offeredNameMatch`. A decoding failure therefore **retains the malformed stored value**, even if the refreshed alliance list is empty. It does not convert it to `all`; query projection simply treats it as no alliance filter. The same `!decodedName` branch also retains arbitrary non-prefixed values. Implementations should reproduce the original accepted value path rather than assume every unknown value is reset.

This expands INTERACTIONS C09. Its named-`none` diagnosis is correct, and the same separation is required for a real alliance literally named `all`; Unicode and URI-significant names round-trip through the prefix encoding.

## Clear Data lifecycle

Original `tr` is bytes `37087-37497`, pretty lines 466-474. It starts with `xn(\`\`)`, then awaits `scan_clear(R)`. No UI-data/filter reset happens before acknowledgement. A rejection writes `scanError = String(error)` and a log entry; the existing state otherwise remains in place.

On acknowledged success the source performs these resets:

- applies the returned scan state through `onState`, then sets `dataServerId` to `reply.serverId`;
- increments options generation `O.current` before applying local resets;
- clears alliance options, Resource/Monster name options, Secret Task level options, reward item options and Treasure type options;
- writes all-zero counts `xe`, marks counts loaded, reports those zeros through `onCounts` for the response server, sets no-alliance count to `0` and scan progress to `null`;
- resets City alliance to `all`, Resource/Monster name selections to `{}`, Secret Task level to `""`, all Truck/Train item filters to `{}`, and Treasure type key to `""`;
- clears the normal-tab `{page,rows,total}` cache, then writes page `1`, rows `[]`, total `0` and loading `false`;
- clears both the Dispatch/Ghost and Truck selection maps;
- increments both `rowsRevision` and `optionsRevision`;
- logs `map scan data cleared`.

States with no `tr` setter and therefore retained across a successful clear include keyword, City marked-only, per-kind quality, Dispatch/Ghost completion status, plunderable-only filters, all sorts, random-delay text, Treasure foreign-radar preference, Treasure lucky-first preference, general action message and Treasure claim message. The executed runner explicitly verified all of those retentions. It also verified the previously selected Resource name clears directly in `tr`; the old C04 diagnostic only looked equivalent in canonical code when its synthetic backend kept re-offering the name. PM-026 already classifies C04 as a harness/context caveat, and C04b with the realistic empty name list is the useful historical control.

There are two important post-ack effect consequences:

1. Although `tr` writes loading `false`, its `rowsRevision` increment re-runs the search effect on a positive data server. `rr` immediately writes loading `true`, so the settled UI while that fresh search is pending is loading. The checker observed page 1 / rows [] / total 0 / loading true immediately after acknowledgement.
2. `optionsRevision` launches a new options request. Together with the explicit `O.current += 1` in `tr`, a pre-clear options response is obsolete and cannot repopulate cleared options. A successful fresh options response then replaces the Resource/Monster name-selection object, which can cause the original's usual extra post-options search.

The clear function does not directly increment search generation `T`, but `rowsRevision` causes a new `rr`, which does. The checker held a pre-clear search pending, acknowledged Clear, then resolved that stale search: stale rows/total were ignored and its `finally` did not clear loading for the newer post-clear request. This is the required stale search/finally fence.

Clear itself has **no request-generation/disposal token** in original `tr`. The checker deferred `scan_clear(321)`, changed the parent/server to 456, then resolved the old Clear with a server-321 response. The original accepted that acknowledgement and set its data server back to 321 via the returned state. This is source behavior and is separate from the options/search stale-reply fences. It should not be misreported as evidence that the original Clear request is server-change fenced.

## Treasure preference storage and query projection

Constants are in the source declaration around pretty line 84:

- `Ne = "lwbridge.mapIncludeForeignRadarTreasures"`
- `Pe = "lwbridge.mapLuckyTreasurePriority"`

Lazy state initializers are bytes `31702` and `31764` (pretty line 309):

- foreign-radar: `()=>localStorage.getItem(Ne)===\`true\`` — checked only for exact stored string `true`;
- lucky-first: `()=>localStorage.getItem(Pe)!==\`false\`` — checked for every value except exact stored string `false`, including a missing key.

Persistence effects are around bytes `32076` and `32140` (pretty 312-315) and always call `localStorage.setItem(key,String(boolean))`. They run on mount for the initialized state, so missing or unexpected stored text is normalized immediately even before visiting the Treasure tab. Executed initialization matrix:

| stored foreign / lucky | initialized foreign / lucky | mount writes |
|---|---|---|
| missing / missing | false / true | `false` / `true` |
| `true` / `false` | true / false | `true` / `false` |
| `false` / `true` | false / true | `false` / `true` |
| `TRUE` / `0` | false / true | `false` / `true` |

The Treasure checkbox handlers set the raw checked boolean and page 1. The persistence effects write its string form. A new component mount reads the persisted values and restores the same booleans; the executed checker toggled to foreign=true/lucky=false and confirmed that remount state.

`nr` always includes `includeForeignRadarTreasures` and `luckyFirst` for the Treasure tab, even when either value is false. The checker observed the default query `{includeForeignRadarTreasures:false,luckyFirst:true}` and the toggled query `{includeForeignRadarTreasures:true,luckyFirst:false}`. On non-Treasure tabs both expressions evaluate to `undefined`, so normal serialized requests omit the fields. Clear retains both preferences.

This confirms INTERACTIONS C08/C28 and PM-026's recorded open gap; it also adds the exact unexpected-string normalization and mount-write behavior needed for parity.

## Cross-check against PM-026 / INTERACTIONS

- PM-026's accepted request-lifetime/navigation correction must remain intact. Nothing in this recovery contradicts its generation fencing for stale searches, including stale `finally` writes after backend loss/unmount.
- C02 correctly identified missing alliance/level/Treasure options validation, but the complete source contract also includes `none` count handling, malformed/non-prefixed alliance retention, stale options generation and reply-server handling.
- C03 correctly identified clear reset gaps. The complete reset is selective: keyword, quality and several other filters/messages remain. A blanket reset would be wrong.
- C04 is not independent evidence for a product defect when the synthetic post-clear options keep a name alive; C04b and direct `tr` source show the actual original reset.
- C08/C28 correctly identify lucky default, both localStorage preferences and false-valued Treasure query fields. The lazy initializers also define behavior for missing and unexpected strings, and the mount effects normalize storage.
- C09 correctly identifies raw-name sentinel collision in the prior canonical representation. The original prefix also separates real `all`, Unicode and URI-significant names; malformed `name:` is a special retained-but-query-empty value.
- C14 remains a harness summary/count diagnostic as PM-026 records; it does not change the direct all-zero count write in successful `tr`.
- The accepted explicit Treasure `Checking` fixture/presentation is unrelated to these preference booleans. No source behavior here justifies broadening or replacing that fixture/state producer.

## Limits and implementation impact

This recovery establishes frontend source/component-runtime contracts only. Backend reply contents are harness-controlled except for fields the frontend source reads; no native Clear, search, export, Treasure claim, scan or gameplay action was executed. No original post-auth pixel claim is made.

For implementation, preserve the accepted navigation/cache/request-disposal behavior while adding these source contracts. In particular: keep resource/monster validation and post-options search ownership, keep the stale search `finally` fence, use the encoded real-alliance identity for both search and export query construction, reset only the states written by `tr`, and persist the two Treasure booleans at the unscoped source keys with exact-string lazy defaults.
