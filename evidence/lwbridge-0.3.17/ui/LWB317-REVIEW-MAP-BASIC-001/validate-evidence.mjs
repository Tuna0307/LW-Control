import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, '../../../..');
const hash = b => crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const json = name => JSON.parse(fs.readFileSync(path.join(here, name), 'utf8'));
const manifest = json('baseline-manifest.json'), baseline = json('baseline-results.json');
assert.equal(manifest.head, '6a03a328f25e475bf8e0a8e711d172b62b25fe29');
for (const f of manifest.files) assert.equal(hash(fs.readFileSync(path.join(repo, f.snapshot))), f.sha256);
assert.equal(baseline.mode, 'immutable-baseline');
assert.equal(baseline.status, 'CHANGES_REQUIRED');
assert.equal(baseline.passedAssertions, 268);
assert.equal(baseline.failedAssertions, 104);
assert.deepEqual([...new Set(baseline.failures.map(f => f.id))].sort(), ['city mark presentation', 'coordinate busy predicate', 'coordinate presentation', 'row classes', 'row identity']);
const exe = 'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe';
assert.equal(hash(fs.readFileSync(exe)), '4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783');
function validateResult(result) {
  for (const [p, expected] of Object.entries(result.files)) assert.equal(hash(fs.readFileSync(path.join(repo, p))), expected, p);
  const asset = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js'));
  const index = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'));
  for (const [name, { byte, expression }] of Object.entries(result.locators)) {
    const source = name === 'icon' ? index : asset;
    assert.equal(source.subarray(byte, byte + Buffer.byteLength(expression)).toString('utf8'), expression);
  }
}
validateResult(baseline);
if (!process.argv.includes('--baseline-only')) {
  const current = json('current-results.json');
  assert.equal(current.mode, 'current');
  assert.equal(current.status, 'ACCEPT_SOURCE_LOCAL');
  assert.equal(current.failedAssertions, 0);
  validateResult(current);
  const browserReview = json('browser-review.json');
  assert.equal(hash(fs.readFileSync(path.join(repo, browserReview.source))), browserReview.sha256);
  for (const image of browserReview.images) assert.equal(hash(fs.readFileSync(path.join(repo, image.path))), image.sha256);
  const browser = JSON.parse(fs.readFileSync(path.join(repo, browserReview.source), 'utf8'));
  assert.equal(browser.fixture, 'map-row-actions');
  assert.equal(browser.online, false);
  assert.equal(browser.synthetic, true);
  assert.deepEqual(browser.consoleErrors, []);
  const flow = (kind, language) => { const f = browser.flows.find(f => f.kind === kind && f.language === language); assert.ok(f, `${kind}/${language}`); return f; };
  for (const [kind, language] of [['city', 'en'], ['city', 'ja'], ['resource', 'en'], ['monster', 'en']]) {
    const observed = flow(kind, language);
    assert.ok(observed.rows.every(r => r.actions.every(a => a.disabled === true)), 'native controls remain fenced');
    if (kind === 'city') {
      const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
      assert.equal(observed.rows[0].className, 'map-row is-marked is-missing');
      assert.equal(observed.rows[1].className, 'map-row is-replaced');
      assert.equal(observed.rows[0].actions[0].title, `${catalog['map.unmarkPlayer']} · ${catalog['map.positionMissing']}`);
      assert.equal(observed.rows[1].actions[0].title, `${catalog['map.markPlayer']} · ${catalog['map.positionReplaced']}`);
      assert.equal(observed.rows[0].cells[1], `411,521${catalog['map.jump']}`);
      assert.equal(observed.rows[2].cells[1], `413,523${catalog['map.jumping']}`);
      for (const row of observed.rows.slice(3, 5)) { assert.equal(row.cells[1], '-'); assert.equal(row.actions.length, 1); }
      if (language === 'en') { assert.equal(observed.rows[0].actions[0].svg, 'ui-icon is-filled'); assert.equal(observed.rows[1].actions[0].svg, 'ui-icon'); }
    }
  }
  assert.deepEqual(flow('resource', 'en').rows.slice(0, 3).map(r => r.cells[3]), ['—', 'Idle', 'Gathering']);
  assert.deepEqual(flow('monster', 'en').rows.slice(0, 2).map(r => [r.cells[1], r.cells[3]]), [['Fixture Monster A Label', '47'], ['Fixture Monster B Label', '54']]);
  const peer = json('peer-review.json');
  assert.equal(peer.recommendation, 'ACCEPT focused source/local Map row correction');
  assert.deepEqual(peer.blockers, []);
  assert.equal(peer.distinguishingCases, 41);
  for (const [p, expected] of Object.entries(peer.finalHashes)) assert.equal(hash(fs.readFileSync(path.join(repo, p))), expected);
}
console.log('LWB317_REVIEW_MAP_BASIC_EVIDENCE_OK' + (process.argv.includes('--baseline-only') ? ' baseline-only' : ' baseline-and-current'));
