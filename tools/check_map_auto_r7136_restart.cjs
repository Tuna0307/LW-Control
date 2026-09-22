/* R7-136 browser regression: an Auto Scan interrupted by a full app/process
 * restart must never resume/re-admit its target list. A durable per-profile
 * cycle marker restores the original server first, advances nextRunAt, then
 * clears only after successful restoration.
 */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const candidate = path.join(root, 'src', 'LWBridge.Desktop', 'WebUi');
const zeroCounts = {city:0,resource:0,monster:0,zombie_boss:0,truck:0,railway:0,dispatch:0,ghost:0,treasure:0};

async function serve() {
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
  const listenPort = Number(process.env.LWBRIDGE_R7136_PORT || 18083);
  server.listen(listenPort, '127.0.0.1');
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });
  return { server, origin: `http://127.0.0.1:${listenPort}` };
}

async function newScenario(browser, origin, {
  storageState = undefined,
  seedAutoConfig = true,
  initialServer = 2212,
  returnToOriginalServer = true,
  failRestoreOnce = false
} = {}) {
  const context = await browser.newContext({
    viewport: { width: 1280, height: 900 },
    ...(storageState ? { storageState } : {})
  });
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(String(error.stack || error)));

  await page.addInitScript(({ seedAutoConfig, initialServer, returnToOriginalServer, failRestoreOnce, zeroCounts }) => {
    if (seedAutoConfig) {
      localStorage.setItem('lwbridge.mapAutoScan.local-1', JSON.stringify({
        enabled: true,
        intervalMinutes: 20,
        serverIds: [2213, 2214],
        selectedTypes: ['city'],
        returnToOriginalServer,
        nextRunAt: 0
      }));
      localStorage.removeItem('lwbridge.mapAutoScanCycle.local-1');
    }

    Object.defineProperty(window, 'LWBridgePreview', {
      configurable: true,
      get() { return undefined; },
      set(value) {
        const originalInvoke = value.invoke.bind(value);
        const originalListen = value.listen.bind(value);
        const listeners = new Map();
        const runtime = {
          online: true,
          calls: [],
          currentServer: initialServer,
          failRestoreOnce,
          scanState: {
            serverId: initialServer, serverIdSource: 'live', scanRunId: '', isReading: false,
            phase: 'idle', selectedTypes: ['city'], totalBlocks: 0, readBlocks: 0,
            unreadBlocks: 0, failedBlocks: 0, inflightBlocks: 0, scanMode: 'fast',
            concurrency: 20, retryCount: 2, scanRate: 0, progressPercent: 0,
            nativeCaptureReady: true, nativePendingRecords: 0, nativeDroppedRecords: 0,
            resumeAvailable: false, homeServerId: 2212, seasonServerIds: [], truckMatchServerIds: []
          },
          emit(event, payload) {
            for (const callback of listeners.get(event) || [])
              callback(structuredClone(payload));
          }
        };
        window.__r7136 = runtime;

        value.listen = (event, callback) => {
          if (!listeners.has(event)) listeners.set(event, new Set());
          listeners.get(event).add(callback);
          const stop = originalListen(event, callback);
          return () => {
            listeners.get(event)?.delete(callback);
            stop?.();
          };
        };

        value.invoke = async (command, payload = {}) => {
          runtime.calls.push({ command, payload: structuredClone(payload), at: Date.now() });
          if (command === 'get_status')
            return { xluaOnline: runtime.online, pending: 0, config: { tasks: {} } };
          if (command === 'proxy_status')
            return { gameRunning: runtime.online, repairRequired: false };
          if (command === 'map_scan_status')
            return structuredClone(runtime.scanState);
          if (command === 'map_summary')
            return {
              serverId: runtime.currentServer,
              savedServerIds: [2212, 2213, 2214],
              counts: structuredClone(zeroCounts),
              scanState: structuredClone(runtime.scanState)
            };
          if (command === 'server_jump') {
            const target = Number(payload.serverId);
            const previousServerId = runtime.currentServer;
            if (runtime.failRestoreOnce && target === 2212) {
              runtime.failRestoreOnce = false;
              throw new Error('synthetic restart restoration failure');
            }
            runtime.currentServer = target;
            runtime.scanState.serverId = target;
            runtime.emit('bridge://map-scan-status', runtime.scanState);
            return {
              changed: target !== previousServerId,
              previousServerId,
              serverId: target,
              currentServerId: target
            };
          }
          if (command === 'map_scan_start') {
            runtime.scanState = {
              ...runtime.scanState,
              serverId: runtime.currentServer,
              serverIdSource: 'live',
              scanRunId: `run-${runtime.calls.filter(call => call.command === 'map_scan_start').length}`,
              isReading: true,
              phase: 'scanning',
              totalBlocks: 2500,
              readBlocks: 0,
              unreadBlocks: 2500,
              progressPercent: 0,
              resumeAvailable: false
            };
            runtime.emit('bridge://map-scan-status', runtime.scanState);
            return structuredClone(runtime.scanState);
          }
          if (command === 'map_data_options')
            return {
              serverId: Number(payload.serverId || runtime.currentServer),
              alliances: [], names: { resource: [], monster: [], zombie_boss: [] },
              dispatchLevels: [], monsterLevels: [], counts: structuredClone(zeroCounts),
              rewardItems: { truck: [], railway: [] }, treasureTypes: [],
              noAllianceCount: 0, scanProgress: null
            };
          if (command === 'map_search') return { rows: [], total: 0 };
          return originalInvoke(command, payload);
        };

        Object.defineProperty(window, 'LWBridgePreview', {
          configurable: true,
          writable: true,
          value
        });
      }
    });
  }, { seedAutoConfig, initialServer, returnToOriginalServer, failRestoreOnce, zeroCounts });

  await page.goto(`${origin}/index.html?view=map-data&language=en`);
  await page.locator('.panel.map-panel').waitFor();
  return { context, page, pageErrors };
}

