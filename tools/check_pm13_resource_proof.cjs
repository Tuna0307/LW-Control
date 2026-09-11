/* PM13-02 browser DOM checks for the hardened normal-window resource proof. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('playwright');

const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'src/LWBridge.Desktop/NormalUiResourceProofContract.cs'), 'utf8');
const match = source.match(/ResourceTableSnapshotScript\s*=\s*"""\r?\n([\s\S]*?)\r?\n\s*""";/);
assert.ok(match, 'Could not extract ResourceTableSnapshotScript from production proof contract');
const snapshotScript = match[1];

const cases = [
  ['empty-state', false, '<tr><td class="map-empty" colspan="5">No saved data of this type.</td></tr>'],
  ['loading-state', true, '<tr><td class="map-empty" colspan="5">Processing</td></tr>'],
  ['stale-unrelated', false, '<tr><td>9,9</td><td>Unknown resource</td><td>3</td><td>Idle</td><td>Yesterday</td></tr>'],
  ['same-point-stale-time', false, '<tr><td>481,32</td><td>Unknown resource</td><td>3</td><td>Idle</td><td>9/10/2026, 1:13:45 PM</td></tr>'],
  ['exact-row', false, '<tr><td>481,32</td><td>Unknown resource</td><td>3</td><td>Idle</td><td>9/10/2026, 1:14:45 PM</td></tr>'],
];

async function main() {
  const browser = await chromium.launch({channel: process.env.LWBRIDGE_BROWSER || 'msedge', headless: true});
  const page = await browser.newPage();
  const results = [];
  try {
    for (const [name, busy, row] of cases) {
      await page.setContent(`<table class="map-table--resource" aria-busy="${busy}"><tbody>${row}</tbody></table>`);
      const snapshot = await page.evaluate(snapshotScript);
      assert.ok(snapshot && Array.isArray(snapshot.rows) && snapshot.rows.length === 1, `${name}: one row snapshot expected`);
      assert.equal(snapshot.busy, busy, `${name}: busy state must be preserved`);
      results.push({name, snapshot});
    }    assert.equal(results[0].snapshot.rows[0].isEmpty, true, 'empty-state must be marked as empty');
    assert.equal(results[1].snapshot.rows[0].isEmpty, true, 'loading-state row uses the recovered empty-cell shape');
    assert.equal(results[2].snapshot.rows[0].isEmpty, false, 'stale unrelated row is structurally a data row');
    assert.deepEqual(results[3].snapshot.rows[0].cells,
      ['481,32','Unknown resource','3','Idle','9/10/2026, 1:13:45 PM'],
      'same-point stale row cells must be preserved for C# timestamp rejection');
    assert.deepEqual(results[4].snapshot.rows[0].cells,
      ['481,32','Unknown resource','3','Idle','9/10/2026, 1:14:45 PM'],
      'exact row cells must be preserved for positive correlation');
    process.stdout.write(JSON.stringify({ok:true, cases:results}, null, 2) + '\n');
  } finally {
    await page.close();
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
