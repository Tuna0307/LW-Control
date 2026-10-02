import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');

function sha(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
}

const expected = {
  executable: ['C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe', '4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783'],
  mapPanel: [path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js'), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089'],
  main: [path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6'],
  rewards: [path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/rewardDisplay-eZWrd6iS.js'), '7F65DD3F5B81C96117AF5E83E8310D6C6A3A48787B1F693D387020255C78E73E'],
  truckScreenshot: [path.join(here, 'truck-en.png'), '994E88261AAC12ED12EEAC965B6228A8530734FF3299AAE4C95AC77558FB5ABB'],
  trainScreenshot: [path.join(here, 'train-ja.png'), '3E1B29E8873C023C51B662A0F524D231D266F1AC6F34B5C68F59C936DAA03D9E'],
};

for (const [name, [file, digest]] of Object.entries(expected)) {
  assert.ok(fs.existsSync(file), `${name} missing: ${file}`);
  assert.equal(sha(file), digest, `${name} SHA-256`);
}

const transport = JSON.parse(fs.readFileSync(path.join(here, 'transport-review-results.json'), 'utf8'));
assert.equal(transport.task, 'LWB317-REVIEW-MAP-TRANSPORT-001');
assert.equal(transport.result, 'CHANGES_REQUIRED');
assert.equal(transport.mismatchCount, 14);
assert.deepEqual([...new Set(transport.mismatches.map(entry => entry.category))].sort(), ['live-target', 'truck-selection-label']);
assert.equal(transport.source.mapPanel.columnFactory.utf8ByteOffset, 9594);
assert.equal(transport.source.mapPanel.liveTargetRenderer.utf8ByteOffset, 15490);
assert.equal(transport.source.mapPanel.rewardRenderer.utf8ByteOffset, 16260);
assert.equal(transport.source.mapPanel.selectionRenderer.utf8ByteOffset, 17072);
assert.equal(transport.source.mapPanel.itemFilter.utf8ByteOffset, 53914);
assert.equal(transport.source.main.truckMaximum.utf8ByteOffset, 201275);
assert.equal(transport.source.main.truckState.utf8ByteOffset, 201399);
assert.equal(transport.source.rewards.compactCount.utf8ByteOffset, 0);

const browser = JSON.parse(fs.readFileSync(path.join(here, 'browser-results.json'), 'utf8'));
assert.equal(browser.consoleErrors, 0);
assert.equal(browser.checks.truckStates.nativeActionControlsDisabled, true);
assert.equal(browser.checks.retainedGoodsSort.afterItemSort.itemsAriaSort, 'descending');
assert.equal(browser.checks.retainedGoodsSort.afterClear.itemsAriaSort, null);
assert.equal(browser.checks.trainJapanese.hasSelectionColumn, false);
assert.deepEqual(browser.checks.trainJapanese.headers, ['リアルタイム対象', '連盟', '品質', '戦力', 'アイテム', '保護時間', '更新日時']);

const consoleErrors = JSON.parse(fs.readFileSync(path.join(here, 'console-errors.json'), 'utf8'));
assert.equal(consoleErrors.count, 0);
assert.deepEqual(consoleErrors.entries, []);

console.log('LWB317-REVIEW-MAP-TRANSPORT-001 evidence validation passed.');
