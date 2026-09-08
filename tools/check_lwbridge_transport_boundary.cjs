const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const repoRoot = path.resolve(__dirname, '..');

function plain(value) {
  return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
}

function makeStorage() {
  const values = new Map();
  return {
    getItem(key) { return values.has(key) ? values.get(key) : null; },
    setItem(key, value) { values.set(String(key), String(value)); },
    removeItem(key) { values.delete(String(key)); },
    clear() { values.clear(); },
  };
}

async function checkRecoveredProfileWrappers() {
  const apiPath = path.join(repoRoot, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'api-ClPPi2JT.js');
  const source = fs.readFileSync(apiPath, 'utf8');
  const start = source.indexOf('function U(e,t){');
  const end = source.indexOf('function G(e){', start);
  assert.ok(start >= 0 && end > start, 'generated U/W wrapper functions must be present');

  const wrapperSource = source.slice(start, end);
  const calls = [];
  const listeners = new Map();
  const context = {
    window: {
      LWBridgePreview: {
        invoke(command, payload) {
          calls.push({command, payload: plain(payload)});
          return Promise.resolve(payload);
        },
        listen(event, callback) {
          listeners.set(event, callback);
          return () => listeners.delete(event);
        },
      },
    },
  };
  vm.createContext(context);
  vm.runInContext(
    'let __active=`profile-a`; function T(){return __active}' +
      wrapperSource +
      ';globalThis.__test={U,W,setProfile:value=>{__active=value}};',
    context,
    {filename: 'generated-profile-wrapper-check.js'},
  );

  await context.__test.U('implicit', undefined);
  assert.deepEqual(calls.at(-1), {command: 'implicit', payload: {profileId: 'profile-a'}});

  await context.__test.U('explicit', {profileId: 'profile-explicit', value: 7});
  assert.deepEqual(calls.at(-1), {command: 'explicit', payload: {profileId: 'profile-explicit', value: 7}});

  await context.__test.U('number', 42);
  assert.deepEqual(calls.at(-1), {command: 'number', payload: {value: 42, profileId: 'profile-a'}});

  await context.__test.U('null', null);
  assert.deepEqual(calls.at(-1), {command: 'null', payload: {profileId: 'profile-a'}});

  const received = [];
  context.__test.W('bridge://status', payload => received.push(plain(payload)));
  const dispatch = listeners.get('bridge://status');
  assert.equal(typeof dispatch, 'function');

  dispatch({profileId: 'profile-a', payload: {sequence: 1}});
  context.__test.setProfile('profile-b');
  dispatch({profileId: 'profile-a', payload: {sequence: 2}}); // stale after profile switch
  dispatch({profileId: 'profile-b', payload: {sequence: 3}});
  dispatch({sequence: 4}); // recovered raw/global-event behavior
  assert.deepEqual(received, [{sequence: 1}, {sequence: 3}, {sequence: 4}]);
}

async function checkLiveBrowserTransport() {
  const hostPath = path.join(repoRoot, 'src', 'LWBridge.Desktop', 'WebUi', 'preview-host.js');
  const source = fs.readFileSync(hostPath, 'utf8');
  const posted = [];
  let nativeMessageHandler = null;
  let uuidCounter = 0;
  const realSetTimeout = setTimeout;
  const fastSetTimeout = (callback, ms, ...args) =>
    realSetTimeout(callback, ms >= 30000 ? 10 : ms, ...args);

  const nativeWebView = {
    addEventListener(event, callback) {
      if (event === 'message') nativeMessageHandler = callback;
    },
    postMessage(message) {
      posted.push(plain(message));
    },
  };
  const window = {
    __LWBridgeBootstrap: {
      mode: 'live',
      sessionId: 'session-a',
      profiles: {
        selectedProfileId: 'profile-a',
        profiles: [{id: 'profile-a', displayName: 'Profile A'}],
      },
    },
    chrome: {webview: nativeWebView},
  };
  const sandbox = {
    window,
    location: {search: ''},
    URLSearchParams,
    structuredClone: global.structuredClone || plain,
    localStorage: makeStorage(),
    crypto: {randomUUID: () => `request-${++uuidCounter}`},
    setTimeout: fastSetTimeout,
    clearTimeout,
    addEventListener() {},
    console,
  };
  window.setTimeout = fastSetTimeout;
  window.clearTimeout = clearTimeout;
  vm.createContext(sandbox);
  vm.runInContext(source, sandbox, {filename: 'preview-host.js'});

  assert.equal(window.LWBridgePreview.mode, 'live');
  assert.equal(typeof nativeMessageHandler, 'function');

  let settled = false;
  const responsePromise = window.LWBridgePreview.invoke('profile_list', {}).then(value => {
    settled = true;
    return value;
  });
  const request = posted.find(message => message.kind === 'invoke' && message.command === 'profile_list');
  assert.ok(request, 'live invoke must cross the native transport');

  nativeMessageHandler({data: {kind: 'response', sessionId: 'wrong-session', id: request.id, ok: true, result: {wrong: true}}});
  await Promise.resolve();
  assert.equal(settled, false, 'wrong-session response must be ignored');

  nativeMessageHandler({data: {kind: 'response', sessionId: 'session-a', id: request.id, ok: true, result: {ok: true}}});
  assert.deepEqual(plain(await responsePromise), {ok: true});

  const events = [];
  window.LWBridgePreview.listen('bridge://status', value => events.push(plain(value)));
  nativeMessageHandler({data: {kind: 'event', sessionId: 'wrong-session', event: 'bridge://status', payload: {ignored: true}}});
  nativeMessageHandler({data: {kind: 'event', sessionId: 'session-a', event: 'bridge://status', payload: {accepted: true}}});
  assert.deepEqual(events, [{accepted: true}], 'wrong-session event must be ignored');

  const timeoutPromise = window.LWBridgePreview.invoke('delayed_test', {});
  const delayedRequest = posted.find(message => message.kind === 'invoke' && message.command === 'delayed_test');
  assert.ok(delayedRequest);
  await assert.rejects(timeoutPromise, error => error && error.code === 'COMMAND_TIMEOUT');
  assert.ok(posted.some(message =>
    message.kind === 'cancel' && message.sessionId === 'session-a' && message.id === delayedRequest.id),
  'timeout must propagate cancellation to native ownership');

  // A completion arriving after timeout has no pending owner and must be ignored.
  nativeMessageHandler({data: {kind: 'response', sessionId: 'session-a', id: delayedRequest.id, ok: true, result: {late: true}}});
  await Promise.resolve();
}

(async () => {
  await checkRecoveredProfileWrappers();
  await checkLiveBrowserTransport();
  console.log('transport boundary check passed');
})().catch(error => {
  console.error(error.stack || error);
  process.exitCode = 1;
});
