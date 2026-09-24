const assert = require('assert/strict');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const originalApi = fs.readFileSync(path.join(root, 'evidence', 'lwbridge-0.3.1', 'frontend', 'assets', 'api-ClPPi2JT.js'), 'utf8');
const api = fs.readFileSync(path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'api-ClPPi2JT.js'), 'utf8');
const originalPanel = fs.readFileSync(path.join(root, 'evidence', 'lwbridge-0.3.1', 'frontend', 'assets', 'MapDataPanel-C1HVeNHr.js'), 'utf8');
const panel = fs.readFileSync(path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'MapDataPanel-C1HVeNHr.js'), 'utf8');

function sliceBetween(text, start, end) {
  const a = text.indexOf(start);
  assert.notEqual(a, -1, `missing start anchor: ${start}`);
  const b = text.indexOf(end, a);
  assert.notEqual(b, -1, `missing end anchor: ${end}`);
  return text.slice(a, b);
}

for (const [label, original, generated, start, end] of [
  ['Scheduled Plunder API wrappers', originalApi, api,
    'function qt(){return U(`map_plunder_jobs_list`)}',
    'function Qt(e,t){return U(`map_player_mark_set`'],
  ['Scheduled Plunder tab/status definitions', originalPanel, panel,
    'var x=[[', 'var Ce='],
  ['Scheduled Plunder tables/components', originalPanel, panel,
    'function it({jobs:', 'function ot({items:'],
]) {
  assert.equal(sliceBetween(generated, start, end), sliceBetween(original, start, end),
    `${label} must be byte-identical to immutable 0.3.1`);
}

for (const token of [
  'te(`bridge://dispatch-plunder-changed`,Q)',
  'te(`bridge://truck-plunder-changed`,Q)',
  'F===`scheduledPlunder`&&Q()',
  'map.randomDelaySeconds',
  'map.scheduleSelected',
  'map.scheduleSelectedTrucks',
  'map.scheduledPlunder',
  'map.plunderAgain',
  'map.plunderRewards',
  'F===`scheduledPlunder`&&(0,D.jsx)(it,{jobs:sn',
  'F===`scheduledPlunder`&&(0,D.jsx)(at,{jobs:ln',
]) assert.ok(panel.includes(token), `missing restored Scheduled Plunder UI token: ${token}`);

assert.ok(panel.includes('selectedTypes:t.target.checked?[...S.selectedTypes,e.key]:S.selectedTypes.filter(t=>t!==e.key)'),
  'Auto scan kinds must retain the original scan-type selector');
assert.equal(panel.includes('selectedTypes:[...S.selectedTypes,`scheduledPlunder`]'), false,
  'Scheduled Plunder must never become a scan kind');

for (const file of fs.readdirSync(path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets'))
  .filter(name => /^(en|id|ja|ko|pt|ru|vi|zh-CN|zh-TW)-.*\.js$/.test(name))) {
  const locale = fs.readFileSync(path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', file), 'utf8');
  for (const key of ['map.scheduledPlunder', 'map.scheduleSelected', 'map.scheduleSelectedTrucks',
    'map.randomDelaySeconds', 'map.plunderResult', 'map.plunderRewards']) {
    assert.ok(locale.includes(key), `${file} missing restored locale key ${key}`);
  }
}

const desktop = path.join(root, 'src', 'LWBridge.Desktop');
for (const removed of ['DispatchPlunderWorker.cs', 'TruckPlunderWorker.cs', 'MapDataStore.Plunder.cs'])
  assert.equal(fs.existsSync(path.join(desktop, removed)), false, `protected worker/runtime file must remain absent: ${removed}`);

const control = fs.readFileSync(path.join(desktop, 'MapDataStore.PlunderControlPlane.cs'), 'utf8');
for (const forbidden of ['ReadArmable', 'TryMark', 'RecordTruckPlunderSuccess', 'RecoverTruckPlunder', 'RecoverDispatchPlunder'])
  assert.equal(control.includes(forbidden), false, `execution helper must remain absent: ${forbidden}`);

console.log('R8-016 Scheduled Plunder control-plane/frontend check passed.');
