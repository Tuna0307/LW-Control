const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const providerSource = fs.readFileSync(
  path.join(root, 'src/LWBridge.Desktop/WebUi/local-providers.js'),
  'utf8'
);

const flush = () => new Promise(resolve => setImmediate(resolve));

function createHarness(initialValue = false) {
  const hooks = [];
  const pending = [];
  let cursor = 0;
  const Jt = {Provider: {}};
  const M = {
    Fragment: {},
    jsx(type, props) {
      return type === Jt.Provider ? props.value : {type, props};
    },
    jsxs(type, props) {
      return {type, props};
    }
  };
  const context = {
    window: {
      __LWBridgeBootstrap: {autoLaunchGame: initialValue},
      LWBridgePreview: {
        mode: 'live',
        profiles: {selectedProfileId: 'preference-check'},
        invoke(command, payload) {
          return new Promise((resolve, reject) => pending.push({command, payload, resolve, reject}));
        }
      }
    },
    j: {
      useState(initial) {
        const slot = cursor++;
        if (!(slot in hooks)) hooks[slot] = typeof initial === 'function' ? initial() : initial;
        return [hooks[slot], value => {
          hooks[slot] = typeof value === 'function' ? value(hooks[slot]) : value;
        }];
      },
      useRef(initial) {
        const slot = cursor++;
        return hooks[slot] ??= {current: initial};
      },
      useEffect() {
        cursor++;
      }
    },
    Jt,
    M,
    Promise
  };
  vm.createContext(context);
  vm.runInContext(providerSource, context);
  return {
    pending,
    render() {
      cursor = 0;
      return vm.runInContext('Xt({children:null})', context);
    }
  };
}

async function settle(harness, index, outcome) {
  while (harness.pending.length <= index) await flush();
  const request = harness.pending[index];
  if (outcome === 'success') {
    request.resolve({autoLaunchGame: request.payload.autoLaunchGame});
  } else {
    request.reject(Object.assign(new Error(`save ${index + 1} failed`), {code: 'CONFIG_WRITE_FAILED'}));
  }
  await flush();
  await flush();
}

async function runRapidScenario(first, second) {
  const harness = createHarness(false);
  harness.render().setAutoLaunchGame(true);
  harness.render().setAutoLaunchGame(false);
  await settle(harness, 0, first);
  await settle(harness, 1, second);
  const value = harness.render();
  return {
    first,
    second,
    finalUi: value.autoLaunchGame,
    errors: value.profileLaunchErrors.map(error => error.error),
    sentValues: harness.pending.map(request => request.payload.autoLaunchGame)
  };
}

(async () => {
  const bothFail = await runRapidScenario('fail', 'fail');
  const failThenSuccess = await runRapidScenario('fail', 'success');
  const successThenFail = await runRapidScenario('success', 'fail');
  const bothSuccess = await runRapidScenario('success', 'success');

  const recoveryHarness = createHarness(false);
  recoveryHarness.render().setAutoLaunchGame(true);
  await settle(recoveryHarness, 0, 'fail');
  const failed = recoveryHarness.render();
  recoveryHarness.render().setAutoLaunchGame(true);
  await settle(recoveryHarness, 1, 'success');
  const recovered = recoveryHarness.render();

  const checks = {
    bothFail: bothFail.finalUi === false && bothFail.errors.length === 1,
    failThenSuccess: failThenSuccess.finalUi === false && failThenSuccess.errors.length === 0,
    successThenFail: successThenFail.finalUi === true && successThenFail.errors.length === 1,
    bothSuccess: bothSuccess.finalUi === false && bothSuccess.errors.length === 0,
    recovery: failed.autoLaunchGame === false && failed.profileLaunchErrors.length === 1 &&
      recovered.autoLaunchGame === true && recovered.profileLaunchErrors.length === 0
  };
  const result = {
    ok: Object.values(checks).every(Boolean),
    checks,
    scenarios: {bothFail, failThenSuccess, successThenFail, bothSuccess},
    recovery: {
      failedUi: failed.autoLaunchGame,
      failedErrors: failed.profileLaunchErrors.map(error => error.error),
      recoveredUi: recovered.autoLaunchGame,
      recoveredErrors: recovered.profileLaunchErrors.map(error => error.error)
    }
  };
  console.log(JSON.stringify(result, null, 2));
  if (!result.ok) process.exitCode = 1;
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
