const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const candidate = path.join(root, 'src', 'LWBridge.Desktop', 'WebUi');

async function main() {
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const rel = url.pathname.slice(1) || 'index.html';
    try {
      const file = path.resolve(candidate, rel);
      if (!file.startsWith(candidate + path.sep) && file !== path.join(candidate, 'index.html'))
        throw new Error('outside root');
      const data = fs.readFileSync(file);
      res.setHeader('Content-Type',
        rel.endsWith('.js') ? 'text/javascript' :
        rel.endsWith('.css') ? 'text/css' : 'text/html');
      res.end(data);
    } catch {
      res.writeHead(404);
      res.end();
    }
  });
  const listenPort = Number(process.env.LWBRIDGE_R7131_PORT || 18081);
  server.listen(listenPort, '127.0.0.1');
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });
  const origin = `http://127.0.0.1:${listenPort}`;

  const browser = await chromium.launch({ channel: process.env.LWBRIDGE_BROWSER || 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(String(error.stack || error)));
  await page.addInitScript(() => {
    const profile = 'local-1';
    const seed = (key, value) => {
      if (localStorage.getItem(key) === null) localStorage.setItem(key, value);
    };
    seed(`lwbridge.mapManualScanTypes.${profile}`, JSON.stringify(['city', 'monster']));
    seed(`lwbridge.mapScanTab.${profile}`, 'auto');
    seed(`lwbridge.mapResultTab.${profile}`, 'monster');
    seed(`lwbridge.mapBrowseServer.${profile}`, '2213');

    Object.defineProperty(window, 'LWBridgePreview', {
      configurable: true,
      get() { return undefined; },
      set(value) {
        const originalInvoke = value.invoke.bind(value);
        const counts = {
          city: 2, resource: 0, monster: 3, zombie_boss: 0,
          truck: 1, railway: 0, dispatch: 0, ghost: 0, treasure: 0
        };
        const runtime = {
          calls: [],
          currentServer: 2212,
          delaySearchFailure: false,
          scanState: {
            serverId: 2212, serverIdSource: 'live', scanRunId: '', isReading: false,
            phase: 'idle', selectedTypes: ['city'], totalBlocks: 2500, readBlocks: 0,
            unreadBlocks: 2500, failedBlocks: 0, inflightBlocks: 0,
            scanMode: 'fast', concurrency: 20, retryCount: 2, scanRate: 0,
            progressPercent: 0, nativeCaptureReady: true, nativePendingRecords: 0,
            nativeDroppedRecords: 0, homeServerId: 2212, seasonServerIds: [], truckMatchServerIds: []
          }
        };
        window.__r7131 = runtime;

        const clone = object => object == null ? object : structuredClone(object);
        value.invoke = async (command, payload = {}) => {
          runtime.calls.push({ command, payload: clone(payload), at: Date.now() });
          if (command === 'get_status')
            return { xluaOnline: true, pending: 0, config: { tasks: {} } };
          if (command === 'proxy_status')
            return { gameRunning: true, repairRequired: false };
          if (command === 'map_scan_status')
            return clone(runtime.scanState);
          if (command === 'map_summary')
            return { serverId: runtime.scanState.isReading ? runtime.currentServer : 2212,
              savedServerIds: [2212, 2213], counts: clone(counts), scanState: clone(runtime.scanState) };
          if (command === 'map_data_options')
            return { serverId: Number(payload.serverId || 2212), alliances: [], names: { resource: [], monster: [], zombie_boss: [] },
              dispatchLevels: [], monsterLevels: [5, 10], counts: clone(counts), rewardItems: { truck: [], railway: [] },
              treasureTypes: [], noAllianceCount: 0, scanProgress: null };
          if (command === 'map_search') {
            if (runtime.delaySearchFailure) {
              await new Promise(resolve => setTimeout(resolve, 350));
              throw new Error('Saved map search failed stale fixture');
            }
            return { rows: [], total: 0 };
          }
          if (command === 'server_jump') {
            const target = Number(payload.serverId);
            const previousServerId = runtime.currentServer;
            runtime.currentServer = target;
            runtime.scanState.serverId = target;
            runtime.scanState.serverIdSource = 'live';
            return { changed: target !== previousServerId, previousServerId, serverId: target, currentServerId: target };
          }
          if (command === 'map_scan_start') {
            runtime.scanState = {
              ...runtime.scanState,
              serverId: runtime.currentServer,
              serverIdSource: 'live',
              scanRunId: 'fixture-auto-run',
              isReading: true,
              phase: 'scanning',
              selectedTypes: Array.isArray(payload.selectedTypes) ? [...payload.selectedTypes] : ['city'],
              totalBlocks: 2500,
              readBlocks: 0,
              unreadBlocks: 2500,
              progressPercent: 0
            };
            return clone(runtime.scanState);
          }
          if (command === 'map_scan_stop') {
            runtime.scanState = {
              ...runtime.scanState,
              isReading: false, phase: 'idle', inflightBlocks: 0,
              scanRunId: '', readBlocks: 0, unreadBlocks: 0, totalBlocks: 0, progressPercent: 0
            };
            return clone(runtime.scanState);
          }
          if (command === 'map_scan_clear') {
            runtime.scanState = {
              ...runtime.scanState,
              serverId: Number(payload.serverId || runtime.currentServer),
              isReading: false, phase: 'idle', scanRunId: '',
              totalBlocks: 0, readBlocks: 0, unreadBlocks: 0, failedBlocks: 0,
              inflightBlocks: 0, progressPercent: 0
            };
            return clone(runtime.scanState);
          }
          return originalInvoke(command, payload);
        };

        Object.defineProperty(window, 'LWBridgePreview', {
          configurable: true, writable: true, value
        });
      }
    });
  });
  try {
    await page.goto(`${origin}/index.html?view=map-data&language=en`);
    await page.locator('.panel.map-panel').waitFor();
    await page.waitForTimeout(500);

    // R7-130 persistence: the seeded per-profile state must be honored by the shipped bundle.
    const scanTabs = page.locator('.map-scan-tabs button');
    assert.equal(await scanTabs.nth(1).getAttribute('aria-selected'), 'true', 'Auto Scan tab must restore');

    await scanTabs.nth(0).click();
    await page.waitForTimeout(100);
    const manualTypes = await page.locator('.map-controls .map-types label').evaluateAll(labels =>
      labels.map(label => ({
        text: label.textContent.trim(),
        checked: label.querySelector('input').checked
      })));
    const selectedLabels = manualTypes.filter(item => item.checked).map(item => item.text);
    assert.deepEqual(selectedLabels, ['Player City', 'Monster'], 'Manual selected types must restore per profile');

    const resultTabs = page.locator('.map-tabs button');
    const activeResultText = await resultTabs.filter({ has: page.locator('.map-tab-label') })
      .evaluateAll(buttons => buttons.find(button => button.classList.contains('active'))?.textContent || '');
    assert.match(activeResultText, /Monster/, 'Result tab must restore per profile');

    const serverSelect = page.locator('.map-searchbar select[aria-label="Server"]');
    await serverSelect.waitFor();
    assert.deepEqual(await serverSelect.locator('option').evaluateAll(options => options.map(o => o.value)),
      ['2212', '2213'], 'all saved server datasets must be selectable');
    assert.equal(await serverSelect.inputValue(), '2213', 'saved browse server must restore independently of live server');

    // Change all four persisted choices.
    const labels = page.locator('.map-controls .map-types label');
    await labels.filter({ hasText: 'Truck' }).click();
    await labels.filter({ hasText: 'Player City' }).click();
    await page.locator('.map-tabs button').filter({ hasText: 'Truck' }).click();
    await page.waitForTimeout(150);
    await serverSelect.selectOption('2212');
    await scanTabs.nth(1).click();
    await page.waitForTimeout(100);
    const persisted = await page.evaluate(() => ({
      types: JSON.parse(localStorage.getItem('lwbridge.mapManualScanTypes.local-1')),
      scanTab: localStorage.getItem('lwbridge.mapScanTab.local-1'),
      resultTab: localStorage.getItem('lwbridge.mapResultTab.local-1'),
      server: localStorage.getItem('lwbridge.mapBrowseServer.local-1')
    }));
    assert.deepEqual(persisted.types, ['monster', 'truck']);
    assert.equal(persisted.scanTab, 'auto');
    assert.equal(persisted.resultTab, 'truck');
    assert.equal(persisted.server, '2212');

    await page.reload();
    await page.locator('.panel.map-panel').waitFor();
    await page.waitForTimeout(400);
    assert.equal(await page.locator('.map-scan-tabs button').nth(1).getAttribute('aria-selected'), 'true',
      'Auto Scan tab must survive full reload');
    await page.locator('.map-scan-tabs button').nth(0).click();
    await page.waitForTimeout(100);
    const reloadedSelected = await page.locator('.map-controls .map-types label').evaluateAll(labels =>
      labels.filter(label => label.querySelector('input').checked).map(label => label.textContent.trim()));
    assert.deepEqual(reloadedSelected, ['Monster', 'Truck'], 'Manual types must survive full reload');
    assert.match(await page.locator('.map-tabs button.active').innerText(), /Truck/, 'result tab must survive reload');
    const reloadedServerSelect = page.locator('.map-searchbar select[aria-label="Server"]');
    assert.equal(await reloadedServerSelect.inputValue(), '2212', 'saved server choice must survive reload');

    // R7-130 Auto Stop: two targets are configured, but Stop during the first scan must disable
    // the scheduler synchronously so target two never starts.
    await page.locator('.map-scan-tabs button').nth(1).click();
    const card = page.locator('.map-auto-scan-card');
    const master = card.locator('.map-auto-scan-master input[type="checkbox"]');
    const serverInput = card.locator('.map-auto-scan-server-input input');
    const addButton = card.locator('.map-auto-scan-server-input button');
    for (const serverId of ['2212', '2213']) {
      await serverInput.fill(serverId);
      await addButton.click();
    }
    const returnToggle = card.locator('.map-auto-scan-options input[type="checkbox"]');
    if (await returnToggle.isChecked()) await returnToggle.uncheck();
    await master.check();

    await page.waitForFunction(() =>
      window.__r7131.calls.some(call => call.command === 'map_scan_start'), null, { timeout: 8000 });
    const stop = card.getByRole('button', { name: 'Stop' });
    await stop.waitFor();
    await page.waitForFunction(() => {
      const buttons = [...document.querySelectorAll('.map-auto-scan-card button')];
      const stop = buttons.find(button => button.textContent.trim() === 'Stop');
      return stop && !stop.disabled;
    }, null, { timeout: 3000 });
    await stop.click();
    await page.waitForFunction(() =>
      window.__r7131.calls.some(call => call.command === 'map_scan_stop'), null, { timeout: 3000 });
    await page.waitForTimeout(2500);
    const stopEvidence = await page.evaluate(() => ({
      calls: window.__r7131.calls.map(call => ({ command: call.command, payload: call.payload })),
      auto: Object.entries(localStorage)
        .filter(([key]) => key.startsWith('lwbridge.mapAutoScan.'))
        .map(([key, value]) => ({ key, value: JSON.parse(value) }))
    }));
    const starts = stopEvidence.calls.filter(call => call.command === 'map_scan_start');
    const jumps = stopEvidence.calls.filter(call => call.command === 'server_jump');
    assert.equal(starts.length, 1, 'Stop must prevent a second target scan from starting');
    assert.equal(jumps.some(call => Number(call.payload.serverId) === 2213), false,
      'Stop must prevent navigation to the second target');
    assert.equal(stopEvidence.calls.filter(call => call.command === 'map_scan_stop').length, 1,
      'Stop must issue exactly one backend scan stop');
    assert.equal(stopEvidence.auto.length > 0, true, 'Auto config must persist');
    assert.equal(stopEvidence.auto[0].value.enabled, false, 'Stop must persist Auto Scan disabled');

    // R7-130 Clear/search race: an old search rejects after Clear. Its error must be stale.
    await page.locator('.map-scan-tabs button').nth(0).click();
    await page.locator('.map-tabs button').first().click();
    await page.waitForTimeout(100);
    await page.evaluate(() => { window.__r7131.delaySearchFailure = true; });
    const searchbar = page.locator('.map-searchbar');
    await searchbar.locator('input').first().fill('stale-race');
    await searchbar.getByRole('button', { name: 'Search' }).click();
    const clearButton = page.locator('.map-actions button').filter({ hasText: 'Clear' });
    await clearButton.click();
    await page.waitForTimeout(700);

    const alertText = await page.locator('.map-scan-error[role="alert"]').allInnerTexts();
    assert.equal(alertText.some(text => /Saved map search failed|MAP_QUERY_FAILED|stale fixture/i.test(text)), false,
      'stale saved-search rejection after Clear must not surface as a user error');
    const clearEvidence = await page.evaluate(() => {
      const calls = window.__r7131.calls;
      const clearIndex = calls.findIndex(call => call.command === 'map_scan_clear');
      return {
        clearCount: calls.filter(call => call.command === 'map_scan_clear').length,
        commandsAfterClear: clearIndex < 0 ? [] : calls.slice(clearIndex + 1).map(call => call.command)
      };
    });
    assert.equal(clearEvidence.clearCount, 1, 'Clear must reach the backend exactly once');
    assert.equal(clearEvidence.commandsAfterClear.includes('map_search'), false,
      'Clear must leave a stable empty result set instead of automatically re-querying the cleared server');

    assert.deepEqual(pageErrors, []);
    console.log('R7-131 Map Data persistence, saved-server, Stop, and Clear-race browser checks passed.');
  } finally {
    await page.close();
    await browser.close();
    server.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
