/* R7-132 browser regression: Auto Scan retains one scheduler owner across
 * navigation/status refresh/reconnect, and never scans before confirmed travel.
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
  }).listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  return { server, origin: `http://127.0.0.1:${server.address().port}` };
}

async function newScenario(browser, origin, { delaySecondJump = false } = {}) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(String(error.stack || error)));

  await page.addInitScript(({ delaySecondJump, zeroCounts }) => {
    localStorage.setItem('lwbridge.mapAutoScan.local-1', JSON.stringify({
      enabled: true,
      intervalMinutes: 20,
      serverIds: [2212, 2213],
      selectedTypes: ['city'],
      returnToOriginalServer: false,
      nextRunAt: 0
    }));

    Object.defineProperty(window, 'LWBridgePreview', {
      configurable: true,
      get() { return undefined; },
      set(value) {
        const originalInvoke = value.invoke.bind(value);
        const originalListen = value.listen.bind(value);
        const listeners = new Map();
        let releaseSecondJump = null;

        const runtime = {
          online: true,
          calls: [],
          currentServer: 2212,
          delaySecondJump,
          scanState: {
            serverId: 2212, serverIdSource: 'live', scanRunId: '', isReading: false,
            phase: 'idle', selectedTypes: ['city'], totalBlocks: 0, readBlocks: 0,
            unreadBlocks: 0, failedBlocks: 0, inflightBlocks: 0, scanMode: 'fast',
            concurrency: 20, retryCount: 2, scanRate: 0, progressPercent: 0,
            nativeCaptureReady: true, nativePendingRecords: 0, nativeDroppedRecords: 0,
            homeServerId: 2212, seasonServerIds: [], truckMatchServerIds: []
          },
          emit(event, payload) {
            for (const callback of listeners.get(event) || [])
              callback(structuredClone(payload));
          },
          finishScan() {
            this.scanState = {
              ...this.scanState,
              isReading: false,
              phase: 'completed',
              readBlocks: 2500,
              unreadBlocks: 0,
              totalBlocks: 2500,
              progressPercent: 100
            };
            this.emit('bridge://map-scan-status', this.scanState);
          },
          releaseSecondJump() {
            releaseSecondJump?.();
          }
        };
        window.__r7132 = runtime;

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
              savedServerIds: [2212, 2213],
              counts: structuredClone(zeroCounts),
              scanState: structuredClone(runtime.scanState)
            };
          if (command === 'server_jump') {
            const target = Number(payload.serverId);
            const previousServerId = runtime.currentServer;
            if (runtime.delaySecondJump && target === 2213) {
              await new Promise(resolve => { releaseSecondJump = resolve; });
              releaseSecondJump = null;
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
              progressPercent: 0
            };
            runtime.emit('bridge://map-scan-status', runtime.scanState);
            return structuredClone(runtime.scanState);
          }
          if (command === 'map_data_options')
            return {
              serverId: Number(payload.serverId || runtime.currentServer),
              alliances: [],
              names: { resource: [], monster: [], zombie_boss: [] },
              dispatchLevels: [],
              monsterLevels: [],
              counts: structuredClone(zeroCounts),
              rewardItems: { truck: [], railway: [] },
              treasureTypes: [],
              noAllianceCount: 0,
              scanProgress: null
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
  }, { delaySecondJump, zeroCounts });

  await page.goto(`${origin}/index.html?view=map-data&language=en`);
  await page.locator('.panel.map-panel').waitFor();
  return { context, page, pageErrors };
}

async function calls(page, command) {
  return page.evaluate(command =>
    window.__r7132.calls.filter(call => call.command === command),
  command);
}

async function refreshStatus(page) {
  await page.getByRole('button', { name: 'Refresh Status' }).click();
}

async function navigationRefreshScenario(browser, origin) {
  const { context, page, pageErrors } = await newScenario(browser, origin, { delaySecondJump: true });
  try {
    await page.waitForFunction(() =>
      window.__r7132.calls.filter(call => call.command === 'map_scan_start').length === 1,
    null, { timeout: 8000 });

    await page.locator('.side-nav button').nth(0).click();
    await page.waitForTimeout(100);
    for (let i = 0; i < 3; i += 1) await refreshStatus(page);
    await page.locator('.side-nav button').nth(2).click();
    await page.locator('.panel.map-panel').waitFor();

    await page.evaluate(() => window.__r7132.finishScan());
    await page.waitForFunction(() =>
      window.__r7132.calls.some(call =>
        call.command === 'server_jump' && Number(call.payload.serverId) === 2213),
    null, { timeout: 5000 });

    await page.waitForTimeout(300);
    assert.equal((await calls(page, 'map_scan_start')).length, 1,
      'Auto Scan must not start target 2213 before server_jump confirms arrival');

    await page.evaluate(() => window.__r7132.releaseSecondJump());
    await page.waitForFunction(() =>
      window.__r7132.calls.filter(call => call.command === 'map_scan_start').length === 2,
    null, { timeout: 5000 });
    await page.evaluate(() => window.__r7132.finishScan());

    await page.waitForFunction(() => {
      const auto = JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'));
      return auto.nextRunAt > Date.now();
    }, null, { timeout: 5000 });
    await page.waitForTimeout(300);

    const starts = await calls(page, 'map_scan_start');
    const jumps = await calls(page, 'server_jump');
    assert.equal(starts.length, 2,
      'navigation and repeated status refresh must not duplicate Auto starts');
    assert.deepEqual(jumps.map(call => Number(call.payload.serverId)), [2212, 2213],
      'configured target order must remain single-pass across navigation/refresh');
    assert.deepEqual(pageErrors, []);
  } finally {
    await context.close();
  }
}

async function reconnectScenario(browser, origin) {
  const { context, page, pageErrors } = await newScenario(browser, origin);
  try {
    await page.waitForFunction(() =>
      window.__r7132.calls.filter(call => call.command === 'map_scan_start').length === 1,
    null, { timeout: 8000 });

    await page.evaluate(() => { window.__r7132.online = false; });
    await refreshStatus(page);
    await page.waitForTimeout(100);
    await page.evaluate(() => window.__r7132.finishScan());

    await page.waitForFunction(() => {
      const auto = JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'));
      return auto.nextRunAt > Date.now();
    }, null, { timeout: 5000 });

    let jumps = await calls(page, 'server_jump');
    assert.equal(jumps.some(call => Number(call.payload.serverId) === 2213), false,
      'disconnect at target boundary must stop additional server travel');

    await page.evaluate(() => { window.__r7132.online = true; });
    await refreshStatus(page);
    await page.waitForTimeout(5500);

    const starts = await calls(page, 'map_scan_start');
    jumps = await calls(page, 'server_jump');
    assert.equal(starts.length, 1,
      'reconnect must not duplicate the already-admitted Auto cycle');
    assert.deepEqual(jumps.map(call => Number(call.payload.serverId)), [2212],
      'reconnect must not retroactively continue the interrupted target list');
    const auto = await page.evaluate(() =>
      JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1')));
    assert.equal(auto.nextRunAt > Date.now(), true,
      'interrupted cycle must advance to a future schedule before reconnect');
    assert.deepEqual(pageErrors, []);
  } finally {
    await context.close();
  }
}

async function disconnectDuringJumpScenario(browser, origin) {
  const { context, page, pageErrors } = await newScenario(browser, origin, { delaySecondJump: true });
  try {
    await page.waitForFunction(() =>
      window.__r7132.calls.filter(call => call.command === 'map_scan_start').length === 1,
    null, { timeout: 8000 });
    await page.evaluate(() => window.__r7132.finishScan());

    await page.waitForFunction(() =>
      window.__r7132.calls.some(call =>
        call.command === 'server_jump' && Number(call.payload.serverId) === 2213),
    null, { timeout: 5000 });

    await page.evaluate(() => { window.__r7132.online = false; });
    await refreshStatus(page);
    await page.waitForTimeout(100);
    await page.evaluate(() => window.__r7132.releaseSecondJump());

    await page.waitForFunction(() => {
      const auto = JSON.parse(localStorage.getItem('lwbridge.mapAutoScan.local-1'));
      return auto.nextRunAt > Date.now();
    }, null, { timeout: 5000 });
    await page.waitForTimeout(300);

    const starts = await calls(page, 'map_scan_start');
    assert.equal(starts.length, 1,
      'disconnect while server_jump is pending must prevent that target scan after arrival confirms');
    const jumps = await calls(page, 'server_jump');
    assert.deepEqual(jumps.map(call => Number(call.payload.serverId)), [2212, 2213],
      'travel request may finish, but no scan may start after connectivity ownership is lost');
    assert.deepEqual(pageErrors, []);
  } finally {
    await context.close();
  }
}

async function main() {
  const { server, origin } = await serve();
  const browser = await chromium.launch({
    channel: process.env.LWBRIDGE_BROWSER || 'msedge',
    headless: true
  });
  try {
    await navigationRefreshScenario(browser, origin);
    await reconnectScenario(browser, origin);
    await disconnectDuringJumpScenario(browser, origin);

    const index = fs.readFileSync(path.join(candidate, 'assets', 'index-sfL2sT3K.js'), 'utf8');
    for (const token of [
      'autoOnlineRef=(0,j.useRef)(P)',
      'autoOnlineRef.current=P',
      'if(!Zn(i,Date.now(),autoOnlineRef.current,qe.current,Ye.current))return',
      'if(e||!Je.current.enabled||!autoOnlineRef.current)break',
      'try{let n=await Se(t);if(e||!Je.current.enabled||!autoOnlineRef.current)break;F(n.changed?',
      'return()=>{e=!0,window.clearInterval(a)}},[u.selectedProfileId]),(0,M.jsxs)(M.Fragment'
    ]) {
      assert.equal(index.includes(token), true,
        `Auto scheduler bundle must retain R7-132 ownership guard: ${token}`);
    }
    assert.equal(index.includes('if(!Zn(i,Date.now(),P,qe.current,Ye.current))return'), false,
      'Auto scheduler must not key cycle admission directly to render-time P');
    console.log('R7-132 Auto navigation/refresh/reconnect browser checks passed.');
  } finally {
    await browser.close();
    server.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
