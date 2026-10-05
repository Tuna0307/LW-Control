import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {read} from '../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import {hash, scenarios} from './garrison-zombie-harness.mjs';

const here=path.dirname(fileURLToPath(import.meta.url));
const cases=[];
for(const language of ['en','ja']) for(const name of ['empty','positive','modalUid','statusError','zombieOpenError']) {
  const pair=scenarios[name](language);
  cases.push({id:`${language}-${name}`,...pair});
}
const by=(suffix)=>cases.filter((entry)=>entry.id.endsWith(suffix));
const expected={
  emptyAncestry:by('-empty').filter((entry)=>JSON.stringify(entry.source)!==JSON.stringify(entry.current)).length,
  dragIcons:by('-positive').filter((entry)=>JSON.stringify(entry.source.priorityLists).includes('M5 4h.01') && entry.current.arrowTextCount>0).length,
  uidSearch:by('-modalUid').filter((entry)=>entry.source.allyRows.length===0 && entry.current.allyRows.length>0).length,
  inventedStatusError:by('-statusError').filter((entry)=>entry.source.alerts.length===0 && entry.current.alerts.length>0).length,
  zombieErrorAncestry:by('-zombieOpenError').filter((entry)=>entry.source.settingsBlocks.length===2 && entry.current.settingsBlocks.length===1).length,
};
if(Object.values(expected).some((count)=>count!==2)) throw new Error(`Unexpected G/Z failing baseline ${JSON.stringify(expected)}`);
const report={
  marker:'LWB317_REMAINING_M2_GARRISON_ZOMBIE_FAILING_BASELINE',immutableBaseline:true,
  source:{asset:'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js',sha256:hash(read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js')),pe:{utf8ByteOffset:4586,byteLength:11256},he:{utf8ByteOffset:16529,byteLength:1957}},
  current:{path:'src/LWBridge.UI-0.3.17/src/SquadsPage.jsx',sha256:hash(read('evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-2/baseline/pre-fix-SquadsPage.jsx'))},
  expectedFailures:expected,cases,
  limits:'Actual recovered pe/he and actual current GarrisonPreviewSettings/ZombieBusPreviewSettings execute under disclosed inert source-valid states. Native actions/effects are not run. Compact-card and already accepted Zombie table details are outside these structural assertions.'
};
fs.writeFileSync(path.join(here,'garrison-zombie-failing-baseline.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({marker:report.marker,expectedFailures:expected},null,2));
