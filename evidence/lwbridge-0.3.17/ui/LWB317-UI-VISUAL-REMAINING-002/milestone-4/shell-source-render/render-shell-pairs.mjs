import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../../..');
const ui = path.join(repo, 'src/LWBridge.UI-0.3.17');
const src = path.join(ui, 'src');
const assets = path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets');
const requireUi = createRequire(path.join(ui, 'package.json'));
const { parse } = requireUi('@babel/parser');
const { transformSync } = requireUi('esbuild');
const React = requireUi('react');
const { renderToStaticMarkup } = requireUi('react-dom/server');
const verifyOnly = process.argv.includes('--verify');

const hash = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const read = (file) => fs.readFileSync(file, 'utf8');
const persist = (file, value) => {
  const text = String(value);
  if (verifyOnly) assert.equal(read(file), text, `Fresh source execution differs from pinned output: ${path.relative(repo, file)}`);
  else {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, text);
  }
};
const json = (file, value) => persist(path.join(here, file), `${JSON.stringify(value, null, 2)}\n`);

const originalAssetFile = path.join(assets, 'index-BVfnK1wp.js');
const originalAssetBytes = fs.readFileSync(originalAssetFile);
assert.equal(hash(originalAssetBytes), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
const originalSource = originalAssetBytes.toString('utf8');
const originalAst = parse(originalSource, { sourceType: 'module' });
const originalNodes = [];
function walk(node) {
  if (!node || typeof node !== 'object') return;
  if (node.type) originalNodes.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(walk);
    else if (value && typeof value === 'object') walk(value);
  }
}
walk(originalAst);
const byteOffset = (start) => Buffer.byteLength(originalSource.slice(0, start));
const locateNode = (node) => {
  const text = originalSource.slice(node.start, node.end);
  return { byteOffset: byteOffset(node.start), byteLength: Buffer.byteLength(text), sha256: hash(text) };
};
function functionAt(name, expectedByte) {
  const node = originalNodes.find((entry) => entry.type === 'FunctionDeclaration' && entry.id?.name === name && byteOffset(entry.start) === expectedByte);
  assert.ok(node, `original function ${name} @ ${expectedByte}`);
  return node;
}
function functionNamed(name) {
  const matches = originalNodes.filter((entry) => entry.type === 'FunctionDeclaration' && entry.id?.name === name);
  assert.ok(matches.length, `original function ${name}`);
  return matches.at(-1);
}
function variableNamed(name) {
  const matches = originalNodes.filter((entry) => entry.type === 'VariableDeclarator' && entry.id?.type === 'Identifier' && entry.id.name === name);
  assert.ok(matches.length, `original variable ${name}`);
  return matches.at(-1);
}
function sourceFunction(node, env = {}) {
  return new Function(...Object.keys(env), `return (${originalSource.slice(node.start, node.end)});`)(...Object.values(env));
}
function sourceValue(node, env = {}) {
  return new Function(...Object.keys(env), `return (${originalSource.slice(node.init.start, node.init.end)});`)(...Object.values(env));
}

const sourceNodes = {
  Oe: functionAt('Oe', 199047),
  Fn: functionAt('Fn', 208687),
  In: functionAt('In', 209017),
  Yr: functionAt('Yr', 339285),
  Qr: functionAt('Qr', 349778),
  ti: functionAt('ti', 350656),
  ai: functionAt('ai', 354064),
  si: functionAt('si', 354710),
  Gi: functionAt('Gi', 361306),
  qi: functionAt('qi', 375835),
  Ji: functionAt('Ji', 376111),
  ce: functionAt('ce', 194447),
  le: functionAt('le', 194621),
  ue: functionAt('ue', 194751),
  Vr: functionNamed('Vr'),
  Xr: functionNamed('Xr'),
  Lr: functionNamed('Lr'),
  Ir: functionNamed('Ir'),
};
assert.deepEqual(locateNode(sourceNodes.Fn), { byteOffset: 208687, byteLength: 330, sha256: '590CD67C68F8A77B53F82185E6417DE75BA06DE81DC7281D67FF394A47E4941B' });
assert.deepEqual(locateNode(sourceNodes.In), { byteOffset: 209017, byteLength: 1209, sha256: 'E42C17AAC67AFE7A73D525902C9E757644E40838422730CEB32EC47830C4F8A2' });
assert.deepEqual(locateNode(sourceNodes.Yr), { byteOffset: 339285, byteLength: 7154, sha256: 'FE5D0408492E71E9DD74615C269699B8C75B55D346F823BC22B05992C330A490' });
assert.deepEqual(locateNode(sourceNodes.si), { byteOffset: 354710, byteLength: 3232, sha256: '32E9CFF50FC34FCD3B91B2A3256AEC4DC9C605E98713B4F8F0ED536A4B94B405' });
assert.deepEqual(locateNode(sourceNodes.Gi), { byteOffset: 361306, byteLength: 14461, sha256: 'AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5' });
assert.deepEqual(locateNode(sourceNodes.Ji), { byteOffset: 376111, byteLength: 881, sha256: '72F95F7202BC43FD3F9E449DC77133A08C57DE1C8D6F48AB929DCC1D34F48C21' });
assert.equal(locateNode(sourceNodes.ti).sha256, '04B9D1EE3469A345EB2F41368FE06EDFFD43645CDE880B9541E75B84B101FD05');

