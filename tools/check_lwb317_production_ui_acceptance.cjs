const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

function arg(name) {
  const index = process.argv.indexOf(name);
  if (index < 0 || index + 1 >= process.argv.length) throw new Error(`missing ${name}`);
  return process.argv[index + 1];
}

function ensure(condition, message) {
  if (!condition) throw new Error(message);
}

function numericText(value) {
  const parsed = Number(String(value || '').replace(/[^0-9]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

async function nativeInvoke(page, command, payload = {}, timeoutMs = 180000) {
  return page.evaluate(({ command, payload, timeoutMs }) => new Promise((resolve, reject) => {
    const webview = window.chrome?.webview;
    const sessionId = window.__LWBridgeBootstrap?.sessionId;
    if (!webview || typeof sessionId !== 'string' || !sessionId) {
      reject(new Error('native WebView bootstrap is unavailable'));
      return;
    }
    const id = window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
    const timer = window.setTimeout(() => {
      webview.removeEventListener('message', onMessage);
      try { webview.postMessage({ kind: 'cancel', sessionId, id }); } catch {}
      reject(new Error(`native command timed out: ${command}`));
    }, timeoutMs);
    function onMessage(event) {
      let message = event?.data;
      if (typeof message === 'string') {
        try { message = JSON.parse(message); } catch { return; }
      }
      if (!message || message.sessionId !== sessionId || message.kind !== 'response' || message.id !== id) return;
      window.clearTimeout(timer);
      webview.removeEventListener('message', onMessage);
      if (message.ok) resolve(message.result);
      else {
        const error = new Error(message.error?.message || 'native command failed');
        error.code = message.error?.code || 'NATIVE_COMMAND_FAILED';
        reject(error);
      }
    }
    webview.addEventListener('message', onMessage);
    webview.postMessage({ kind: 'invoke', sessionId, id, command, payload });
  }), { command, payload, timeoutMs });
}

async function waitForNativeStatus(page, profileId, predicate, timeoutMs, label) {
  const deadline = Date.now() + timeoutMs;
  let latest = null;
  while (Date.now() < deadline) {
    latest = await nativeInvoke(page, 'profile_instance_status', { profileId }, 15000);
    if (predicate(latest)) return latest;
    await page.waitForTimeout(500);
  }
  throw new Error(`${label} timed out; latest=${JSON.stringify(latest)}`);
}

async function waitForTable(page) {
  await page.waitForFunction(() => {
    const table = document.querySelector('.map-table--resource');
    return !!table && table.getAttribute('aria-busy') !== 'true';
  }, undefined, { timeout: 30000 });
}

async function tableRows(page) {
  return page.locator('.map-table--resource tbody tr').evaluateAll((rows) => rows
    .filter((row) => !row.querySelector('td.map-empty'))
    .map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent.trim())));
}

(async () => {
  const port = Number(arg('--port'));
  const outputPath = path.resolve(arg('--output'));
  const screenshotDir = path.resolve(arg('--screenshots'));
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.mkdirSync(screenshotDir, { recursive: true });

  const diagnostics = {
    consoleErrors: [],
    consoleWarnings: [],
    pageErrors: [],
    requestFailures: [],
    badResponses: [],
    securityPolicyViolations: [],
  };
  let browser;
  let page;
  let profileId = '';
  let instanceId = '';
  let gameStarted = false;
  let cleanup = { attempted: false, stopped: false, error: null };
  let proof = null;
  let fatalError = null;

  try {
    browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
    const context = browser.contexts()[0];
    page = context.pages().find((candidate) => candidate.url().startsWith('https://lwbridge.local/')) || context.pages()[0];
    ensure(page, 'normal LWBridge WebView page was not found');

    page.on('console', (message) => {
      if (message.type() === 'error') diagnostics.consoleErrors.push(message.text());
      if (message.type() === 'warning') diagnostics.consoleWarnings.push(message.text());
    });
    page.on('pageerror', (error) => diagnostics.pageErrors.push(String(error)));
    page.on('requestfailed', (request) => diagnostics.requestFailures.push({
      url: request.url(),
      method: request.method(),
      failure: request.failure()?.errorText || null,
    }));
    page.on('response', (response) => {
      if (response.status() >= 400) diagnostics.badResponses.push({ url: response.url(), status: response.status() });
    });

    await page.waitForSelector('.app-shell[data-ui-project="LWBridge.UI-0.3.17"]', { timeout: 30000 });
    await page.evaluate(() => {
      window.addEventListener('securitypolicyviolation', (event) => {
        window.__lwb317SecurityPolicyViolations ||= [];
        window.__lwb317SecurityPolicyViolations.push({
          blockedURI: event.blockedURI,
          effectiveDirective: event.effectiveDirective,
          violatedDirective: event.violatedDirective,
        });
      });
    });

    const bootstrap = await page.evaluate(() => ({
      mode: window.__LWBridgeBootstrap?.mode || null,
      hasSessionId: typeof window.__LWBridgeBootstrap?.sessionId === 'string' && window.__LWBridgeBootstrap.sessionId.length > 0,
      selectedProfileId: window.__LWBridgeBootstrap?.profiles?.selectedProfileId || '',
      hasNativeWebView: !!window.chrome?.webview,
    }));
    ensure(bootstrap.mode === 'live' && bootstrap.hasSessionId && bootstrap.hasNativeWebView,
      `normal launch did not expose the live native bootstrap: ${JSON.stringify(bootstrap)}`);
    profileId = bootstrap.selectedProfileId;
    ensure(profileId, 'normal launch bootstrap did not provide a selected profile');

    const buildIdentity = await page.evaluate(async () => {
      const response = await fetch('/lwbridge-ui-build.json', { cache: 'no-store' });
      if (!response.ok) throw new Error(`build identity HTTP ${response.status}`);
      return response.json();
    });
    ensure(buildIdentity.project === 'LWBridge.UI-0.3.17' &&
      buildIdentity.canonicalSource === 'src/LWBridge.UI-0.3.17',
    `unexpected packaged UI identity: ${JSON.stringify(buildIdentity)}`);

    const expectedPages = [
      ['Home', 'Game Setup'],
      ['Automation', 'Automation'],
      ['Map Data', 'World Map Data'],
      ['Squads / AFK', 'Squads'],
      ['City Layout', 'The game is disconnected'],
      ['Hotkeys', 'Game Hotkeys'],
      ['Mini Games', 'Mini Games'],
      ['Settings', 'Settings'],
    ];
    const navigation = [];
    for (const [label, expectedText] of expectedPages) {
      const button = page.locator('.side-nav button', { hasText: label });
      ensure(await button.count() === 1, `navigation entry missing or ambiguous: ${label}`);
      await button.click();
      await page.waitForFunction((text) => document.querySelector('.main-view')?.innerText.includes(text), expectedText);
      navigation.push({
        label,
        expectedText,
        active: await button.getAttribute('aria-current') === 'page',
        renderedText: (await page.locator('.main-view').innerText()).slice(0, 240),
      });
      ensure(navigation[navigation.length - 1].active, `navigation did not activate ${label}`);
    }

    await page.locator('.side-nav button', { hasText: 'Map Data' }).click();
    await page.waitForSelector('.panel.map-panel[data-bridge-mode="native"]');
    const bridgeMode = await page.locator('.panel.map-panel').getAttribute('data-bridge-mode');
    ensure(bridgeMode === 'native', `Map Data bridge mode is ${bridgeMode}`);

    const initialInstance = await nativeInvoke(page, 'profile_instance_status', { profileId }, 15000);
    ensure(initialInstance == null, `exclusive acceptance unexpectedly found an existing profile instance: ${JSON.stringify(initialInstance)}`);
    const started = await nativeInvoke(page, 'profile_instance_start', { profileId, closeUnmanaged: true }, 180000);
    gameStarted = true;
    instanceId = started?.instanceId || '';
    ensure(instanceId, `profile_instance_start did not return an instance id: ${JSON.stringify(started)}`);
    const connectedStatus = await waitForNativeStatus(
      page,
      profileId,
      (status) => status?.instanceId === instanceId && status?.connectionState === 'connected',
      120000,
      'assistant-owned Last War connection',
    );
    await page.waitForFunction(() => document.querySelector('.status-card.status-online strong')?.textContent.trim() === 'Connected', undefined, { timeout: 30000 });

    const modeIndex = process.argv.indexOf('--mode');
    const verificationMode = modeIndex >= 0 ? process.argv[modeIndex + 1] : 'resource';
    if (verificationMode === 'auto-scan') {
      const initialMapStatus = await nativeInvoke(page, 'map_scan_status', { profileId }, 15000);
      ensure(initialMapStatus?.serverId > 0, `Auto Scan current server is unavailable: ${JSON.stringify(initialMapStatus)}`);
      await page.getByRole('tab', { name: 'Auto Scan' }).click();
      const master = page.locator('.map-auto-scan-master input[type=checkbox]');
      ensure(await master.count() === 1, 'Auto Scan master toggle is missing');
      if (await master.isChecked()) await master.click();
      await master.click();
      await page.waitForFunction(() => document.querySelector('.map-status-pill')?.textContent.trim() === 'Reading', undefined, { timeout: 30000 });
      await page.waitForFunction(() => document.querySelector('.map-status-pill')?.textContent.trim() === 'Completed', undefined, { timeout: 480000 });
      const completed = await nativeInvoke(page, 'map_scan_status', { profileId }, 15000);
      ensure(completed?.phase === 'completed' && completed?.readBlocks === completed?.totalBlocks && completed?.failedBlocks === 0,
        `Auto Scan did not complete cleanly: ${JSON.stringify(completed)}`);
      if (await master.isChecked()) await master.click();
      const storedConfig = await page.evaluate((id) => JSON.parse(localStorage.getItem(`lwbridge.mapAutoScan.${id}`) || 'null'), profileId);
      ensure(storedConfig?.enabled === false, `Auto Scan remained enabled after proof: ${JSON.stringify(storedConfig)}`);
      await nativeInvoke(page, 'map_scan_clear', { profileId, serverId: completed.serverId }, 30000);
      const healthy = await waitForNativeStatus(page, profileId, (status) => status?.instanceId === instanceId && status?.connectionState === 'connected', 120000, 'post-auto-scan health');
      await page.screenshot({ path: path.join(screenshotDir, 'auto-scan-completed-disabled.png'), animations: 'disabled' });
      proof = {
        schemaVersion: 1,
        checkpoint: 'LWB317-MAP-AUTO-SCAN-001',
        state: 'proven',
        launch: { applicationArguments: [], url: page.url(), bootstrap, buildIdentity, bridgeMode },
        lifecycle: { assistantOwned: true, instanceId, connectionState: healthy?.connectionState || null },
        autoScan: { currentServerOnly: true, completed, storedConfig, disabledAfterProof: true, clearedAfterProof: true },
        runtimeDiagnostics: diagnostics,
        generatedAt: new Date().toISOString(),
      };
      return;
    }

    const selection = await page.evaluate(() => {
      const labels = [...document.querySelectorAll('.panel.map-panel .map-controls .map-types label')];
      const rows = labels.map((label) => ({ label, text: label.textContent.trim(), input: label.querySelector('input[type=checkbox]') }));
      for (const row of rows) {
        const desired = row.text === 'Resource Point';
        if (row.input && row.input.checked !== desired) row.input.click();
      }
      return rows.map((row) => ({ text: row.text, checked: !!row.input?.checked }));
    });
    ensure(selection.filter((item) => item.checked).length === 1 && selection.some((item) => item.text === 'Resource Point' && item.checked),
      `Resource-only selection failed: ${JSON.stringify(selection)}`);

    await page.evaluate(() => {
      window.__lwb317ScanSamples = [];
      window.__lwb317ScanSampler = window.setInterval(() => {
        const status = document.querySelector('.map-status-pill')?.textContent.trim() || '';
        const progress = document.querySelector('.map-progress span')?.textContent.trim() || '';
        const counters = [...document.querySelectorAll('.map-counters span')].map((node) => node.textContent.trim());
        window.__lwb317ScanSamples.push({ at: Date.now(), status, progress, counters });
        if (window.__lwb317ScanSamples.length > 2400) window.__lwb317ScanSamples.shift();
      }, 250);
    });
    const startButton = page.locator('.map-header .map-actions button', { hasText: 'Start Scan' });
    await page.waitForFunction(() => {
      const button = [...document.querySelectorAll('.map-header .map-actions button')]
        .find((candidate) => candidate.textContent.trim() === 'Start Scan');
      return !!button && !button.disabled;
    }, undefined, { timeout: 30000 });
    await startButton.click();
    await page.waitForFunction(() => document.querySelector('.map-status-pill')?.textContent.trim() === 'Reading', undefined, { timeout: 30000 });
    await page.waitForFunction(() => document.querySelector('.map-status-pill')?.textContent.trim() === 'Completed', undefined, { timeout: 480000 });
    const scanSamples = await page.evaluate(() => {
      window.clearInterval(window.__lwb317ScanSampler);
      return window.__lwb317ScanSamples || [];
    });
    ensure(scanSamples.some((sample) => sample.status === 'Reading'), 'scan progress never exposed the Reading state');
    const completedScanStatus = await nativeInvoke(page, 'map_scan_status', { profileId }, 15000);
    ensure(completedScanStatus?.phase === 'completed' && completedScanStatus?.readBlocks > 0 &&
      completedScanStatus.readBlocks === completedScanStatus.totalBlocks &&
      completedScanStatus.failedBlocks === 0 && completedScanStatus.unreadBlocks === 0,
    `Resource scan did not complete cleanly: ${JSON.stringify(completedScanStatus)}`);

    const resourceTab = page.locator('.map-tabs button', { hasText: 'Resource' });
    await page.waitForFunction(() => {
      const button = [...document.querySelectorAll('.map-tabs button')].find((candidate) => candidate.textContent.includes('Resource'));
      const count = Number(button?.querySelector('.map-tab-count')?.textContent?.replace(/[^0-9]/g, '') || 0);
      return count > 0;
    }, undefined, { timeout: 30000 });
    const resourceCount = numericText(await resourceTab.locator('.map-tab-count').innerText());
    ensure(resourceCount > 0, 'Resource count is zero after completed live scan');
    await resourceTab.click();
    await waitForTable(page);
    const firstPageRows = await tableRows(page);
    ensure(firstPageRows.length > 0, 'Resource page 1 rendered no real rows');
    const firstPageResultText = await page.locator('.map-result-count').innerText();
    await page.screenshot({ path: path.join(screenshotDir, 'resource-page-1.png'), animations: 'disabled' });

    const nextButton = page.locator('.map-pagination button', { hasText: 'Next' });
    ensure(await nextButton.count() === 1 && !(await nextButton.isDisabled()), 'Resource page 2 control is unavailable');
    await nextButton.click();
    await page.waitForFunction(() => document.querySelector('.map-pagination span')?.textContent.includes('Page 2'), undefined, { timeout: 30000 });
    await waitForTable(page);
    const secondPageRows = await tableRows(page);
    ensure(secondPageRows.length > 0, 'Resource page 2 rendered no rows');
    const firstFingerprints = new Set(firstPageRows.map((row) => JSON.stringify(row)));
    const duplicateRenderedRows = secondPageRows.filter((row) => firstFingerprints.has(JSON.stringify(row))).length;
    ensure(duplicateRenderedRows === 0, `Resource page 2 duplicated ${duplicateRenderedRows} rendered page-1 rows`);

    const resourceSelect = page.locator('select[aria-label="Resource name"]');
    const filterOptions = await resourceSelect.locator('option').evaluateAll((options) => options
      .map((option) => ({ value: option.value, text: option.textContent.trim() }))
      .filter((option) => option.value));
    ensure(filterOptions.length > 0, 'Resource-name filter has no live options');
    const selectedFilter = filterOptions[0];
    await resourceSelect.selectOption(selectedFilter.value);
    await waitForTable(page);
    await page.waitForFunction(() => document.querySelector('.map-pagination span')?.textContent.includes('Page 1') || !document.querySelector('.map-pagination'), undefined, { timeout: 30000 });
    const filteredRows = await tableRows(page);
    const filteredResultText = await page.locator('.map-result-count').innerText();
    ensure(filteredRows.length > 0 && numericText(filteredResultText) > 0,
      `Resource-name filter returned no rows: ${selectedFilter.value}`);

    const preClearScreenshot = path.join(screenshotDir, 'resource-filtered.png');
    await page.screenshot({ path: preClearScreenshot, animations: 'disabled' });
    const clearButton = page.locator('.map-header .map-actions button', { hasText: 'Clear Map Data' });
    ensure(!(await clearButton.isDisabled()), 'Clear Map Data is disabled after completed Resource scan');
    await clearButton.click();
    await page.waitForFunction(() => {
      const resource = [...document.querySelectorAll('.map-tabs button')].find((candidate) => candidate.textContent.includes('Resource'));
      const count = resource?.querySelector('.map-tab-count')?.textContent.trim();
      const table = document.querySelector('.map-table--resource');
      return count === '0' && !!table?.querySelector('tbody td.map-empty') && table.getAttribute('aria-busy') !== 'true';
    }, undefined, { timeout: 30000 });
    const clearedRows = await tableRows(page);
    ensure(clearedRows.length === 0, 'Clear Map Data left rendered Resource rows');

    const postClearStatus = await waitForNativeStatus(
      page,
      profileId,
      (status) => status?.instanceId === instanceId && status?.connectionState === 'connected',
      30000,
      'post-clear Last War health',
    );
    const postClearMapStatus = await nativeInvoke(page, 'map_scan_status', { profileId }, 15000);
    const connectionText = await page.locator('.status-card.status-online strong').innerText();
    ensure(connectionText.trim() === 'Connected' && postClearStatus?.instanceId === instanceId,
      'same Last War session was not healthy after Clear');
    await page.screenshot({ path: path.join(screenshotDir, 'post-clear.png'), animations: 'disabled' });

    diagnostics.securityPolicyViolations = await page.evaluate(() => window.__lwb317SecurityPolicyViolations || []);
    proof = {
      schemaVersion: 1,
      checkpoint: 'LWB317-MAP-UI-PRODUCTIONIZE-001',
      state: 'proven',
      launch: {
        applicationArguments: [],
        url: page.url(),
        bootstrap,
        buildIdentity,
        uiProject: await page.locator('.app-shell').getAttribute('data-ui-project'),
        bridgeMode,
      },
      navigation,
      lifecycle: {
        assistantOwned: true,
        instanceId,
        gamePid: connectedStatus?.pid || null,
        connectionState: connectedStatus?.connectionState || null,
      },
      mapResource: {
        selectedTypes: ['resource'],
        scan: completedScanStatus,
        progressObserved: scanSamples.some((sample) => sample.status === 'Reading'),
        progressSamples: scanSamples.filter((_, index) => index % Math.max(1, Math.floor(scanSamples.length / 20)) === 0).slice(0, 24),
        resourceCount,
        page1: {
          resultText: firstPageResultText,
          renderedRows: firstPageRows.length,
          firstRow: firstPageRows[0],
        },
        page2: {
          renderedRows: secondPageRows.length,
          firstRow: secondPageRows[0],
          duplicateRenderedRowsFromPage1: duplicateRenderedRows,
        },
        resourceFilter: {
          value: selectedFilter.value,
          optionText: selectedFilter.text,
          resultText: filteredResultText,
          renderedRows: filteredRows.length,
        },
        clear: {
          renderedRows: clearedRows.length,
          resourceCount: 0,
          mapStatus: postClearMapStatus,
        },
        postClearHealth: {
          sameInstance: postClearStatus?.instanceId === instanceId,
          connectionState: postClearStatus?.connectionState || null,
          connectionText: connectionText.trim(),
        },
      },
      runtimeDiagnostics: diagnostics,
      generatedAt: new Date().toISOString(),
    };
    ensure(diagnostics.consoleErrors.length === 0, `console errors: ${JSON.stringify(diagnostics.consoleErrors)}`);
    ensure(diagnostics.pageErrors.length === 0, `page errors: ${JSON.stringify(diagnostics.pageErrors)}`);
    ensure(diagnostics.requestFailures.length === 0, `request failures: ${JSON.stringify(diagnostics.requestFailures)}`);
    ensure(diagnostics.badResponses.length === 0, `failed HTTP responses: ${JSON.stringify(diagnostics.badResponses)}`);
    ensure(diagnostics.securityPolicyViolations.length === 0,
      `CSP violations: ${JSON.stringify(diagnostics.securityPolicyViolations)}`);
  } catch (error) {
    fatalError = { message: String(error?.message || error), stack: String(error?.stack || '') };
    throw error;
  } finally {
    if (page && gameStarted) {
      cleanup.attempted = true;
      try {
        let current = await nativeInvoke(page, 'profile_instance_status', { profileId }, 15000).catch(() => null);
        const stopId = current?.instanceId || instanceId;
        if (stopId) {
          await nativeInvoke(page, 'profile_instance_stop', { profileId, instanceId: stopId }, 120000);
          cleanup.stopped = true;
        }
      } catch (error) {
        cleanup.error = String(error?.message || error);
      }
    }
    if (proof) proof.cleanup = cleanup;
    const output = proof || {
      schemaVersion: 1,
      checkpoint: 'LWB317-MAP-UI-PRODUCTIONIZE-001',
      state: 'failed',
      runtimeDiagnostics: diagnostics,
      cleanup,
      error: fatalError,
      generatedAt: new Date().toISOString(),
    };
    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2) + '\n');
    await browser?.close().catch(() => {});
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
