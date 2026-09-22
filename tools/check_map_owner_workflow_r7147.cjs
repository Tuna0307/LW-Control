const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const webRoot = path.join(root, 'src', 'LWBridge.Desktop', 'WebUi');

async function main() {
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const rel = url.pathname.slice(1) || 'index.html';
    try {
      const file = path.resolve(webRoot, rel);
      if (!file.startsWith(webRoot + path.sep) && file !== path.join(webRoot, 'index.html'))
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
  const port = Number(process.env.LWBRIDGE_R7147_PORT || 18087);
  server.listen(port, '127.0.0.1');
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });

  const browser = await chromium.launch({
    channel: process.env.LWBRIDGE_BROWSER || 'msedge',
    headless: true
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(String(error.stack || error)));

  await page.addInitScript(() => {
    const profile = 'local-1';
    localStorage.setItem(`lwbridge.mapScanTab.${profile}`, 'auto');
    localStorage.setItem(`lwbridge.mapResultTab.${profile}`, 'city');
    localStorage.setItem(`lwbridge.mapBrowseServer.${profile}`, '0');
    localStorage.setItem(`lwbridge.mapAutoScan.${profile}`, JSON.stringify({
      enabled: false,
      intervalMinutes: 60,
      serverIds: [2185, 2190],
      selectedTypes: ['city'],
      returnToOriginalServer: true,
      nextRunAt: 0,
      runOnceRequestedAt: 0
    }));

    Object.defineProperty(window, 'LWBridgePreview', {
      configurable: true,
      get() { return undefined; },
      set(value) {
        const originalInvoke = value.invoke.bind(value);
        const counts = {
          city: 1, resource: 0, monster: 1, zombie_boss: 0,
          truck: 0, railway: 0, dispatch: 0, ghost: 0, treasure: 0
        };
        const runtime = {
          calls: [],
          currentServer: 2212,
          savedServerIds: [2185, 2190],
          scanState: {
            serverId: 2212, serverIdSource: 'live', scanRunId: '',
            isReading: false, phase: 'idle', selectedTypes: ['city'],
            totalBlocks: 0, readBlocks: 0, unreadBlocks: 0, failedBlocks: 0,
            inflightBlocks: 0, scanMode: 'fast', concurrency: 20, retryCount: 2,
            scanRate: 0, progressPercent: 0, nativeCaptureReady: true,
            nativePendingRecords: 0, nativeDroppedRecords: 0,
            homeServerId: 2212, seasonServerIds: [], truckMatchServerIds: []
          }
        };
        window.__r7147 = runtime;
        const clone = value => value == null ? value : structuredClone(value);

        value.invoke = async (command, payload = {}) => {
          runtime.calls.push({ command, payload: clone(payload), at: Date.now() });
          if (command === 'get_status')
            return { xluaOnline: true, pending: 0, config: { tasks: {} } };
          if (command === 'proxy_status')
            return { gameRunning: true, repairRequired: false };
          if (command === 'map_scan_status')
            return clone(runtime.scanState);
          if (command === 'map_summary')
            return {
              serverId: runtime.currentServer,
              savedServerIds: [...runtime.savedServerIds],
              counts: clone(counts),
              scanState: clone(runtime.scanState)
            };
          if (command === 'map_data_options')
            return {
              serverId: Number(payload.serverId),
              alliances: [], names: { resource: [], monster: [], zombie_boss: [] },
              dispatchLevels: [], monsterLevels: [200],
              counts: clone(counts), rewardItems: { truck: [], railway: [] },
              treasureTypes: [], noAllianceCount: 0, scanProgress: null
            };
          if (command === 'map_search') {
            const kind = String(payload.kind || '');
            if (kind === 'city') {
              return {
                total: 1,
                rows: [{
                  serverId: 2185, recordKey: 'city-owner-workflow',
                  pointIndex: 123, uuid: 'city-uuid', ownerUid: '1001',
                  ownerName: 'Cross Server City', x: 297, y: 999,
                  level: 30, updatedAt: Date.now()
                }]
              };
            }
            if (kind === 'monster') {
              return {
                total: 1,
                rows: [{
                  serverId: 2190, recordKey: 'doom-walker-owner-workflow',
                  pointIndex: 456, uuid: 'doom-uuid',
                  marchUuid: '9223372036854775001',
                  monsterNameKey: 'doom-walker', name: 'Doom Walker',
                  x: 400, y: 500, level: 200,
                  configType: 8, special: 11, configSpecial: 11, updatedAt: Date.now()
                }]
              };
            }
            return { total: 0, rows: [] };
          }
          if (command === 'server_jump') {
            const target = Number(payload.serverId);
            const previousServerId = runtime.currentServer;
            runtime.currentServer = target;
            runtime.scanState.serverId = target;
            return {
              changed: target !== previousServerId,
              previousServerId,
              serverId: target,
              currentServerId: target
            };
          }
          if (command === 'map_coordinate_jump')
            return { serverId: Number(payload.serverId), x: Number(payload.x), y: Number(payload.y) };
          if (command === 'map_march_follow')
            return { serverId: Number(payload.serverId), marchUuid: String(payload.marchUuid) };
          if (command === 'map_scan_start') {
            runtime.scanState = {
              ...runtime.scanState,
              serverId: runtime.currentServer,
              isReading: false,
              phase: 'idle',
              selectedTypes: [...(payload.selectedTypes || ['city'])],
              totalBlocks: 2500, readBlocks: 2500, unreadBlocks: 0,
              failedBlocks: 0, inflightBlocks: 0, progressPercent: 100
            };
            return clone(runtime.scanState);
          }
          if (command === 'map_scan_stop') {
            runtime.scanState = {
              ...runtime.scanState, isReading: false, phase: 'idle', scanRunId: ''
            };
            return clone(runtime.scanState);
          }
          if (command === 'map_scan_clear') {
            runtime.savedServerIds = [];
            runtime.scanState = {
              ...runtime.scanState, isReading: false, phase: 'idle',
              scanRunId: '', totalBlocks: 0, readBlocks: 0, unreadBlocks: 0,
              failedBlocks: 0, inflightBlocks: 0, progressPercent: 0
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
    await page.goto(`http://127.0.0.1:${port}/index.html?view=map-data&language=en`);
    const panel = page.locator('.panel.map-panel');
    await panel.waitFor();
    await page.waitForTimeout(700);

    const scanTabs = panel.locator('.map-scan-tabs button');
    assert.equal(await scanTabs.nth(1).getAttribute('aria-selected'), 'true',
      'Auto Scan tab should restore');

    // Auto browsing exposes one real All option plus this session's scanned servers.
    const serverSelect = panel.locator('.map-searchbar select[aria-label="Server"]');
    await serverSelect.waitFor();
    assert.deepEqual(await serverSelect.locator('option').evaluateAll(options =>
      options.map(option => ({ value: option.value, text: option.textContent.trim() }))),
      [
        { value: '0', text: 'All' },
        { value: '2185', text: 'Server 2185' },
        { value: '2190', text: 'Server 2190' }
      ]);
    assert.equal(await serverSelect.inputValue(), '0', 'Auto browse should restore All');

    // Recurring scheduler disabled must not disable one-shot multi-server Run Now.
    const autoCard = panel.locator('.map-auto-scan-card');
    const master = autoCard.locator('.map-auto-scan-master input[type="checkbox"]');
    assert.equal(await master.isChecked(), false, 'recurring Auto Scan starts disabled in fixture');
    const runNow = autoCard.getByRole('button', { name: 'Run now' });
    assert.equal(await runNow.isEnabled(), true,
      'Run now must remain enabled when recurring automatic scanning is disabled');
    await runNow.click();

    await page.waitForFunction(() => {
      const calls = window.__r7147.calls;
      return calls.filter(call => call.command === 'map_scan_start').length >= 2 &&
        calls.filter(call => call.command === 'server_jump')
          .some(call => Number(call.payload.serverId) === 2212);
    }, null, { timeout: 15000 });

    const oneShot = await page.evaluate(() => ({
      calls: window.__r7147.calls.map(call => ({ command: call.command, payload: call.payload })),
      config: JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'))
    }));
    assert.deepEqual(
      oneShot.calls.filter(call => call.command === 'map_scan_start')
        .map(call => call.payload.selectedTypes),
      [['city'], ['city']],
      'one-shot cycle should scan each configured target exactly once');
    const jumpedServers = oneShot.calls.filter(call => call.command === 'server_jump')
      .map(call => Number(call.payload.serverId));
    assert.equal(jumpedServers.includes(2185), true);
    assert.equal(jumpedServers.includes(2190), true);
    assert.equal(jumpedServers[jumpedServers.length - 1], 2212,
      'one-shot cycle should return to its original server');
    assert.equal(oneShot.config.enabled, false,
      'one-shot Run now must not silently enable recurring scheduling');
    assert.equal(Number(oneShot.config.runOnceRequestedAt || 0), 0,
      'one-shot request marker must clear after terminal cycle');

    // Auto has its own Clear and it clears the whole session scan dataset.
    const clearAuto = autoCard.getByRole('button', { name: /Clear Map Data/i });
    await clearAuto.click();
    await page.waitForFunction(() =>
      window.__r7147.calls.some(call =>
        call.command === 'map_scan_clear' && Number(call.payload.serverId) === 0));
    const clearCalls = await page.evaluate(() =>
      window.__r7147.calls.filter(call => call.command === 'map_scan_clear')
        .map(call => call.payload));
    assert.equal(clearCalls.length, 1);
    assert.equal(Number(clearCalls[0].serverId), 0);

    // Re-seed session datasets for navigation/browser checks.
    await page.evaluate(() => {
      window.__r7147.savedServerIds = [2185, 2190];
      window.__r7147.currentServer = 2212;
      window.__r7147.scanState.serverId = 2212;
    });
    await page.waitForTimeout(5200);

    // Manual scans exactly one live server, so Manual has no server filter.
    await scanTabs.nth(0).click();
    await page.waitForTimeout(300);
    assert.equal(await panel.locator('.map-searchbar select[aria-label="Server"]').count(), 0,
      'Manual Scan must not show a server filter');

    // Return to Auto / All for cross-server row navigation.
    await scanTabs.nth(1).click();
    await page.waitForTimeout(300);
    const allSelect = panel.locator('.map-searchbar select[aria-label="Server"]');
    await allSelect.selectOption('0');
    await panel.locator('.map-tabs button').filter({ hasText: 'City' }).click();
    await page.waitForTimeout(350);

    const cityJump = panel.locator('.map-table').getByRole('button', { name: /297,999.*Jump/i });
    await cityJump.click();
    await page.waitForFunction(() => {
      const calls = window.__r7147.calls;
      const jumpIndex = calls.findIndex(call =>
        call.command === 'server_jump' && Number(call.payload.serverId) === 2185);
      const coordinateIndex = calls.findIndex(call =>
        call.command === 'map_coordinate_jump' &&
        Number(call.payload.serverId) === 2185 &&
        Number(call.payload.x) === 297 && Number(call.payload.y) === 999);
      return jumpIndex >= 0 && coordinateIndex > jumpIndex;
    }, null, { timeout: 5000 });

    // Doom Walker is a moving SuperRunningBoss: Follow by exact march UUID.
    await page.evaluate(() => {
      window.__r7147.currentServer = 2212;
      window.__r7147.scanState.serverId = 2212;
    });
    await panel.locator('.map-tabs button').filter({ hasText: 'Monster' }).click();
    await page.waitForTimeout(350);
    const follow = panel.locator('.map-table').getByRole('button', { name: /Follow/i });
    assert.equal(await follow.count(), 1, 'Doom Walker row must render Follow instead of Jump');
    await follow.click();
    await page.waitForFunction(() => {
      const calls = window.__r7147.calls;
      const jumpIndex = calls.findIndex(call =>
        call.command === 'server_jump' && Number(call.payload.serverId) === 2190);
      const followIndex = calls.findIndex(call =>
        call.command === 'map_march_follow' &&
        Number(call.payload.serverId) === 2190 &&
        String(call.payload.marchUuid) === '9223372036854775001');
      return jumpIndex >= 0 && followIndex > jumpIndex;
    }, null, { timeout: 5000 });

    assert.deepEqual(pageErrors, []);
    console.log('R7-147 Map Data owner-workflow browser checks passed.');
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