const Fragment = Symbol('fragment');
const Activity = Symbol('activity');
const Suspense = Symbol('suspense');
function element(type, props, ...children) {
  const next = { ...(props || {}) };
  if (children.length) next.children = children.length === 1 ? children[0] : children;
  return { type, props: next };
}
const M = {
  Fragment,
  jsx: (type, props, key) => element(type, key === undefined ? props : { ...(props || {}), key }),
  jsxs: (type, props, key) => element(type, key === undefined ? props : { ...(props || {}), key }),
};

const hookBindings = new Map();
function registerBindings(key, source, node) {
  const bindings = [];
  function visit(entry) {
    if (!entry || typeof entry !== 'object') return;
    if (entry.type === 'VariableDeclarator' && entry.id?.type === 'ArrayPattern' && entry.init?.type === 'CallExpression') {
      let callee = entry.init.callee;
      if (callee?.type === 'SequenceExpression') callee = callee.expressions.at(-1);
      if (callee?.type === 'MemberExpression' && callee.property?.name === 'useState') {
        bindings.push({ offset: entry.start, name: entry.id.elements[0]?.name || '' });
      } else if (callee?.type === 'Identifier' && callee.name === 'useState') {
        bindings.push({ offset: entry.start, name: entry.id.elements[0]?.name || '' });
      }
    }
    for (const value of Object.values(entry)) {
      if (Array.isArray(value)) value.forEach(visit);
      else if (value && typeof value === 'object') visit(value);
    }
  }
  visit(node);
  bindings.sort((a, b) => a.offset - b.offset);
  hookBindings.set(key, bindings.map((entry) => entry.name));
}
for (const name of ['Gi', 'si', 'ti', 'Yr', 'qi']) registerBindings(`original:${name}`, originalSource, sourceNodes[name]);

let activeCase = null;
let hookContext = { key: '', index: 0 };
function stateOverride(key, binding, fallback) {
  const scope = activeCase?.stateOverrides?.[key];
  return scope && Object.prototype.hasOwnProperty.call(scope, binding) ? scope[binding] : fallback;
}
const fakeReact = {
  Activity,
  Suspense,
  useState(initial) {
    const binding = hookBindings.get(hookContext.key)?.[hookContext.index++] || '';
    const fallback = typeof initial === 'function' ? initial() : initial;
    return [stateOverride(hookContext.key, binding, fallback), () => {}];
  },
  useRef(value) { return { current: value }; },
  useEffect() {},
  useLayoutEffect() {},
  useTransition() { return [false, (fn) => fn()]; },
  useCallback(fn) { return fn; },
  useMemo(fn) { return fn(); },
  useId() { return 'shell-proof-id'; },
  useSyncExternalStore(_subscribe, getSnapshot) { return getSnapshot(); },
};

