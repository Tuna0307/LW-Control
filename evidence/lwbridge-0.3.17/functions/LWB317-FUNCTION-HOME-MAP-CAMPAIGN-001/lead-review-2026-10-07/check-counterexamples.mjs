import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {fileURLToPath, pathToFileURL} from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const ui = path.join(root, 'src/LWBridge.UI-0.3.17/src');
const app = fs.readFileSync(path.join(ui, 'App.jsx'), 'utf8');
const panel = fs.readFileSync(path.join(ui, 'MapDataPage.jsx'), 'utf8');
const helpers = await import(pathToFileURL(path.join(ui, 'mapAutoConfig.js')));
const {createAutoScanNativeCoordinator} = await import(pathToFileURL(path.join(ui, 'autoScanNativeCoordinator.js')));
const extract = (source, start, end) => {
  const from = source.indexOf(start), to = source.indexOf(end, from);
  assert.ok(from >= 0 && to > from, 'Exact current-source anchors missing');
  return source.slice(from, to);
};
const results = [];
{
  const source = extract(app, '  const updateAutoLaunch = useCallback(', '  const updateAutoReconnect = useCallback(');
  const owner = {profileId:'B', generation:2};
  let visible = false, stored = false, error = '';
  const make = new Function('useCallback','backendBridge','selectedProfileOwnerRef','profileOwnerKey',
    'autoLaunchGame','autoLaunchSaveRevisionRef','writeAutoLaunchGamePreference','localStorage',
    'setAutoLaunchGame','setGameActionError','autoLaunchSaveChainRef','isCurrentProfileOwner',
    'autoLaunchNativeCommitEpochRef','autoLaunchNativeCommittedByOwnerRef', source + '\nreturn updateAutoLaunch;');
  const update = make(f=>f, {available:true,invoke:async()=>{throw new Error('native persistence rejected');}},
    {current:owner}, o=>`${o.generation}:${o.profileId}`, visible, {current:0},
    (_,value)=>{stored=value;}, {}, value=>{visible=value;}, value=>{error=value;},
    {current:Promise.resolve()}, ()=>true, {current:0}, {current:new Map([['2:B',true]])});
  await update(true);
  assert.equal(visible, true);
  assert.equal(stored, true);
  results.push({case:'global-false-native-B-true-rejected-enable', expectedGlobal:false,
    actualGlobal:visible, actualStored:stored, actionError:error, defectReproduced:true});
}
{
  const initial = extract(app, 'function initialAutoScanConfig(', 'function RetainedPages(');
  const initialize = new Function('backendBridge','normalizeAutoScanConfig','loadAutoScanConfig',
    'AUTO_SCAN_DEFAULT_TYPES','window', initial+'\nreturn initialAutoScanConfig;')(
    {mode:'native'}, helpers.normalizeAutoScanConfig, helpers.loadAutoScanConfig, helpers.AUTO_SCAN_DEFAULT_TYPES, {});
  const persisted = helpers.normalizeAutoScanConfig({enabled:true,serverIds:[317], selectedTypes:['city'],
    intervalMinutes:60,scanMode:'normal',returnToOriginalServer:false});
  const visible = initialize('A','');
  let write;
  const coordinator = createAutoScanNativeCoordinator({
    saveConfig:async config=>{write=config;return {revision:2,config};},
    readStatus:async()=>({revision:1,config:persisted}),runNow:async()=>null,
    mergeConfig:(base,edit)=>helpers.applyAutoScanConfigEdit(base,{...base,...edit.patch},edit.editedAt),
    onConfigSnapshot:()=>{},onRuntimeSnapshot:()=>{},onWriteError:()=>{},onActionError:()=>{},
  });
  let patch;
  const addSource=extract(panel,'  function addAutoServers() {','  function toggleAutoType(');
  const add=new Function('parseAutoServerIds','appendAutoServerIds','autoConfig','autoServerInput',
    'emitAutoConfig','setAutoServerInput',addSource+'\nreturn addAutoServers;')(
    helpers.parseAutoServerIds,helpers.appendAutoServerIds,visible,'9',value=>{patch=value;},()=>{});
  add();
  await coordinator.save({...visible,...patch},{patch,editedAt:1800000000000});
  assert.deepEqual(write.serverIds,[9]);
  results.push({case:'real-add-handler-before-hydration-drops-saved-server',expectedServers:[317,9],
    actualServers:write.serverIds, defectReproduced:true});
  coordinator.retire();
}
const sources=['App.jsx','MapDataPage.jsx','mapAutoConfig.js','autoScanNativeCoordinator.js'].map(name=>({
  path:'src/LWBridge.UI-0.3.17/src/'+name,
  sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(ui,name))).digest('hex'),
}));
fs.writeFileSync(path.join(here,'counterexamples.json'),JSON.stringify({
  reviewedHead:'59d3cde5192f03d39ae01580e05d1a6249158b20',proofType:'exact-current-App/panel-callbacks-and-coordinator; controlled native promises; no mounted browser',
  externalActions:0,nativeCommands:0,sources,results,
},null,2)+'\n');
console.log('LWB317_RECOVERY002_LEAD_DEFECTS_REPRODUCED 2/2');
