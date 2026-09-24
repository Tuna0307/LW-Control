const assert = require('assert/strict');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const panel = fs.readFileSync(
  path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'MapDataPanel-C1HVeNHr.js'),
  'utf8');

assert.ok(panel.includes('function Ue(e){return e===`resource`||e===`monster`}'),
  'name selectors must remain Resource/Monster only');
assert.ok(panel.includes('monsterNameKey:n===`monster`?Ot.monster:void 0'),
  'Monster name selector must remain Monster-only');
assert.ok(panel.includes('minLevel:n===`dispatch`&&Wt?Number(Wt):void 0,maxLevel:n===`dispatch`&&Wt?Number(Wt):void 0'),
  'level bounds must remain Dispatch-only');

for (const removed of [
  'zombie_boss', 'monsterLevelSteps', 'monsterNameKeys', 'resourceLevel',
  'resourceIdleOnly', 'resourceFullOnly', 'excludeBlackTile',
]) {
  assert.equal(panel.includes(removed), false,
    `rebuild-only public search token must remain absent: ${removed}`);
}

console.log('R8-014 strict map_search frontend contract check passed.');