async function startInterruptedCycle(browser, origin, returnToOriginalServer = true) {
  const first = await newScenario(browser, origin, {
    seedAutoConfig: true,
    initialServer: 2212,
    returnToOriginalServer
  });
  await first.page.waitForFunction(() =>
    window.__r7136.calls.filter(call => call.command === 'map_scan_start').length === 1,
  null, { timeout: 8000 });

  const firstJumpTargets = await first.page.evaluate(() =>
    window.__r7136.calls
      .filter(call => call.command === 'server_jump')
      .map(call => Number(call.payload.serverId)));
  assert.deepEqual(firstJumpTargets, [2213],
    'the interrupted cycle must travel to its first configured target before restart');

  const marker = await first.page.evaluate(() =>
    JSON.parse(localStorage.getItem('lwbridge.mapAutoScanCycle.local-1')));
  assert.equal(marker.schemaVersion, 1);
  assert.equal(marker.originalServerId, 2212);
  assert.equal(marker.returnToOriginalServer, returnToOriginalServer);
  assert.equal(marker.startedAt > 0, true);

  const persisted = JSON.parse(await first.page.evaluate(() =>
    localStorage.getItem('lwbridge.mapAutoScan.local-1')));
  assert.equal(persisted.nextRunAt, 0,
    'before interruption finalization, the original due deadline remains unchanged');

  const storageState = await first.context.storageState();
  await first.context.close();
  return storageState;
}