function materialize(node) {
  if (node == null || typeof node === 'boolean') return null;
  if (Array.isArray(node)) return node.map((entry, index) => React.createElement(React.Fragment, { key: entry?.props?.key ?? index }, materialize(entry)));
  if (typeof node !== 'object') return node;
  if (node.type === Fragment) return materialize(node.props?.children);
  if (node.type === Activity) return node.props?.mode === 'hidden' ? null : materialize(node.props?.children);
  if (node.type === Suspense) return materialize(node.props?.children);
  if (typeof node.type === 'function') {
    const previous = hookContext;
    const key = node.type.__proofKey || `runtime:${node.type.name || 'anonymous'}`;
    hookContext = { key, index: 0 };
    const rendered = node.type(node.props || {});
    hookContext = previous;
    return materialize(rendered);
  }
  const props = {};
  for (const [key, value] of Object.entries(node.props || {})) {
    if (key === 'children' || key === 'ref' || key === 'key' || value === undefined) continue;
    props[key] = value;
  }
  const children = materialize(node.props?.children);
  return React.createElement(node.type, props, ...(Array.isArray(children) ? children : [children]));
}
const tag = (fn, key) => Object.assign(fn, { __proofKey: key });

function currentFunction(file, name, env = {}) {
  const source = read(file);
  const ast = parse(source, { sourceType: 'module', plugins: ['jsx'] });
  const node = ast.program.body.map((entry) => entry.type === 'ExportNamedDeclaration' ? entry.declaration : entry)
    .find((entry) => entry?.type === 'FunctionDeclaration' && entry.id?.name === name);
  assert.ok(node, `current function ${name}`);
  const code = transformSync(source.slice(node.start, node.end), { loader: 'jsx', jsxFactory: 'h', jsxFragment: 'Fragment', target: 'es2022' }).code;
  return new Function(...Object.keys(env), `${code}\nreturn ${name};`)(...Object.values(env));
}
function currentModule(file, names, env = {}) {
  const source = read(file);
  const ast = parse(source, { sourceType: 'module', plugins: ['jsx'] });
  const declarations = [];
  for (const entry of ast.program.body) {
    if (entry.type === 'ImportDeclaration' || entry.type === 'ExportAllDeclaration') continue;
    const node = entry.type === 'ExportNamedDeclaration' ? entry.declaration : entry;
    if (node) declarations.push(source.slice(node.start, node.end));
  }
  const code = transformSync(declarations.join('\n'), { loader: 'jsx', jsxFactory: 'h', jsxFragment: 'Fragment', target: 'es2022' }).code;
  return new Function(...Object.keys(env), `${code}\nreturn {${names.join(',')}};`)(...Object.values(env));
}
function registerCurrentBindings(file, functionName, key) {
  const source = read(file);
  const ast = parse(source, { sourceType: 'module', plugins: ['jsx'] });
  let node = null;
  for (const entry of ast.program.body) {
    const candidate = entry.type === 'ExportNamedDeclaration' ? entry.declaration : entry;
    if (candidate?.type === 'FunctionDeclaration' && candidate.id?.name === functionName) node = candidate;
  }
  assert.ok(node, `current function ${functionName}`);
  registerBindings(key, source, node);
}

const currentFiles = {
  app: path.join(src, 'App.jsx'),
  presentation: path.join(src, 'ShellPresentation.jsx'),
  profiles: path.join(src, 'ProfileSidebar.jsx'),
  profileSwitch: path.join(src, 'ProfileSwitchState.jsx'),
  exit: path.join(src, 'AppExitDialog.jsx'),
  navIcon: path.join(src, 'NavIcon.jsx'),
  routes: path.join(src, 'routes.js'),
  i18n: path.join(src, 'i18n.jsx'),
  en: path.join(src, 'locales/en.js'),
  ja: path.join(src, 'locales/ja.js'),
  referenceCss: path.join(src, 'reference.css'),
  stylesCss: path.join(src, 'styles.css'),
};
registerCurrentBindings(currentFiles.profiles, 'ProfileSidebar', 'current:ProfileSidebar');
registerCurrentBindings(currentFiles.exit, 'AppExitPrompt', 'current:AppExitPrompt');

const appSource = read(currentFiles.app);
const appAst = parse(appSource, { sourceType: 'module', plugins: ['jsx'] });
const appNode = appAst.program.body.map((entry) => entry.type === 'ExportNamedDeclaration' ? entry.declaration : entry)
  .find((entry) => entry?.type === 'FunctionDeclaration' && entry.id?.name === 'App');
