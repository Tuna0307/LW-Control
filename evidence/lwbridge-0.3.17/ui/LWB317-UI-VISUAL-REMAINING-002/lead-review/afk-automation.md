# Independent lead review — AFK/Squads/Garrison and Automation

Date: 2026-10-05. Reviewed HEAD `967a92c3553e4bc81272741f333803ee54ae5e44` against `6919816b4382e16c48dc8694493c06879e34f310`.

**CHANGES_REQUIRED.** Milestone 3 has three independently reproduced source-local presentation mismatches. Milestone 2 lacks the required current whole-`I` paired composition evidence. Neither gate supports blanket completion of all recoverable UIUX branches.

This review made no production edits, evidence regeneration, index/history changes, or owner-session actions. One reviewer-owned headless Chrome process used in-memory `setContent` pages and was closed in `finally`. The only written file is this report.

## Verified identities

- Target EXE SHA-256 independently checked: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Original `AutomationPanel-BJ0gIqFh.js`: `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`. Actual `Ae`, UTF-8 byte 21817, length 53453.
- Original `SquadPanel-HC3-DJei.js`: `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`. Actual `I` byte 28070; `pe` byte 4586; `he` byte 16529.
- Current `AutomationPage.jsx`: `B05E6B3873DC86118379F8C7E1FC224DF3D4B0E8005AD7E48B5D659C6EDF98A5`.
- Current `SquadsPage.jsx`: `16370281810045DF67CA9FF8E324BEE60E3C99DA1E7C493005D2B92E7769A4F4`.
- Current `previewAutomationContracts.js`: `FD4BC9C29F23631718D19818D273745C8D042DF35B1DDA2BE1E1928E75BB2295`.

Original asset locators below are zero-based UTF-8 byte offsets inside `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`. Current files are under `src/LWBridge.UI-0.3.17/src/`.

## Actionable findings

### A1 [P2] Missing fixed-carriage fields select extra Train carriages

Source: `AutomationPanel-BJ0gIqFh.js` byte 30848: `Gn=(F?.allianceTrainRide?.fixedCarriageIds??[]).filter(...)` and the adjacent VIP `Kn` fallback use empty arrays. Fixed-mode checkbox rendering uses `Gn.includes(e)` and `Kn.includes(e)`. The radio callback at byte 54581 writes only the mode.

Current: `AutomationPage.jsx:231-232`, `fieldState("normalFixedCarriageIds", [1], true)` and `fieldState("vipFixedCarriageIds", [1, 2], true)`, consumed by the complete Train form at line 376.

Independent SAME-input renderer execution, including direct canonical `AutomationFields` invocation that bypasses `initialAutomationDraft`, yields:

| Fixed-mode input | Original normal/VIP checked counts | Current normal/VIP checked counts |
| --- | --- | --- |
| Missing both carriage arrays | 0 / 0 | 1 / 2 |
| Explicit empty arrays | 0 / 0 | 0 / 0 |
| Explicit `[1]` / `[1,2]` | 1 / 2 | 1 / 2 |

The source/current field-name mapping is `selectionMode` -> `trainMode`, `fixedCarriageIds` -> `normalFixedCarriageIds`, and `vipSelectionMode` -> `vipTrainMode`; VIP array names agree. No native execution is involved. This is an `EXACT_CONTRACT` missing-field renderer fallback mismatch. The explicit seeded preview fixture at `previewAutomationContracts.js:12` is source-valid when the same IDs are supplied to the original; it is not itself a defect. Correct the renderer fallbacks and prove the missing/empty/populated normal/VIP modes with matched inputs.

### A2 [P2] Train reward drop-target highlight is missing

Source: `AutomationPanel-BJ0gIqFh.js` byte 55931 adds `drag-over` when `Tt===e.key`. Its exact `onDragOver` callback calls `Et(e.key)` after the selected/different-dragged-item guard; drop/end clear both source and target state. Current `AutomationPage.jsx:236` owns only `draggedReward`; line 376 never owns/sets a target and never adds `drag-over`.

With two selected rewards, dragging Medal over Parts renders original target `automation-preference-item selected drag-over`. Executing the actual current `onDragStart`, rerender, actual `onDragOver`, rerender produces `automation-preference-item selected` for Parts. Both sides use the same selected rewards. Recovered CSS has `.automation-preference-item.drag-over{border-color:var(--blue);box-shadow:inset 0 2px 0 var(--blue)}`.

