// Independent audit of the actual local provider using deterministic hook storage.
// No browser, filesystem persistence or game service is invoked by the provider.
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const root = process.cwd();
const hooks = [];
const pending = [];
let cursor = 0;
const context = {
  window: {
    __LWBridgeBootstrap: {autoLaunchGame: false},
    LWBridgePreview: {
      mode: 'live', profiles: {selectedProfileId: 'audit-profile'},
      invoke(command, payload) {
        return new Promise((resolve, reject) => pending.push({command, payload, resolve, reject}));
      }
    }
  },
  j: {
    useState(initial) {
      const slot = cursor++;
      if (!(slot in hooks)) hooks[slot] = typeof initial === 'function' ? initial() : initial;
      return [hooks[slot], value => { hooks[slot] = typeof value === 'function' ? value(hooks[slot]) : value; }];
    },
    useRef(initial) {
      const slot = cursor++;
      return hooks[slot] ??= {current: initial};
    },
    useEffect() { cursor++; }
  },
  Jt: {Provider: {}},
  M: {
    Fragment: {},
    jsx: (type, props) => type === context.Jt.Provider ? props.value : {type, props},
    jsxs: (type, props) => ({type, props})
  },
  Promise
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(root, 'src/LWBridge.Desktop/WebUi/local-providers.js'), 'utf8'), context);
const render = () => { cursor = 0; return vm.runInContext('Xt({children:null})', context); };
const flush = () => new Promise(resolve => setImmediate(resolve));
(async () => {
  render().setAutoLaunchGame(true);
  render().setAutoLaunchGame(false);
  await flush();
  if (pending.length !== 1) throw new Error('First serialized save not started');
  pending[0].reject(Object.assign(new Error('First isolated save failed'), {code: 'CONFIG_WRITE_FAILED'}));
  await flush();
  if (pending.length !== 2) throw new Error('Second serialized save not started');
  pending[1].reject(Object.assign(new Error('Second isolated save failed'), {code: 'CONFIG_WRITE_FAILED'}));
  await flush();
  const result = {id:'PM3-02', scenario:'false -> true -> false, both serialized saves fail', lastCommitted:false, finalUi:render().autoLaunchGame, sentValues:pending.map(p => p.payload.autoLaunchGame)};
  result.passed = result.finalUi === result.lastCommitted;
  console.log(JSON.stringify(result, null, 2));
})().catch(error => { console.error(error); process.exitCode = 1; });
