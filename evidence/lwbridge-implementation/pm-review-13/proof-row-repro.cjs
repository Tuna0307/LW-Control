// PM13-02: isolated DOM counterexamples to the current UI proof predicate.
// No app/game launch, native calls, or acquired data. All rows are synthetic.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {chromium} = require('playwright');
(async () => {
  const source = fs.readFileSync(path.resolve(__dirname, '../../../src/LWBridge.Desktop/LWBridgeWindow.cs'), 'utf8');
  const match = source.match(/"(\(\(\)=>document\.querySelector\('\.map-table--resource tbody tr'\).*?)"\);/);
  assert.ok(match, 'Audited source predicate changed; review this reproducer before reuse');
  const browser = await chromium.launch({channel:process.env.LWBRIDGE_BROWSER || 'msedge',headless:true});
  try {
    const page = await browser.newPage();
    const results = [];
    for (const [name, row] of [
      ['empty-state', '<tr><td colspan="6">No saved data of this type.</td></tr>'],
      ['loading-state', '<tr><td colspan="6">Processing</td></tr>'],
      ['stale-unrelated-row', '<tr><td>Old point 999</td><td>Yesterday</td></tr>'],
    ]) {
      await page.setContent('<table class="map-table--resource"><tbody>'+row+'</tbody></table>');
      const rowText = await page.evaluate(match[1]);
      const acceptedByCurrentPredicate = typeof rowText === 'string' && rowText.trim().length > 0;
      assert.equal(acceptedByCurrentPredicate, true);
      results.push({name, acceptedByCurrentPredicate, expectedForFreshProof:false, rowText});
    }
    process.stdout.write(JSON.stringify({scope:'synthetic DOM only', sourcePredicate:match[1], defectReproduced:true, results}, null, 2)+'\n');
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