Restore the exact target state/class/clear lifecycle and pair source/current dragging and target states. This is local drag feedback, independent of physical HTML5 drag or native gameplay.

### A3 [P2] Generic invalid-card error paragraphs add vertical margins

Source: `AutomationCard-LCx_jIi7.js` function `c`, byte 2330: `l&&b&&...jsx("div",{className:"automation-error",role:"alert",...})`. Construction passes its invalid builder-limit error from exact `Ae`.

Current: `AutomationPage.jsx:439` renders both `fieldError` and `visibleDraftError` as `<p role="alert" className="automation-error">`.

Matched `maxBuilders=0`, online/source-local current fixture, EN/light/1280 rendering yielded the same error copy but these browser measurements:

```json
{"original":{"tag":"DIV","margin":"0px","y":219,"height":16},"current":{"tag":"P","margin":"12px 0px","y":231,"height":16}}
```

The recovered/current styles do not reset these generic paragraphs. Restore the recovered error element and pair invalid full cards, including the next control/card position, rather than relying on a text assertion.

### A4 [P3] Save-error title class does not match exact Ae

Source `Ae` byte 42382 uses `className:Lr==="error"?"error-text":"muted"`. Current `AutomationPage.jsx:655` always delegates to `sharedPageUI.jsx:30` `PanelTitle`, which always emits `muted`. Fresh exact renderer execution with a failed config-store snapshot gives original `span.error-text`; current failed-state title is `span.muted`, with identical copy. No color difference is claimed: the examined recovered CSS contains no `error-text` rule. Restore the source DOM class in the error state without changing unrelated titles.

## Acceptance evidence gaps

**M2:** `milestone-2/full-afk-harness.mjs:13` intentionally loads the immutable pre-fix `SquadsPage.jsx`; its original/current profile comparison replaces compact/Garrison/Zombie/editor siblings and is an appropriate failing baseline, not a current whole-page oracle. `build-composition-pairs.mjs:99-125` provides only two EN/light initial `pe/he` pairs, `garrison-pending` and `zombie-waiting`. Neither executes the complete `I` toolbar/profile/editor composition. The 70 current-browser assertions and current screenshots show mounted local behavior but cannot establish original/current whole-page parity. The requirement in work item lines 147-160 explicitly calls for whole original `I` comparison and full compositions before cropping. Supply a fresh current exact-`I` pair adapter and matched state matrix, covering toolbar/profiles/editor/runtime/errors and Garrison/Drill/Potion/Zombie conditionals in the required modes. No additional M2 production defect is asserted here.

**M3:** `source-render/render-pairs.mjs:116` sets every paired case `online:false`; lines 122-126 vary only seven categories, expansion and locale. These are 28 source/current HTML cases expanded into 56 visual pairs. They do not close newly assigned positive/conditional/error/running branches. Fixed Train controls, drag-over, invalid errors, enabled Chat reply/dispatch forms, populated Trade/history and generic task-busy settings composition need matched original/current proof. The current-browser packet does not substitute for that oracle. Retain accepted finite lower-helper/default gates and add only the unclosed branch evidence; do not repin historical records.

## Preserved work and ownership assessment

Independent function-body equality against `milestone-2/baseline/pre-fix-SquadsPage.jsx` was true for `AfkProfileEditor`, `EquipmentContent`, and `CompactAfkCard`. Current hashes respectively: `5DC6567B9AFE9332C3DC8E61DDD1056A443C921D496E61EE6172E73ABCB32E09`, `D8E34A02FA9E4C09AC95EFF6C4B0B48685244C2893D4F994A46564FD79C27F2A`, `EA05F60221761E50FE8959DB95C6B4E53E144CAC09431880C5BA2379709DF817`. Current source restores Master/profile/Drill shared store and separate Potion/Garrison/Zombie stores; Dispatch Assist owns its separate local store. The previously reported immediate-save/railway-running fixes are present. This review does not reopen the accepted editor/Equipment slices.

Global preview-store/profile isolation remains **UNKNOWN** in this report; a global registry and profile-keyed remount alone do not establish a reproduced cross-profile defect. Native/provider execution, loaded game assets, protected original runtime and live parity remain outside scope. The mismatches above require only recovered frontend state and local rendering/callbacks and must not be classified as those dependencies.

