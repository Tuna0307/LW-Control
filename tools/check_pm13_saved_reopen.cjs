const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const repoRoot = path.resolve(__dirname, '..');
function durablePath(value) {
  const rel = path.relative(repoRoot, value);
  return !rel.startsWith('..') && !path.isAbsolute(rel) ? rel.replaceAll('\\', '/') : value;
}

function arg(name) {
  const i = process.argv.indexOf(name);
  if (i < 0 || i + 1 >= process.argv.length) throw new Error(`missing ${name}`);
  return process.argv[i + 1];
}
function pick(o, ...names) {
  for (const n of names) if (o && Object.prototype.hasOwnProperty.call(o, n)) return o[n];
  return undefined;
}
function normalizedExpected(proof) {
  const e = proof.second || proof.expected || proof;
  const row = pick(e, 'searchRow', 'SearchRow') || {};
  return {
    profileId: pick(e, 'profileId', 'ProfileId'),
    serverId: Number(pick(e, 'serverId', 'ServerId')),
    recordKey: String(pick(e, 'recordKey', 'RecordKey')),
    pointIndex: Number(pick(e, 'pointIndex', 'PointIndex')),
    x: Number(pick(e, 'x', 'X')),
    y: Number(pick(e, 'y', 'Y')),
    level: pick(e, 'level', 'Level'),
    updatedAt: Number(pick(row, 'updatedAt', 'UpdatedAt') ?? pick(e, 'capturedAtUnixMilliseconds', 'CapturedAtUnixMilliseconds'))
  };
}
function requireExpected(e) {
  for (const [k, v] of Object.entries(e)) {
    if (k === 'profileId' || k === 'level') continue;
    if (v === undefined || v === null || (typeof v === 'number' && !Number.isFinite(v)))
      throw new Error(`expected proof is missing ${k}`);
  }
}
function sameRow(row, e) {
  return Number(row?.serverId) === e.serverId &&
    String(row?.recordKey) === e.recordKey &&
    Number(row?.pointIndex) === e.pointIndex &&
    Number(row?.x) === e.x && Number(row?.y) === e.y &&
    (e.level == null || Number(row?.level) === Number(e.level)) &&
    Number(row?.updatedAt) === e.updatedAt;
}

(async () => {
  const port = Number(arg('--port'));
  const proofPath = path.resolve(arg('--proof'));
  const outputPath = path.resolve(arg('--output'));
  const screenshotPath = path.resolve(arg('--screenshot'));
  const expected = normalizedExpected(JSON.parse(fs.readFileSync(proofPath, 'utf8')));
  requireExpected(expected);

  const browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
  try {
    const context = browser.contexts()[0];
    const pages = context.pages();
    const page = pages.find(p => p.url().startsWith('https://lwbridge.local/')) || pages[0];
    if (!page) throw new Error('normal LWBridge WebView page was not found');
    await page.waitForFunction(() => window.LWBridgePreview?.invoke && document.querySelectorAll('.map-tabs button').length > 1);
    await page.evaluate(() => {
      const bridge = window.LWBridgePreview;
      if (window.__pm13ReopenWrapped) return;
      const original = bridge.invoke.bind(bridge);
      window.__pm13ReopenSearch = null;
      bridge.invoke = async (command, payload = {}) => {
        const result = await original(command, payload);
        if (command === 'map_search') {
          window.__pm13ReopenSearch = { payload, result, capturedAt: Date.now() };
        }
        return result;
      };
      window.__pm13ReopenWrapped = true;
    });

    await page.locator('.map-tabs button').nth(1).click();
    await page.waitForFunction(() => {
      const table = document.querySelector('.map-table--resource');
      return !!table && table.getAttribute('aria-busy') !== 'true';
    });
    const search = page.locator('.map-searchbar > button');
    if (await search.isDisabled()) throw new Error('normal Resource Search button is disabled after reopen');
    await search.click();
    await page.waitForFunction(() => window.__pm13ReopenSearch !== null);
    await page.waitForFunction(() => document.querySelector('.map-table--resource')?.getAttribute('aria-busy') !== 'true');
    const observation = await page.evaluate(() => window.__pm13ReopenSearch);
    if (expected.profileId && observation?.payload?.profileId !== expected.profileId)
      throw new Error(`reopen Search profile mismatch: ${observation?.payload?.profileId}`);
    const query = observation?.payload?.query || {};
    if (observation?.payload?.kind !== 'resource' || Number(query.serverId) !== expected.serverId || Number(query.page) !== 1)
      throw new Error('reopen did not issue the expected page-1 Resource Search query');
    const rows = observation?.result?.rows;
    if (!Array.isArray(rows)) throw new Error('reopen Resource Search returned no rows array');
    const matched = rows.find(row => sameRow(row, expected));
    if (!matched) throw new Error('reopen Resource Search did not return the exact saved acquisition row');

    const expectedTime = await page.evaluate(ms => new Date(ms).toLocaleString(document.documentElement.lang || undefined), expected.updatedAt);
    const rendered = await page.locator('.map-table--resource tbody tr').evaluateAll(rows => rows.map(row => ({
      isEmpty: !!row.querySelector('td.map-empty'),
      coordinateText: (row.querySelector('.map-coordinate-button span:not(.map-coordinate-icon)')?.textContent || '').trim(),
      cells: [...row.querySelectorAll('td')].map(td => (td.innerText || '').trim())
    })));
    const renderedMatch = rendered.find(row => !row.isEmpty && row.cells.length >= 5 &&
      row.coordinateText === `${expected.x},${expected.y}` &&
      (expected.level == null || row.cells[2] === String(expected.level)) &&
      row.cells[4] === expectedTime);
    if (!renderedMatch) throw new Error(`reopen table did not render the exact saved coordinates/level/time; expectedTime=${expectedTime}; rendered=${JSON.stringify(rendered)}`);

    fs.mkdirSync(path.dirname(outputPath), { recursive: true });
    fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });
    await page.screenshot({ path: screenshotPath, fullPage: true });
    const output = {
      schemaVersion: 1,
      proof: 'normal_window_saved_reopen',
      proofSource: durablePath(proofPath),
      expected,
      searchPayload: observation.payload,
      searchRow: matched,
      renderedCoordinateText: renderedMatch.coordinateText,
      renderedCells: renderedMatch.cells,
      screenshotPath: durablePath(screenshotPath),
      pageUrl: page.url(),
      verifiedAt: new Date().toISOString()
    };
    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2) + '\n');
    console.log(JSON.stringify(output, null, 2));
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(error?.stack || String(error));
  process.exitCode = 1;
});