assert.ok(appNode, 'current App');
const appReturn = appNode.body.body.findLast?.((entry) => entry.type === 'ReturnStatement')
  || [...appNode.body.body].reverse().find((entry) => entry.type === 'ReturnStatement');
assert.ok(appReturn?.argument, 'current App return expression');
const appReturnSource = appSource.slice(appReturn.argument.start, appReturn.argument.end);
const appReturnCode = transformSync(`const __tree = ${appReturnSource};`, { loader: 'jsx', jsxFactory: 'h', jsxFragment: 'Fragment', target: 'es2022' }).code;
function renderCurrentReturn(env) {
  return new Function(...Object.keys(env), `${appReturnCode}\nreturn __tree;`)(...Object.values(env));
}

const routesModule = await import(pathToFileURL(currentFiles.routes).href);
const currentRoutes = routesModule.routes;
const i18nSource = read(currentFiles.i18n);
const i18nAst = parse(i18nSource, { sourceType: 'module', plugins: ['jsx'] });
const languagesNode = i18nAst.program.body.flatMap((entry) => entry.type === 'ExportNamedDeclaration' ? [entry.declaration] : [entry])
  .flatMap((entry) => entry?.type === 'VariableDeclaration' ? entry.declarations : [])
  .find((entry) => entry.id?.name === 'LANGUAGES');
assert.ok(languagesNode, 'current LANGUAGES');
const currentLanguages = new Function(`return (${i18nSource.slice(languagesNode.init.start, languagesNode.init.end)});`)();

const originalLanguages = sourceValue(variableNamed('xe'));
const originalRoutes = sourceValue(variableNamed('Zr'));
const statusKeys = sourceValue(variableNamed('ii'));
const connectionKeys = sourceValue(variableNamed('Jr'));
assert.deepEqual(currentRoutes.map(({ key }) => key), originalRoutes.map(({ key }) => key));

const currentCatalogs = {
  en: (await import(pathToFileURL(currentFiles.en).href)).default,
  ja: (await import(pathToFileURL(currentFiles.ja).href)).default,
};
function recoveredCatalog(language) {
  const file = fs.readdirSync(assets).find((name) => name.startsWith(`${language}-`) && name.endsWith('.js'));
  assert.ok(file, `recovered ${language} locale asset`);
  const source = read(path.join(assets, file));
  const ast = parse(source, { sourceType: 'module' });
  const messages = {};
  function collect(node) {
    if (!node || typeof node !== 'object') return;
    if (node.type === 'ObjectProperty' && node.key?.type === 'StringLiteral' && (node.value?.type === 'StringLiteral' || node.value?.type === 'TemplateLiteral')) {
      messages[node.key.value] = node.value.type === 'StringLiteral' ? node.value.value : node.value.quasis[0].value.cooked;
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(collect);
      else if (value && typeof value === 'object') collect(value);
    }
  }
  collect(ast);
  return { file: path.join(assets, file), messages };
}
const originalCatalogs = { en: recoveredCatalog('en'), ja: recoveredCatalog('ja') };
const tFor = (catalog) => (key, values = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));

const transparentPixel = 'data:image/gif;base64,R0lGODlhAQABAAAAACw=';
function makeStore(error = null) {
  return {
    subscribe: () => () => {},
    getSnapshot: () => ({ error, saving: false, dirty: !!error }),
    flush: async () => {},
    refresh: async () => {},
  };
}
function makeProfiles(showProfiles) {
  const profiles = showProfiles ? [
    { id: 'profile-1', roleName: 'Alpha', displayName: 'Alpha', serverId: 321, note: 'Front line', enabled: true },
    { id: 'profile-2', roleName: 'Bravo', displayName: 'Bravo', serverId: 322, note: '', enabled: true },
    { id: 'profile-3', roleName: 'Charlie', displayName: 'Charlie', serverId: 323, note: 'Reserve', enabled: false },
  ] : [{ id: 'profile-1', roleName: 'Alpha', displayName: 'Alpha', serverId: 321, note: '', enabled: true }];
  return { profiles, selectedProfileId: 'profile-1', maxProfiles: showProfiles ? 5 : 1 };
}

