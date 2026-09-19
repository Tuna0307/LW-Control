const path = require('path');

global.window = global;
global.location = {search: ''};
global.localStorage = {
  getItem() { return null; },
  setItem() {},
  removeItem() {},
  clear() {},
};
global.addEventListener = () => {};

window.__LWBridgeBootstrap = {
  mode: 'live',
  sessionId: 'missing-native-check',
  profiles: {
    selectedProfileId: 'local-check',
    profiles: [{id: 'local-check'}],
  },
};

require(path.resolve(__dirname, '..', 'src', 'LWBridge.Desktop', 'WebUi', 'preview-host.js'));

(async () => {
  if (window.LWBridgePreview.mode !== 'live') {
    throw new Error(`expected live mode, got ${window.LWBridgePreview.mode}`);
  }

  try {
    await window.LWBridgePreview.invoke('profile_list');
    throw new Error('requested live mode unexpectedly used fixture transport');
  } catch (error) {
    if (error?.code !== 'NATIVE_TRANSPORT_MISSING') throw error;
  }

  if (!window.LWBridgePreview.failures.some(item => item.includes('NATIVE_TRANSPORT_MISSING'))) {
    throw new Error('missing-native failure was not surfaced to diagnostics');
  }

  console.log('live transport missing-native check passed');
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
