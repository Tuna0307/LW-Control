import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';

// Reuse the worker's actual-App compiler/mount scaffolding without executing its
// cases or overwriting its results. These cases and original comparisons are new.
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const worker = path.join(repo, 'evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-b');
let scaffold = fs.readFileSync(path.join(worker, 'run-focused.mjs'), 'utf8').split('const cases = [];')[0];
scaffold = scaffold.replace('const here = path.dirname(fileURLToPath(import.meta.url));', `const here = ${JSON.stringify(worker)};`);
const extra = `
const asset = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'));
const originalText = asset.subarray(350405, 350405 + 3406).toString('utf8');
const originalParentText = asset.subarray(365565, 365565 + 229).toString('utf8');
const jsxRuntime = requireUi('react/jsx-runtime');
let originalImport = async () => [];
let originalSet = async value => value;
const Original = new Function('M', 'b', 'De', 'pn', 'fn', originalText + '\\nreturn ti;')(
  jsxRuntime, React, () => ({t}), (...args) => originalImport(...args), (...args) => originalSet(...args)
);
const baseProps = {profileId:'profile-a', currentServerId:321, homeServerId:320,
  seasonServerIds:[321,322], truckMatchServerIds:[323,321], online:true, scanActive:false,
  onJump:async serverId => ({changed:false,serverId})};
async function originalSession(body) {
  localStorage.clear(); language='en'; originalImport=async()=>[]; originalSet=async value=>value;
  const host=document.createElement('div'); document.body.append(host);
  const root=createRoot(host); let props={...baseProps};
  const rerender=async patch=>{props={...props,...patch};await flush(()=>root.render(React.createElement(Original,props)));};
  await rerender({});
  try {return await body({host,rerender,open:async()=>flush(()=>host.querySelector('.server-jump > button').click()),
    setInput:value=>inputValue(host,value), enter:()=>pressEnter(host), snapshot:()=>snapshot(host)});}
  finally {await flush(()=>root.unmount());host.remove();}
}
const cases=[];
const pass=(name,details={})=>cases.push({name,status:'PASS',...details});

// Numeric validation is frontend behavior; compare real original and current
// rendered callback outcomes, including blank conversion and upper boundaries.
const numbers=['','-1','0','1','1.5','99999','100000'];
for (const value of numbers) {
  const original=await originalSession(async h=>{
    await h.open();await h.setInput(value);await h.enter();return h.snapshot();
  });
  const current=await session('numeric '+value,null,async h=>{
    await h.open();await h.setInput(value);await h.enter();return h.snapshot();
  });
  assert.deepEqual(current,original);
  pass('numeric '+JSON.stringify(value),{original,current});
}

// A late import from the old profile must not overwrite the new profile's list.
async function staleImport(which) {
  const gate=deferred();
  if(which==='original') return originalSession(async h=>{
    originalImport=(_history,profile)=>profile==='profile-pending'?gate.promise:Promise.resolve([410]);
    await h.rerender({profileId:'profile-pending'});await h.rerender({profileId:'profile-new'});
    await flush(()=>gate.resolve([999]));await h.open();return h.snapshot();
  });
  return session('stale import',null,async h=>{
    h.setImport(()=>gate.promise);h.bridge.profileId='profile-pending';await h.rerender();
    h.setImport(async()=>[410]);h.bridge.profileId='profile-new';await h.rerender();
    await flush(()=>gate.resolve([999]));await h.open();return h.snapshot();
  });
}
const oldImportOriginal=await staleImport('original'),oldImportCurrent=await staleImport('current');
assert.deepEqual(oldImportCurrent,oldImportOriginal);
pass('obsolete profile import ignored',{original:oldImportOriginal,current:oldImportCurrent});

// Delayed history save uses the latest imported list, remains busy/open, permits
// editing, and adopts the actual history acknowledgement before closing.
async function delayedHistory(which,reject) {
  const gate=deferred(); const log=[];
  const body=async h=>{
    await h.open();await h.setInput('322');await h.enter();
    const pending=h.snapshot(); assert.equal(pending.open,true);assert.equal(pending.submit.disabled,true);
    await h.setInput('444');language='ja';await h.rerender({});
    const edited=h.snapshot();
    await flush(()=>reject?gate.reject(new Error('controlled')):gate.resolve([322,410]));
    const settled=h.snapshot();
    if(!reject)await h.open();
    return {log,pending,edited,settled,reopened:h.snapshot()};
  };
  if(which==='original') return originalSession(async h=>{
    originalImport=async()=>[410];await h.rerender({profileId:'profile-new'});
    originalSet=history=>{log.push(history);return gate.promise;};
    await h.rerender({onJump:async serverId=>{
      await h.rerender({currentServerId:serverId});return {changed:true,serverId};
    }});return body(h);
  });
  return session('delayed history',h=>{
    h.setImport(async()=>[410]);
    h.setJump(async serverId=>({changed:true,serverId}));
    h.setHistory(history=>{log.push(history);return gate.promise;});
  },body);
}
for(const reject of [false,true]) {
  const original=await delayedHistory('original',reject),current=await delayedHistory('current',reject);
  assert.deepEqual(current,original);
  pass('delayed history '+(reject?'failure':'success')+' and pending edit/locale',{original,current});
}

// Exact parent callback tolerates summary rejection. Its informational logging
// is excluded from the clone's UI comparison; dispatch/summary/result order is not.
const parentLog=[];
const parent=new Function('Kt','_t','F',originalParentText+'\\nreturn Mt;')(
 async id=>{parentLog.push('jump:'+id);return {changed:false,serverId:id};},
 async()=>{parentLog.push('summary');throw new Error('controlled');},()=>{}
);
assert.deepEqual(await parent(321),{changed:false,serverId:321});
assert.deepEqual(parentLog,['jump:321','summary']);
pass('exact original parent summary rejection contract',{parentLog});

const report={result:'LWB317_CROSSSERVER_INDEPENDENT_OK',cases,
 original:{assetSha256:sha256(asset),popoverOffset:350405,popoverLength:3406,parentOffset:365565,parentLength:229},
 current:{appSha256:sha256(source)},
 limits:'Actual original bytes and current App hooks/rendered callbacks with controlled inert APIs and page children. No native/game/network action; no new browser observations. Failed subagent produced no review evidence; lead executed these independently.'};
fs.writeFileSync(${JSON.stringify(path.join(here,'independent-results.json'))},JSON.stringify(report,null,2)+'\\n');
console.log('LWB317_CROSSSERVER_INDEPENDENT_OK cases='+cases.length);
`;
try {await import('data:text/javascript;base64,' + Buffer.from(scaffold + extra).toString('base64'));}
catch(error) {console.error(error.message);process.exitCode=1;}