const cases = [
  { id: 'base-single-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: false },
  { id: 'update-connected-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: false, online: true, pending: 3, update: { phase: 'available', currentVersion: '0.3.17', latestVersion: '0.3.18', progress: null } },
  { id: 'profiles-compact-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true },
  { id: 'profile-cached-return-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, cachedReturn: true, browserHover: '.profile-compact-item.active', browserFocus: '.profile-collapse' },
  { id: 'profiles-expanded-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, expanded: true },
  { id: 'profiles-error-forced-open-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, profileError: 'CONTROLLED_PROFILE_ERROR' },
  { id: 'profile-note-modal-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, expanded: true, noteModal: true },
  { id: 'profile-reorder-dragover-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, expanded: true, drag: true },
  { id: 'profile-loading-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, switchLoading: true },
  { id: 'four-save-errors-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, saveErrors: true },
  { id: 'cross-server-closed-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, online: true },
  { id: 'cross-server-open-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: true, online: true, crossServerOpen: true },
  { id: 'exit-idle-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: false, exitCount: 2, exitBusy: false },
  { id: 'exit-busy-en-light', language: 'en', theme: 'light', width: 1280, showProfiles: false, exitCount: 2, exitBusy: true },
  { id: 'narrow-profiles-ja-dark', language: 'ja', theme: 'dark', width: 720, showProfiles: true, expanded: true, online: true, crossServerOpen: true },
  { id: 'base-single-ja-dark', language: 'ja', theme: 'dark', width: 1280, showProfiles: false, online: true, pending: 1 },
];

function originalEnvironment(data, t) {
  const profileState = makeProfiles(data.showProfiles);
  const auth = {
    state: { phase: 'active', expiresAt: null, accessRole: 'user' },
    profileLaunchErrors: [],
    clearProfileLaunchErrors() {},
    gameLaunchBusy: false,
    autoLaunchGame: false,
    setAutoLaunchGame() {},
  };
  const profiles = {
    state: profileState,
    selectedProfileId: profileState.selectedProfileId,
    entitlement: { maxProfiles: profileState.maxProfiles },
    busy: false,
    error: data.profileError || '',
    select() {}, create() {}, remove: async () => {}, reorder() {}, updateNote: async () => {}, refresh: async () => {},
  };
  const De = () => ({ language: data.language, setLanguage() {}, t });
  const Ar = () => auth;
  const Br = () => profiles;
  const Vr = tag(sourceFunction(sourceNodes.Vr, { M }), 'original:Vr');
  const Xr = tag(sourceFunction(sourceNodes.Xr, { M }), 'original:Xr');
  const In = tag(sourceFunction(sourceNodes.In, { b: fakeReact, M }), 'original:In');
  const Ir = sourceFunction(sourceNodes.Ir, {});
  const Lr = sourceFunction(sourceNodes.Lr, { Ir });
  const ce = sourceFunction(sourceNodes.ce, {});
  const le = sourceFunction(sourceNodes.le, {});
  const ue = sourceFunction(sourceNodes.ue, {});
  const Fn = tag(sourceFunction(sourceNodes.Fn, { M }), 'original:Fn');
  const Oe = tag(sourceFunction(sourceNodes.Oe, { De, b: fakeReact, M }), 'original:Oe');
  const ai = tag(sourceFunction(sourceNodes.ai, { De, M, ri: transparentPixel, ni: transparentPixel, ii: statusKeys }), 'original:ai');
  const ti = tag(sourceFunction(sourceNodes.ti, { De, b: fakeReact, M }), 'original:ti');
  const si = tag(sourceFunction(sourceNodes.si, { Ar, De, b: fakeReact, M, oi: { phase: 'idle', currentVersion: '', latestVersion: null, progress: null }, ai, ti, xe: originalLanguages }), 'original:si');
  const Yr = tag(sourceFunction(sourceNodes.Yr, { Br, Ar, De, b: fakeReact, M, ue, ce, Jr: connectionKeys, Lr, Vr, le, In }), 'original:Yr');
  const Qr = tag(sourceFunction(sourceNodes.Qr, { De, M, Zr: originalRoutes, Xr }), 'original:Qr');
  const Ji = tag(sourceFunction(sourceNodes.Ji, { M, In, Ki: transparentPixel }), 'original:Ji');
  const qi = tag(sourceFunction(sourceNodes.qi, { De, b: fakeReact, M, Ji }), 'original:qi');
  let storeIndex = 0;
  function me(_key, initial) {
    const error = data.saveErrors ? new Error(`controlled-${storeIndex}`) : null;
    storeIndex += 1;
    return { draft: initial, state: makeStore(error) };
  }
  const Page = ({ routeKey = 'overview' }) => element('div', { className: 'panel shell-page-sentinel', 'data-route': routeKey }, t('nav.overview'));
  const pageStub = (name) => tag((props) => element(Page, { ...props, routeKey: name }), `original:page:${name}`);
  const Gi = tag(sourceFunction(sourceNodes.Gi, {
    De, Ar, Br, b: fakeReact, M, me,
    Si: {}, ui: true, hi: 'afk', ci: () => false, se: () => data.showProfiles,
    Gr: () => data.online ? 'connected' : 'disconnected', fi: () => ({ mapScanState: null }),
    Fn, si, Yr, Qr, Oe, Wi: () => {},
    qr: pageStub('overview'), Li: pageStub('automation'), Bi: pageStub('map-data'), Hi: pageStub('march'), zi: pageStub('hotkeys'), Ri: pageStub('city-layout'), Vi: pageStub('settings'),
    L: null, Wr: () => null,
  }), 'original:Gi');
  return { Gi, qi, profileState, profiles };
}

function setupStateOverrides(data) {
  const runtimeStatus = { pending: data.pending || 0, xluaOnline: !!data.online, config: {} };
  const proxyStatus = { gameRunning: !!data.online };
  const scanState = { serverId: 321, homeServerId: 320, seasonServerIds: [321, 322], truckMatchServerIds: [323, 324], isReading: false };
  const update = data.update || { phase: 'idle', currentVersion: '0.3.17', latestVersion: null, progress: null };
  data.stateOverrides = {
    'original:Gi': { l: runtimeStatus, d: proxyStatus, pe: scanState, Xe: !!data.switchLoading },
    'original:si': { v: update },
    'original:ti': { d: !!data.crossServerOpen },
    'original:Yr': {
      T: !data.expanded,
      g: data.noteModal ? 'profile-1' : '',
      v: data.noteModal ? 'Edited note' : '',
      x: data.drag ? 'profile-1' : '',
      C: data.drag ? 'profile-2' : '',
    },
    'original:qi': { t: data.exitCount ?? null, r: !!data.exitBusy },
    'current:ProfileSidebar': {
      collapsed: !data.expanded,
      noteId: data.noteModal ? 'profile-1' : '',
      noteValue: data.noteModal ? 'Edited note' : '',
      draggedId: data.drag ? 'profile-1' : '',
      dragOverId: data.drag ? 'profile-2' : '',
    },
    'current:AppExitPrompt': { instanceCount: null, busy: false },
  };
  return { runtimeStatus, scanState, update };
}

function currentEnvironment(data, t, state) {
  const commonEnv = { h: element, Fragment, useI18n: () => ({ t, language: data.language, setLanguage() {} }), ...fakeReact };
  const presentation = currentModule(currentFiles.presentation, ['TopVersion', 'ShellConfigSaveErrors'], commonEnv);
  const profileModule = currentModule(currentFiles.profiles, ['ProfileSidebar'], commonEnv);
  const switchModule = currentModule(currentFiles.profileSwitch, ['ProfileSwitchState'], commonEnv);
  const exitModule = currentModule(currentFiles.exit, ['AppExitDialog', 'AppExitPrompt'], { ...commonEnv, warningIcon: transparentPixel });
  const navModule = currentModule(currentFiles.navIcon, ['NavIcon'], commonEnv);
  tag(profileModule.ProfileSidebar, 'current:ProfileSidebar');
  tag(exitModule.AppExitPrompt, 'current:AppExitPrompt');
  const ProfileSidebar = (props) => element(profileModule.ProfileSidebar, { ...props, error: data.profileError || '' });
  const PageForRoute = ({ routeKey }) => element('div', { className: 'panel shell-page-sentinel', 'data-route': routeKey }, t('nav.overview'));
  const RetainedPages = tag(currentFunction(currentFiles.app, 'RetainedPages', { h: element, Fragment, Activity, routes: currentRoutes, PageForRoute }), 'current:RetainedPages');
  const profileState = makeProfiles(data.showProfiles);
  const profilePreviewCallbacks = data.showProfiles ? {
    onSelect: async () => {}, onCreate: async () => {}, onRemove: async () => {}, onReorder: async () => {}, onUpdateNote: async () => {},
  } : {};
  const stores = Array.from({ length: 4 }, (_, index) => makeStore(data.saveErrors ? new Error(`controlled-${index}`) : null));
  const bridgeState = data.online ? 'connected' : 'disconnected';
  const stateText = { connected: t('status.connected'), disconnected: t('status.disconnected') }[bridgeState];
  const scanState = state.scanState;
  const serverJumpBlockedText = data.online ? '' : t('status.gameDisconnected');
  const env = {
    h: element, Fragment,
    GameAssetImageProvider: ({ children }) => children,
    nativeAssetReader: null,
    shellUpdateStatus: state.update,
    online: !!data.online,
    onlineDot: transparentPixel,
    offlineDot: transparentPixel,
    stateText,
    pendingTasks: data.pending || 0,
    t,
    theme: data.theme,
    setTheme() {}, toggleShellTheme() {}, document: {}, window: {}, localStorage: {}, flushSync: (fn) => fn(),
    language: data.language,
    setLanguage() {},
    LANGUAGES: currentLanguages,
    serverJumpRef: { current: null },
    currentServerId: scanState.serverId,
    setServerJumpOpen() {},
    serverJumpOpen: !!data.crossServerOpen,
    serverTarget: '',
    setServerTarget() {},
    serverJumpBusyActive: false,
    serverJumpBlockedText,
    jumpServer: async () => {},
    serverJumpBusy: 0,
    mapRuntime: scanState,
    serverJumpError: '',
    serverHistory: [],
    backendBridge: { available: false },
    refreshStatus() {},
    showProfiles: !!data.showProfiles,
    ProfileSidebar,
    shellProfiles: profileState,
    focusGameOnProfileSelect: true,
    profilePreviewCallbacks,
    routes: currentRoutes,
    activeRoute: 'overview',
    preloadRoute() {},
    selectRoute() {},
    NavIcon: navModule.NavIcon,
    Suspense,
    ShellConfigSaveErrors: presentation.ShellConfigSaveErrors,
    shellFlagStates: stores,
    previewFlagStates: [],
    autoWeekendShieldStore: stores[0], autoAttackShieldStore: stores[1], autoReconnectStore: stores[2], autoClosePopupStore: stores[3],
    switchLoading: !!data.switchLoading,
    ProfileSwitchState: switchModule.ProfileSwitchState,
    RetainedPages,
    selectedProfileId: profileState.selectedProfileId,
    visitedRoutes: new Set(['overview']),
    pageProps: {}, pagePropsByRoute: undefined,
    AppExitPrompt: exitModule.AppExitPrompt,
    subscribeCloseRequests: null, confirmExit: null,
    previewExitCount: data.exitCount ?? null,
    AppExitDialog: exitModule.AppExitDialog,
    previewState: data.exitBusy ? 'shell-exit-busy' : 'shell-exit',
    TopVersion: presentation.TopVersion,
  };
  return env;
}

fs.mkdirSync(path.join(here, 'raw'), { recursive: true });
const results = [];
for (const descriptor of cases) {
  const data = structuredClone(descriptor);
  const state = setupStateOverrides(data);
  activeCase = data;
  const originalT = tFor(originalCatalogs[data.language].messages);
  const currentT = tFor(currentCatalogs[data.language]);
  const original = originalEnvironment(data, originalT);
  const originalTree = element(Fragment, null, element(original.Gi, { initialTheme: data.theme }), element(original.qi, {}));
  const originalHtml = renderToStaticMarkup(materialize(originalTree));
  const currentTree = renderCurrentReturn(currentEnvironment(data, currentT, state));
  const currentHtml = renderToStaticMarkup(materialize(currentTree));
  const originalFile = `raw/${data.id}-original.html`;
  const currentFile = `raw/${data.id}-current.html`;
  persist(path.join(here, originalFile), originalHtml);
  persist(path.join(here, currentFile), currentHtml);
  const originalAside = /<aside class="profile-sidebar"><section class="profile-list/.test(originalHtml);
  const currentAside = /<aside class="profile-sidebar"><section class="profile-list/.test(currentHtml);
  const originalExitSibling = data.exitCount == null || originalHtml.indexOf('</main>') < originalHtml.indexOf('app-exit-backdrop');
  const currentExitSibling = data.exitCount == null || currentHtml.indexOf('</main>') < currentHtml.indexOf('app-exit-backdrop');
  results.push({
    ...descriptor,
    originalHtml: originalFile,
    currentHtml: currentFile,
    originalSha256: hash(originalHtml),
    currentSha256: hash(currentHtml),
    structuralChecks: {
      profileSidebarWrapper: data.showProfiles ? { original: originalAside, current: currentAside } : null,
      exitDialogSiblingAfterShell: data.exitCount == null ? null : { original: originalExitSibling, current: currentExitSibling },
      originalSaveErrorCount: (originalHtml.match(/class="automation-error"/g) || []).length,
      currentSaveErrorCount: (currentHtml.match(/class="automation-error"/g) || []).length,
      originalProfileLoading: originalHtml.includes('profile-switch-loading'),
      currentProfileLoading: currentHtml.includes('profile-switch-loading'),
      originalCrossServerOpen: originalHtml.includes('server-jump-popover'),
      currentCrossServerOpen: currentHtml.includes('server-jump-popover'),
    },
  });
}
activeCase = null;

const locators = Object.fromEntries(Object.entries(sourceNodes).map(([name, node]) => [name, locateNode(node)]));
const sourceLocators = {
  asset: path.relative(repo, originalAssetFile).replaceAll('\\', '/'),
  assetSha256: hash(originalAssetBytes),
  functions: locators,
  rootMount: {
    byteOffset: 377193,
    observation: 'Recovered root renders Gi and qi as siblings inside the provider tree.',
    excerptSha256: hash(originalAssetBytes.subarray(377193, Math.min(originalAssetBytes.length, 377193 + 400))),
  },
};
json('source-locators.json', sourceLocators);
json('renderer-inputs.json', {
  cases: results,
  substitutions: [
    'Original and current route pages execute through their shell route wrappers but render the same inert panel sentinel; page-body parity is outside this shell packet.',
    'Original service/auth/profile providers are disclosed local fixtures. Current profile callbacks are inert preview-style callbacks; no native/gameplay/network/updater action is invoked.',
    'Image URL imports are replaced by the same inert data URL on both sides so this packet measures shell composition rather than packaged asset URL rewriting.',
    'React effects/layout effects are not executed during source rendering. Browser capture explicitly opens rendered dialogs to reproduce recovered showModal presentation.',
    'The current forced-error fixture injects the controlled `error` prop through a one-line wrapper around the real production ProfileSidebar because App has no live external profile-error provider in this offline packet; ProfileSidebar itself renders the compared DOM.',
    'The cached-return fixture represents the source post-cache composition after the uncached loading interval: showProfiles remains true and switchLoading is false. No synthetic loading DOM is introduced.',
    'Account/grace/upgrade UI remains present on the recovered side and absent on current by product scope. Current updater/Refresh Status/profile run controls retain provider-availability disabled fences.',
  ],
});

if (!verifyOnly) {
  const dependencyFiles = [
    originalAssetFile,
    path.join(assets, 'index-rIL9Fpht.css'),
    ...Object.values(currentFiles),
    originalCatalogs.en.file,
    originalCatalogs.ja.file,
    fileURLToPath(import.meta.url),
  ];
  json('dependencies.json', {
    files: dependencyFiles.map((file) => ({ path: path.relative(repo, file).replaceAll('\\', '/'), sha256: hash(fs.readFileSync(file)) })),
    tools: {
      node: process.version,
      react: requireUi('react/package.json').version,
      esbuild: requireUi('esbuild/package.json').version,
      babelParser: requireUi('@babel/parser/package.json').version,
    },
    currentAppReturn: { byteLength: Buffer.byteLength(appReturnSource), sha256: hash(appReturnSource) },
  });
}

console.log(JSON.stringify({ result: 'SHELL_SOURCE_RENDER_OK', cases: results.length, verifyOnly, originalGi: locators.Gi, currentAppSha256: hash(appSource) }));
