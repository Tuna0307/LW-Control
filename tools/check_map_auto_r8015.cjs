const assert = require('assert/strict');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const originalIndex = fs.readFileSync(path.join(root,
  'evidence', 'lwbridge-0.3.1', 'frontend', 'assets', 'index-sfL2sT3K.js'), 'utf8');
const index = fs.readFileSync(path.join(root,
  'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'index-sfL2sT3K.js'), 'utf8');
const originalPanel = fs.readFileSync(path.join(root,
  'evidence', 'lwbridge-0.3.1', 'frontend', 'assets', 'MapDataPanel-C1HVeNHr.js'), 'utf8');
const panel = fs.readFileSync(path.join(root,
  'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'MapDataPanel-C1HVeNHr.js'), 'utf8');

function sliceBetween(text, start, end) {
  const a = text.indexOf(start);
  assert.notEqual(a, -1, `missing start anchor: ${start}`);
  const b = text.indexOf(end, a);
  assert.notEqual(b, -1, `missing end anchor: ${end}`);
  return text.slice(a, b);
}

const blocks = [
  ['Auto config/sanitizer helpers', 'var Un=new Set(', 'var er='],
  ['Auto scheduler-owned state', 'P=Dt===`connected`,', 'At.current='],
  ['Auto scheduler effect', '(0,j.useEffect)(()=>{let e=!1,n=u.selectedProfileId;async function r(){', ',(0,M.jsxs)(M.Fragment,{children:['],
];
for (const [label, start, end] of blocks) {
  assert.equal(sliceBetween(index, start, end), sliceBetween(originalIndex, start, end),
    `${label} must be byte-identical to immutable 0.3.1`);
}

const cardStart = 'Y===`auto`&&(0,D.jsxs)(`div`,{className:`map-auto-scan-card`';
const cardEnd = ',(0,D.jsxs)(`div`,{className:`map-scan-summary`';
assert.equal(sliceBetween(panel, cardStart, cardEnd), sliceBetween(originalPanel, cardStart, cardEnd),
  'Auto Scan card must be byte-identical to immutable 0.3.1');

assert.ok(index.includes('currentServerId:ze?.serverId||0'),
  'top-level current-server prop must use the original scan-state server field');
assert.equal(index.includes('currentServerId:ze?.liveServerId||ze?.serverId||0'), false,
  'rebuild-only liveServerId fallback must remain absent');

for (const removed of [
  'runOnceRequestedAt', 'autoOnlineRef', 'autoCycleRequested',
  'mapAutoScanCycle', 'autoRestartRecovery',
  'automatic map scan cycle finished completed=',
]) assert.equal(index.includes(removed), false, `R7 Auto token must be absent: ${removed}`);

for (const removed of ['runOnceRequestedAt', 'stopAutoScan'])
  assert.equal(panel.includes(removed), false, `R7 Auto panel token must be absent: ${removed}`);

console.log('R8-015 strict Auto Scan scheduler/frontend contract check passed.');
