import assert from 'node:assert/strict';import fs from 'node:fs';import {getMapPreviewProvider} from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
let rejected=0,fences=0;
for(const name of ['map-export-feedback','map-scan-feedback-error']){
 for(const mode of ['native','native-unavailable']){assert.equal(getMapPreviewProvider(mode,name),null);fences++;}
 const p=getMapPreviewProvider('preview',name);assert.equal(p.online,false);assert.equal(p.backendAvailable,true);
 for(const method of ['start','stop','clear','jumpServer','coordinateJump','setPlayerMark','exportCities']){await assert.rejects(p.mapApi[method](),e=>e.code==='PREVIEW_NATIVE_ACTION_BLOCKED');rejected++;}
 if(name==='map-export-feedback')assert.equal(p.previewActionMessage.key,'map.exportExcelSuccess');
 else assert.equal((await p.mapApi.summary()).scanState.lastError,'MAP_SCAN_START_FAILED');
}
const r={status:'PASS',nativeFences:fences,rejectedActions:rejected};if(process.argv.includes('--record'))fs.writeFileSync(new URL('fixture-results.json',import.meta.url),JSON.stringify(r,null,2)+'\n');console.log(`LWB317_MAP_FEEDBACK_FIXTURES_OK nativeFences=${fences} rejected=${rejected}`);