async function restartRestoresBeforeFutureAdmission(browser, origin) {
  const storageState = await startInterruptedCycle(browser, origin, true);
  const second = await newScenario(browser, origin, {
    storageState,
    seedAutoConfig: false,
    initialServer: 2213,
    failRestoreOnce: true
  });
  try {
    await second.page.waitForFunction(() =>
      window.__r7136.calls.filter(call =>
        call.command === 'server_jump' && Number(call.payload.serverId) === 2212).length >= 1,
    null, { timeout: 8000 });

    await second.page.waitForTimeout(250);
    let marker = await second.page.evaluate(() =>
      localStorage.getItem('lwbridge.mapAutoScanCycle.local-1'));
    assert.notEqual(marker, null,
      'failed restart restoration must keep the in-flight marker for retry');
    assert.equal((await second.page.evaluate(() =>
      window.__r7136.calls.filter(call => call.command === 'map_scan_start').length)), 0,
      'restart recovery failure must not re-admit the interrupted Auto scan');

    await second.page.waitForFunction(() => {
      const marker = localStorage.getItem('lwbridge.mapAutoScanCycle.local-1');
      const config = JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'));
      return marker === null && config.nextRunAt > Date.now() &&
        window.__r7136.currentServer === 2212;
    }, null, { timeout: 8000 });

    await second.page.waitForTimeout(300);
    const jumps = await second.page.evaluate(() =>
      window.__r7136.calls
        .filter(call => call.command === 'server_jump')
        .map(call => Number(call.payload.serverId)));
    assert.deepEqual(jumps, [2212, 2212],
      'restart recovery must retry restoration and stop after the authoritative original server is restored');
    assert.equal((await second.page.evaluate(() =>
      window.__r7136.calls.filter(call => call.command === 'map_scan_start').length)), 0,
      'successful restart recovery must schedule the next cycle instead of resuming this one');
    assert.deepEqual(second.pageErrors, []);
  } finally {
    await second.context.close();
  }
}

async function restartWithoutReturnStillRejectsResume(browser, origin) {
  const storageState = await startInterruptedCycle(browser, origin, false);
  const second = await newScenario(browser, origin, {
    storageState,
    seedAutoConfig: false,
    initialServer: 2213
  });
  try {
    await second.page.waitForFunction(() => {
      const marker = localStorage.getItem('lwbridge.mapAutoScanCycle.local-1');
      const config = JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'));
      return marker === null && config.nextRunAt > Date.now();
    }, null, { timeout: 8000 });
    await second.page.waitForTimeout(300);

    assert.deepEqual(await second.page.evaluate(() =>
      window.__r7136.calls.filter(call => call.command === 'server_jump')), [],
      'return-disabled interrupted cycle must preserve its current server on restart');
    assert.equal(await second.page.evaluate(() =>
      window.__r7136.calls.filter(call => call.command === 'map_scan_start').length), 0,
      'return-disabled restart still must not resume the interrupted target list');
    assert.equal(await second.page.evaluate(() => window.__r7136.currentServer), 2213);
    assert.deepEqual(second.pageErrors, []);
  } finally {
    await second.context.close();
  }
}

async function main() {
  const { server, origin } = await serve();
  const browser = await chromium.launch({
    channel: process.env.LWBRIDGE_BROWSER || 'msedge',
    headless: true
  });
  try {
    await restartRestoresBeforeFutureAdmission(browser, origin);
    await restartWithoutReturnStillRejectsResume(browser, origin);

    const index = fs.readFileSync(path.join(candidate, 'assets', 'index-sfL2sT3K.js'), 'utf8');
    for (const token of [
      'lwbridge.mapAutoScanCycle.${e}',
      'function readAutoCycleMarker(e)',
      'function writeAutoCycleMarker(e,t)',
       'async function autoRestartRecovery()',
      'if(await autoRestartRecovery())return',
      'writeAutoCycleMarker(u.selectedProfileId,{schemaVersion:1,startedAt:Date.now(),originalServerId:a,returnToOriginalServer:i.returnToOriginalServer})',
      'automatic map scan restart recovery error',
      '&&writeAutoCycleMarker(n,null)'
    ]) {
      assert.equal(index.includes(token), true,
        `Auto restart ownership guard must retain: ${token}`);
    }
    console.log('R7-136 Auto app/process restart browser checks passed.');
  } finally {
    await browser.close();
    server.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