## Read-only executable reproduction

Run the following JavaScript on stdin using `node --input-type=module` from the repository root. It executes the existing exact renderer adapters after removing their persistence tail, executes actual canonical Train callbacks, and launches/closes its own headless browser for error geometry. It creates no repository outputs. Original `Ut=[]` is an initial/pre-discovery source-valid state; Train/error probes do not depend on squad discovery. Original reward `H` is supplied from the same configured preferred keys.

```javascript
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {compile,h,Fragment,hooks,flatten,read} from './evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import * as contracts from './src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js';
import * as fixtures from './src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js';
import en from './src/LWBridge.UI-0.3.17/src/locales/en.js';
const t=(key,vars={})=>String(en[key]||key).replace(/\{(\w+)\}/g,(m,k)=>vars[k]??m);
const file=path.resolve('evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render/render-pairs.mjs');
const source=fs.readFileSync(file,'utf8');
assert.equal(crypto.createHash('sha256').update(source).digest('hex').toUpperCase(),'3277540CC2ED974936EEC5D93E10F19D11B061D1711D8D779EFF3D71D151268E');
let code=source.replace(/^import .*;\r?\n/gm,'').replaceAll('import.meta.url',JSON.stringify(pathToFileURL(file).href));
const stop=code.indexOf("fs.mkdirSync(path.join(outputRoot,'raw')");
assert.ok(stop>0); code=code.slice(0,stop);
code=code.replace("if(binding==='H')value=['fixture-medal'];","if(binding==='H')value=caseData.preferred||['fixture-medal']; if(binding==='Ct')value=caseData.dragged??null; if(binding==='Tt')value=caseData.over??null;");
const AsyncFunction=Object.getPrototypeOf(async function(){}).constructor;
const make=body=>new AsyncFunction('fs','path','crypto','assert','createRequire','fileURLToPath','pathToFileURL',body+'return {setup,inputFor,materialize,element,renderToStaticMarkup,seed(v){caseData=v;}};')(fs,path,crypto,assert,createRequire,fileURLToPath,pathToFileURL);
const api=await make(code), pair=api.setup('en');
const input=api.inputFor(pair.current,'alliance'); input.online=true;
input.config.allianceTrainRide.preferredRewardKeys=['fixture-medal','fixture-parts'];
api.seed({expanded:true,preferred:['fixture-medal','fixture-parts'],dragged:'fixture-medal',over:'fixture-parts'});
const drag=api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae,input)));
assert.ok(drag.includes('automation-preference-item selected drag-over'));
console.log('ORIGINAL_DRAG_OVER',drag.match(/class="automation-preference-item[^\"]*drag-over[^\"]*"/g));
const pageSource=read('src/LWBridge.UI-0.3.17/src/AutomationPage.jsx');
const Icon=compile(pageSource,'AutomationIcon',{h,Fragment});
function fields(states){return compile(pageSource,'AutomationFields',{h,Fragment,...states,...contracts,...fixtures,useI18n:()=>({t,language:'en'}),previewTrainRewards:pair.current.previewTrainRewards,AutomationIcon:Icon,GameAssetImage:()=>null,ToggleRow:()=>null,DispatchAssistManual:()=>null});}
for(const [label,normal,vip] of [['missing',undefined,undefined],['explicit_empty',[],[]],['same_fixture',[1],[1,2]]]){
  api.seed({expanded:true}); Object.assign(input.config.allianceTrainRide,{selectionMode:'fixed',vipSelectionMode:'fixed'});
  if(normal===undefined){delete input.config.allianceTrainRide.fixedCarriageIds;delete input.config.allianceTrainRide.vipFixedCarriageIds;}else Object.assign(input.config.allianceTrainRide,{fixedCarriageIds:normal,vipFixedCarriageIds:vip});
  const original=api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae,input)));
  const originalCounts=[...original.matchAll(/<fieldset class="automation-compact-choice-group automation-carriage-choices"[\s\S]*?<\/fieldset>/g)].map(m=>(m[0].match(/checked=""/g)||[]).length);
  const states=hooks(), Fields=fields(states), draft={enabled:false,trainMode:'fixed',vipTrainMode:'fixed',...(normal===undefined?{}:{normalFixedCarriageIds:normal,vipFixedCarriageIds:vip})};
  states.begin();const tree=Fields({title:'Automatic Alliance Train Boarding',enabled:true,previewState:'automation-config',config:{draft,store:{}}});
  const currentCounts=flatten(tree).filter(n=>n.type==='fieldset'&&n.props.className.includes('carriage-choices')).map(group=>flatten(group).filter(n=>n.type==='input'&&n.props.checked===true).length);
  assert.deepEqual(originalCounts,label==='same_fixture'?[1,2]:[0,0]); assert.deepEqual(currentCounts,label==='explicit_empty'?[0,0]:[1,2]);
  console.log('FIXED',label,{original:originalCounts,current:currentCounts});
}
const states=hooks(), Fields=fields(states);let draft={...contracts.initialAutomationDraft('Automatic Alliance Train Boarding','automation-config'),preferredRewardKeys:['fixture-medal','fixture-parts']};
const store={getSnapshot:()=>({draft}),edit(update){draft=typeof update==='function'?update(draft):update;},flush:()=>Promise.resolve()};
const render=()=>{states.begin();return Fields({title:'Automatic Alliance Train Boarding',enabled:true,previewState:'automation-config',config:{get draft(){return draft;},store}});};
const rewards=tree=>flatten(tree).filter(n=>String(n.props?.className).startsWith('automation-preference-item'));
rewards(render())[0].props.onDragStart({dataTransfer:{}});rewards(render())[1].props.onDragOver({preventDefault(){},dataTransfer:{}});
const classes=rewards(render()).map(n=>n.props.className);assert.equal(classes[1],'automation-preference-item selected');console.log('CURRENT_AFTER_DRAG_OVER',classes);
const invalidCode=code.replace("const draft=structuredClone(typeof initial==='function'?initial():initial);","const draft=structuredClone(typeof initial==='function'?initial():initial);if(draft&&Object.hasOwn(draft,'maxBuilders'))draft.maxBuilders=0;");
const inv=await make(invalidCode), p=inv.setup('en');inv.seed({expanded:true});const invalidInput=inv.inputFor(p.current,'daily');invalidInput.online=true;invalidInput.config.construction.maxBuilders=0;
const originalHtml=inv.renderToStaticMarkup(inv.materialize(inv.element(p.original.Ae,invalidInput)));
const currentHtml=inv.renderToStaticMarkup(inv.materialize(inv.element(p.current.AutomationPage,{activeCategory:'daily',previewState:'automation-config'})));
const pw=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json')('playwright');
const browser=await pw.chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try {for(const [side,html] of [['original',originalHtml],['current',currentHtml]]){
  const context=await browser.newContext({viewport:{width:1280,height:900}}), page=await context.newPage();
  const css=side==='original'?read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'):read('src/LWBridge.UI-0.3.17/src/reference.css')+'\n'+read('src/LWBridge.UI-0.3.17/src/styles.css');
  await page.setContent('<!doctype html><html data-theme="light"><head><style>'+css+'</style></head><body>'+html+'</body></html>');
  const measurement=await page.locator('.automation-error').filter({hasText:'Enter a builder limit'}).evaluate(n=>({tag:n.tagName,margin:getComputedStyle(n).margin,y:n.getBoundingClientRect().y,height:n.getBoundingClientRect().height}));
  assert.equal(measurement.tag,side==='original'?'DIV':'P');assert.equal(measurement.margin,side==='original'?'0px':'12px 0px');console.log('INVALID_GEOMETRY',side,measurement);await context.close();
}} finally {await browser.close();}
```

Pinned reviewer output, Node `v24.18.0`:

```text
ORIGINAL_DRAG_OVER [ 'class="automation-preference-item selected drag-over"' ]
FIXED missing { original: [ 0, 0 ], current: [ 1, 2 ] }
FIXED explicit_empty { original: [ 0, 0 ], current: [ 0, 0 ] }
FIXED same_fixture { original: [ 1, 2 ], current: [ 1, 2 ] }
CURRENT_AFTER_DRAG_OVER [ 'automation-preference-item selected dragging', 'automation-preference-item selected', 'automation-preference-item' ]
INVALID_GEOMETRY original { tag: 'DIV', margin: '0px', y: 219, height: 16 }
INVALID_GEOMETRY current { tag: 'P', margin: '12px 0px', y: 231, height: 16 }
```

The saved historical validators were inspected, not rerun with persistence enabled. `git diff --check` passed. Final project acceptance remains with the lead.
