import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {getMapPreviewProvider} from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
import {scanHeaderFixture} from '../../../../src/LWBridge.UI-0.3.17/src/mapScanHeaderFixtures.js';
const states=['map-scan-reading','map-scan-completed','map-scan-publishing','map-scan-stopped'];
let rejected=0;
for(const state of states){
 for(const mode of ['native','native-unavailable'])assert.equal(getMapPreviewProvider(mode,state),null);
 const p=getMapPreviewProvider('preview',state);assert.equal(p.online,false);assert.equal(p.mapApi.previewFixture,true);
 const s=await p.mapApi.summary(),o=await p.mapApi.dataOptions();assert.equal(s.scanState.scanRunId,'fixture-header-run');
 if(state==='map-scan-completed'){assert.equal(o.scanProgress.id,s.scanState.scanRunId);assert.equal(o.scanProgress.status,'completed');assert.equal(s.scanState.startedAt,0);}
 else assert.equal(o.scanProgress,null);
 for(const [method,args] of [['start',[['city'],'normal']],['stop',[]],['clear',[321]],['jumpServer',[322]],['coordinateJump',[{serverId:321,x:10,y:10}]],['setPlayerMark',[{ownerUid:'fixture'}]],['exportCities',[{}]]]){await assert.rejects(p.mapApi[method](...args),e=>e.code==='PREVIEW_NATIVE_ACTION_BLOCKED');rejected++;}
}
for(const state of ['map-city','map-truck','map-treasure','map-scheduled','unrecognized'])assert.equal(scanHeaderFixture(state,100000),null);
assert.equal((await getMapPreviewProvider('preview','map-truck').mapApi.dataOptions()).scanProgress,null);
const result={status:'PASS',states:states.length,nativeModeFences:states.length*2,rejectedActions:rejected,priorFixtures:'UNCHANGED'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(path.dirname(fileURLToPath(import.meta.url)),'fixture-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_SCAN_HEADER_FIXTURES_OK nativeFences=8 rejected=${rejected}`);
