// LWB317-UI-MAP-INTERACTIONS-001 / Scheduled Plunder presentation differential.
//
// Extracts the ACTUAL original `ot` (Dispatch/Ghost group), `st` (Truck group) and their helper dependencies from the
// original MapDataPanel asset bytes (AST, not a re-implementation), executes them with real react + react-dom/server,
// and diffs the static markup / interactive skeleton against the canonical ScheduledPlunder.jsx. Also: negative
// (mutation) proof, old-surface baseline, fence checks, fixture coverage, and locale key presence.
//
//   node check-scheduled-plunder.mjs [--record] [--verify-record]
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../..');
const uiRoot = path.join(repo, 'src/LWBridge.UI-0.3.17');
const srcDir = path.join(uiRoot, 'src');
const requireUi = createRequire(path.join(uiRoot, 'package.json'));
const { parse } = requireUi('@babel/parser');
const esbuild = requireUi('esbuild');
const React = requireUi('react');
const jsxRuntime = requireUi('react/jsx-runtime');
const { renderToStaticMarkup } = requireUi('react-dom/server');

const sha = value => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const read = file => fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
const assets = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets';
const panelPath = `${assets}/MapDataPanel-B4GXEND2.js`, mainPath = `${assets}/index-BVfnK1wp.js`;
const rewardPath = `${assets}/rewardDisplay-eZWrd6iS.js`, assetImagePath = `${assets}/GameAssetImage-Diy9VTIr.js`;
const prettyPath = 'evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/reference/MapDataPanel.pretty.js';
const panel = fs.readFileSync(path.join(repo, panelPath), 'utf8');
const main = fs.readFileSync(path.join(repo, mainPath), 'utf8');
const rewardsText = fs.readFileSync(path.join(repo, rewardPath), 'utf8');
const assetImageText = fs.readFileSync(path.join(repo, assetImagePath), 'utf8');
const pretty = fs.readFileSync(path.join(repo, prettyPath), 'utf8');
assert.equal(sha(panel), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
assert.equal(sha(main), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
const utf8Offset = (text, index) => Buffer.byteLength(text.slice(0, index));
const locator = (text, node) => ({ utf8ByteOffset: utf8Offset(text, node.start), length: Buffer.byteLength(text.slice(node.start, node.end)) });
const prettyLine = needle => { const index = pretty.indexOf(needle); assert.ok(index >= 0, needle); return pretty.slice(0, index).split('\n').length; };

// ---------------------------------------------------------------- AST extraction of the ORIGINAL code
function walk(node, output = []) {
  if (!node || typeof node !== 'object') return output;
  if (node.type) output.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(child => walk(child, output));
    else if (value && typeof value === 'object') walk(value, output);
  }
  return output;
}
const panelAst = parse(panel, { sourceType: 'module' }), mainAst = parse(main, { sourceType: 'module' }), rewardAst = parse(rewardsText, { sourceType: 'module' });
const topLevel = ast => ast.program.body.map(n => n.declaration || n);
function fnNode(ast, name) { const node = topLevel(ast).find(n => n.type === 'FunctionDeclaration' && n.id.name === name); assert.ok(node, `function ${name}`); return node; }
function varNode(ast, name) {
  const node = topLevel(ast).filter(n => n.type === 'VariableDeclaration').flatMap(n => n.declarations).find(n => n.id.name === name);
  assert.ok(node, `var ${name}`); return node;
}
const panelFunctions = ['x', 'Ce', 'we', 'S', 'C', 'k', 'j', 'M', 'P', 'F', 'N', 'I', 'L', 'Je', 'ot', 'st'];
const fnNodes = Object.fromEntries(panelFunctions.map(name => [name, fnNode(panelAst, name)]));
const varNodes = Object.fromEntries(['b', 'Te'].map(name => [name, varNode(panelAst, name)]));
const truckNodes = Object.fromEntries(['Fe', 'Ie'].map(name => [name, fnNode(mainAst, name)]));
const rewardNode = fnNode(rewardAst, 'e');
// MapDataPanel imports `St as l` / `Ct as r` from the main asset; confirm these resolve to Fe / Ie, and `t as _` is rewardDisplay's e.
assert.ok(panel.startsWith('import{A as e,B as t,C as n,Ct as r,Et as i,It as a,Mt as o,Q as s,S as c,St as l,Tt as u,'));
const mainExports = main.match(/export\{([^}]*)\}/)[1].split(',').map(part => part.trim().split(' as '));
const exportOf = alias => mainExports.find(([, exported]) => exported === alias)?.[0];
assert.equal(exportOf('St'), 'Fe'); assert.equal(exportOf('Ct'), 'Ie'); assert.equal(exportOf('Tt'), 'De');
assert.ok(panel.includes('import{t as _}from"./rewardDisplay-eZWrd6iS.js";'));
// How the original main component feeds the groups (asserted textually; minified names ln = dispatchJobs, dn = truckJobs).
for (const snippet of ['(0,E.jsx)(ot,{jobs:ln.filter(e=>e.taskKind!==`ghost`),gameTexts:Qt,currentTime:rn,online:l,busyKey:pn,onCancel:hr,onClear:mr})',
  '(0,E.jsx)(ot,{kind:`ghost`,jobs:ln.filter(e=>e.taskKind===`ghost`),gameTexts:Qt,currentTime:rn,online:l,busyKey:pn,onCancel:hr,onClear:mr})',
  '(0,E.jsx)(st,{jobs:dn,gameTexts:Qt,currentTime:rn,online:l,busyKey:pn,onCancel:gr,onPlunderAgain:_r,onClear:()=>mr(`truck`)})']) assert.ok(panel.includes(snippet), snippet);
assert.equal(panel.split('ln.length+dn.length').length - 1, 2);
const slice = (text, node) => text.slice(node.start, node.end);
const originalSource = [...panelFunctions.map(name => slice(panel, fnNodes[name])), ...Object.values(truckNodes).map(node => slice(main, node)), slice(rewardsText, rewardNode).replace('function e(', 'function _(')].join('\n')
  + '\nconst l=Fe,r=Ie;\nvar ' + slice(panel, varNodes.b) + ';\nvar ' + slice(panel, varNodes.Te) + ';\n';
const clock = { value: 1_799_000_000_000 };
const RealDate = globalThis.Date;
class FixedDate extends RealDate { constructor(...args) { super(...(args.length ? args : [clock.value])); } static now() { return clock.value; } }
const i18nContext = { current: null };
const placeholder = ({ className }) => React.createElement('span', { className: `${className} game-asset-placeholder`, 'aria-hidden': 'true' });
const original = new Function('Date', 'u', 'E', 'v', 'return (function(){' + originalSource + ';return {ot,st,we,C,F,P,N,Je,Fe,Ie,b,Te};})();')(FixedDate, () => i18nContext.current, jsxRuntime, placeholder);

// Real GameAssetImage (original `v`) executed from the asset bytes, to measure the placeholder normalisation exactly.
assert.ok(assetImageText.startsWith('import{Et as e,It as t,L as n,Mt as r}from"./index-BVfnK1wp.js";'));
const realAssetImageCode = assetImageText.replace(/^import\{[^}]*\}from"\.\/index-BVfnK1wp\.js";/, 'const e=()=>__jsx,t=m=>m,r=()=>__react,n=()=>{throw new Error("native asset loader is not available offline")};').replace(/export\{x as t\};?\s*$/, 'return x;');
assert.ok(realAssetImageCode.endsWith('return x;'));
const RealGameAssetImage = new Function('__jsx', '__react', realAssetImageCode)(jsxRuntime, React);
const withRealImage = new Function('Date', 'u', 'E', 'v', 'return (function(){' + originalSource + ';return {ot,st};})();')(FixedDate, () => i18nContext.current, jsxRuntime, RealGameAssetImage);

// Statically-visible key set of the original groups / translators (string literals + table-driven error codes).
const keyLiterals = new Set();
for (const name of ['ot', 'st', 'we', 'C']) for (const node of walk(fnNodes[name])) {
  if (node.type === 'StringLiteral' || node.type === 'TemplateElement') { const value = node.type === 'StringLiteral' ? node.value : node.value.cooked; if (/^(map|common)\.[A-Za-z]+$/.test(value) || value === 'error.PLUNDER_POSSIBLE_DAILY_LIMIT_REACHED') keyLiterals.add(value); }
}
const dispatchCodes = new Set([...original.b.map(([, code]) => code), 'DISPATCH_PLUNDER_SERVER_REJECTED', 'DISPATCH_PLUNDER_UNKNOWN']);
const truckCodes = new Set([...Object.values(original.Te), 'TRUCK_PLUNDER_SERVER_REJECTED']);
const originalStaticKeys = new Set([...keyLiterals, ...[...dispatchCodes, ...truckCodes].map(code => `error.${code}`)]);
// Fields the original reads from job/reward rows: member names inside ot/st/F/M/Fe/Ie plus literal args of k(row, "field").
const readFields = new Set();
for (const node of [fnNodes.ot, fnNodes.st, fnNodes.F, fnNodes.M, fnNodes.j, truckNodes.Fe, truckNodes.Ie]) for (const inner of walk(node)) {
  if ((inner.type === 'MemberExpression' || inner.type === 'OptionalMemberExpression') && !inner.computed && inner.property.type === 'Identifier') readFields.add(inner.property.name);
  if (inner.type === 'CallExpression' && inner.callee.type === 'Identifier' && inner.callee.name === 'k' && inner.arguments[1]?.type === 'TemplateLiteral') readFields.add(inner.arguments[1].quasis[0].value.cooked);
}

// `taskKind` is read by the original MAIN component's group filters (asserted textually above), not by ot/st.
readFields.add('taskKind');

// ---------------------------------------------------------------- locales
const LANGUAGES = ['en', 'zh-CN', 'zh-TW', 'ja', 'ko', 'vi', 'id', 'ru', 'pt'];
const catalogs = {};
for (const language of LANGUAGES) catalogs[language] = (await import(pathToFileURL(path.join(srcDir, `locales/${language}.js`)))).default;
function makeT(language, record) {
  const catalog = catalogs[language];
  return (key, values = {}) => { record?.add(key); return (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match)); };
}

// ---------------------------------------------------------------- canonical build (in memory; optional source mutations)
const canonicalFiles = ['ScheduledPlunder.jsx', 'mapPlunderPresentation.js', 'mapTablePresentation.js'];
const canonicalSources = () => Object.fromEntries(canonicalFiles.map(file => [file, read(path.join(srcDir, file))]));
assert.match(read(path.join(srcDir, 'i18n.jsx')), /export function useI18n\(\)/);
const i18nStub = 'export function useI18n() { return globalThis.__LWB317_SCHEDULED_I18N__.current; }\n';
globalThis.__LWB317_SCHEDULED_I18N__ = i18nContext;
async function buildCanonical(sources) {
  const virtual = { name: 'virtual-src', setup(build) {
    build.onResolve({ filter: /^\.\// }, args => ({ path: args.path.slice(2), namespace: 'virt' }));
    build.onLoad({ filter: /.*/, namespace: 'virt' }, args => {
      if (args.path === 'i18n.jsx') return { contents: i18nStub, loader: 'jsx' };
      assert.ok(sources[args.path] !== undefined, `unexpected import ${args.path}`);
      return { contents: sources[args.path], loader: args.path.endsWith('.jsx') ? 'jsx' : 'js' };
    });
  } };
  const result = await esbuild.build({ stdin: { contents: 'import * as S from "./ScheduledPlunder.jsx"; import * as P from "./mapPlunderPresentation.js"; import * as T from "./mapTablePresentation.js"; export { S, P, T };', resolveDir: srcDir, sourcefile: 'entry.js', loader: 'js' },
    bundle: true, format: 'cjs', platform: 'node', write: false, jsx: 'automatic', external: ['react', 'react/jsx-runtime'], plugins: [virtual], logLevel: 'silent' });
  const module = { exports: {} };
  new Function('require', 'module', 'exports', result.outputFiles[0].text)(requireUi, module, module.exports);
  return module.exports;
}

// ---------------------------------------------------------------- shared test inputs
const NOW = 1_799_000_000_000;
const fixtures = await import(pathToFileURL(path.join(srcDir, 'mapPlunderFixtures.js')));
const fixtureStates = ['map-scheduled', 'map-scheduled-populated', 'map-scheduled-conditional'];
const gameTexts = { ...fixtures.plunderFixtureGameTexts, 12345: 'Synthetic mapped numeric text', E000000: 'Synthetic text that must never show for E000000' };
const startedAt = performance.now();
const noop = () => {};
const counters = {};
const bump = name => { counters[name] = (counters[name] || 0) + 1; };
const recordedOriginal = new Set(), recordedCanonical = new Set();
const reactWarnings = [];
const realConsoleError = console.error;
console.error = (...args) => { reactWarnings.push(args.map(String).join(' ').slice(0, 200)); };

function originalElement(args, impl = original) {
  const { dispatchJobs, truckJobs, gameTexts: texts = gameTexts, currentTime, online, busyKey, spy } = args;
  const h = React.createElement;
  return h(React.Fragment, null,
    h(impl.ot, { jobs: dispatchJobs.filter(e => e.taskKind !== 'ghost'), gameTexts: texts, currentTime, online, busyKey, onCancel: spy?.cancelDispatch || noop, onClear: spy?.clear || noop }),
    h(impl.ot, { kind: 'ghost', jobs: dispatchJobs.filter(e => e.taskKind === 'ghost'), gameTexts: texts, currentTime, online, busyKey, onCancel: spy?.cancelDispatch || noop, onClear: spy?.clear || noop }),
    h(impl.st, { jobs: truckJobs, gameTexts: texts, currentTime, online, busyKey, onCancel: spy?.cancelTruck || noop, onPlunderAgain: spy?.again || noop, onClear: () => (spy?.clear || noop)('truck') }));
}
function canonicalElement(canon, args, actionsEnabled) {
  const { dispatchJobs, truckJobs, gameTexts: texts = gameTexts, currentTime, online, busyKey, spy } = args;
  return React.createElement(canon.S.ScheduledPlunder, { dispatchJobs, truckJobs, gameTexts: texts, currentTime, online, busyKey, actionsEnabled,
    onCancelDispatch: spy?.cancelDispatch || noop, onCancelTruck: spy?.cancelTruck || noop, onPlunderAgain: spy?.again || noop, onClear: spy?.clear || noop });
}
function renderOriginal(args, language, impl = original) {
  i18nContext.current = { language, t: makeT(language, recordedOriginal) };
  return renderToStaticMarkup(originalElement(args, impl));
}
function renderCanonical(canon, args, language, actionsEnabled) {
  i18nContext.current = { language, t: makeT(language, recordedCanonical) };
  return renderToStaticMarkup(canonicalElement(canon, args, actionsEnabled));
}
class Mismatch extends Error {}
function firstDifference(a, b) { let i = 0; while (i < a.length && a[i] === b[i]) i++; return `@${i}: original ...${JSON.stringify(a.slice(Math.max(0, i - 60), i + 80))} canonical ...${JSON.stringify(b.slice(Math.max(0, i - 60), i + 80))}`; }
const stripDisabled = markup => markup.replaceAll(' disabled=""', '');
function compare(canon, label, args, language, state) {
  const expected = renderOriginal(args, language);
  const enabled = renderCanonical(canon, args, language, true);
  if (enabled !== expected) throw new Mismatch(`${label} [${language}] actionsEnabled=true markup differs ${firstDifference(expected, enabled)}`);
  const fenced = renderCanonical(canon, args, language, false);
  if (/<button(?![^>]*\sdisabled="")[^>]*>/.test(fenced)) throw new Mismatch(`${label} [${language}] actionsEnabled=false left an enabled button`);
  if (stripDisabled(fenced) !== stripDisabled(expected)) throw new Mismatch(`${label} [${language}] actionsEnabled=false differs beyond disabled ${firstDifference(stripDisabled(expected), stripDisabled(fenced))}`);
  const baseButtons = (expected.match(/<button/g) || []).length;
  if ((fenced.match(/<button/g) || []).length !== baseButtons) throw new Mismatch(`${label} button count`);
  bump(state); bump('renders');
}

// Inputs --------------------------------------------------------------------------------------------------------
function fixtureArgs(state, now = NOW) { const fixture = fixtures.plunderFixtureFor(state, now); return { ...fixture, gameTexts: fixtures.plunderFixtureGameTexts, currentTime: now }; }
function boundaryTimes(jobs, now) {
  const times = new Set([now, now - 1, now + 1]);
  for (const job of jobs) {
    for (const key of ['taskExpireTime', 'completionTime', 'plunderAt']) { const stamp = original.P(job[key]); if (stamp) for (const delta of [-1, 0, 1]) times.add(stamp + delta); }
    for (const key of ['arriveTs', 'protectTime']) { const value = Number(job[key]); if (Number.isFinite(value) && value > 0) for (const delta of [-1, 0, 1]) times.add(value + delta); }
    const protect = Number(job.protectTime);
    if (protect > 0) for (const offset of [-999, -1000, -1001, -2000, -59_999, -60_000, -61_000, -3_723_000]) times.add(protect + offset);
  }
  return [...times].filter(Number.isFinite).sort((left, right) => left - right);
}
const SECRET_ERRORS = [undefined, '', 0, 'E000000', ' E000000 ', 'dispatch_des041', 'ghostrecon_901', '12345', 'DISPATCH_PLUNDER_DAILY_LIMIT_REACHED', 'DISPATCH_PLUNDER_SEND_FAILED: synthetic detail', 'DISPATCH_PLUNDER_FOO',
  'DISPATCH_PLUNDER_SERVER_REJECTED: 777', 'DISPATCH_PLUNDER_SERVER_REJECTED', 'DISPATCH_PLUNDER_UNKNOWN - detail', 'DISPATCH_PLUNDER_X：中文 detail', 'DISPATCH_PLUNDER_ALREADY_ARMED E000000', 'game disconnected', 'Dispatch_Des043 extra',
  'invalid scheduled target', '   ', 'Task   Expired\n now', 'client restarted', 'invalid map plunder schedule', 'map plunder schedule already armed', '457567', '  99  ', '0',
  'synthetic unrecognised server text that is intentionally much longer than sixty characters in total', 'x'.repeat(61), 12345,
  { code: 'DISPATCH_PLUNDER_ALREADY_ARMED', message: 'synthetic' }, { message: 'only message' }, { code: 7 }, new Error('server response timeout'), new Error(''),
  ...original.b.map(([needle]) => `Synthetic ${needle.toUpperCase()} trailing`)];
const TRUCK_ERRORS = [undefined, '', 0, 'E000000', ' E000000 ', '457567', '457589', '458632', 'season_mastery_s3_tips_12', 'truck_tips10008', 'trade_person_tips1013', 'Truck Expired', 'game disconnected', 'Client   Restarted',
  'Server Response Timeout', 'constructor', '__proto__', 'hasOwnProperty', '999999', '  ', 'synthetic   unrecognised\ttruck text', 'dispatch_des041', 12345,
  { code: 'trade_person_tips1013', message: 'x' }, { message: 'truck expired' }, new Error('game disconnected'), new Error(''), ...Object.keys(original.Te)];
const STATUSES = ['scheduled', 'waiting_connection', 'running', 'succeeded', 'failed', 'cancelled', 'expired', 'synthetic-other', undefined];
function secretTimings(now) {
  const ms = offset => now + offset, sec = offset => Math.floor((now + offset) / 1000);
  return { pending: { completionTime: ms(1000), plunderAt: ms(2000), taskExpireTime: ms(100000) }, protectedMs: { completionTime: ms(-1000), plunderAt: ms(2000), taskExpireTime: ms(100000) },
    protectedSec: { completionTime: sec(-10000), plunderAt: sec(20000), taskExpireTime: sec(100000) }, ready: { completionTime: ms(-2000), plunderAt: ms(-1000) },
    full: { completionTime: ms(-2000), plunderAt: ms(-1000), stolenCount: 3, maxStealCount: 3 }, expired: { completionTime: ms(-2000), plunderAt: ms(-1000), taskExpireTime: ms(0) },
    noPlunderAt: { completionTime: ms(-2000), plunderAt: 0 }, noTimes: {}, junk: { completionTime: 'x', plunderAt: -1, taskExpireTime: null },
    secondsBelowThreshold: { completionTime: 999_999_999_999, plunderAt: 999_999_999_999 }, msAtThreshold: { completionTime: 1_000_000_000_000, plunderAt: 1_000_000_000_000 } };
}
function truckVariants(now) {
  const base = { serverId: 9002, uuid: '900200001', jobId: 'synthetic-j', scheduledAt: now - 1000, ownerName: 'Synthetic escort', quality: 3, maxLootCount: 3, robTimes: 0, protectTime: now - 1000, arriveTs: now + 100000 };
  return { ready: base, protectedSoon: { ...base, protectTime: now + 61000, robTimes: 1 }, protectedExact: { ...base, protectTime: now + 1000 }, full: { ...base, robTimes: 3 }, expired: { ...base, arriveTs: now }, invalidUuid: { ...base, uuid: '  ' },
    invalidServer: { ...base, serverId: 1.5 }, reindeer: { ...base, isSpecialURQuality: true, maxLootCount: 9, robTimes: 1, quality: 6 }, fallbacks: { ...base, ownerName: '', allianceName: 'Synthetic alliance' }, bare: { serverId: 9002, uuid: 'u' },
    junkCounts: { ...base, robTimes: 'abc', maxLootCount: 'xyz', quality: null, protectTime: 'nope', arriveTs: -5 } };
}
const rewardVariants = [undefined, null, [], [{ key: 'a', name: 'Synthetic A', count: 1 }], [{ key: 'a', name: 'Synthetic A', nameKey: 'synthetic-reward-key', count: 999 }, { key: 'b', name: 'Synthetic B', nameKey: 'missing-key', count: 1000 }, { key: 'c', count: 1250 }, { key: 'd', name: 'Synthetic D', iconPath: 'synthetic/icon.png', count: 999999 }],
  [{ key: 'e', name: 'Synthetic E', count: 1e6 }, { key: 'f', name: 'Synthetic F', count: 1e9 }, { key: 'g', name: 'Synthetic G', count: -1500 }, { key: 'h', name: 'Synthetic H', count: NaN }, { key: 'i', name: 'Synthetic I' }, { key: 'j', name: 'Synthetic J', count: '12' }, { key: 'k', name: 'Synthetic K', count: 1234567.891 }, { key: 'l', nameKey: 'synthetic-reward-key', count: 0 }]];

// ---------------------------------------------------------------- the suite (shared by the real run and every mutation)
async function runSuite(canon, { exhaustive }) {
  const failures = [];
  const stage = name => { if (process.env.LWB317_STAGES && exhaustive) process.stderr.write(`[stage] ${name} t=${Math.round(performance.now() - startedAt)}ms renders=${counters.renders || 0}\n`); };
  const guard = (fn) => { try { fn(); } catch (error) { if (!(error instanceof Mismatch) && !(error instanceof assert.AssertionError)) throw error; failures.push(error.message.slice(0, 600)); if (!exhaustive) throw error; } };
  try {
    stage('start');
    // 1. fixtures: every locale x online x busyKey at the fixture clock
    for (const state of fixtureStates) {
      const args = fixtureArgs(state);
      const keys = ['', args.busyKey, 'clear:dispatch', 'clear:ghost', 'clear:truck', 'schedule', 'schedule-truck', ...args.dispatchJobs.slice(0, 3).map(job => `${job.serverId}:${job.uuid}`), ...args.truckJobs.slice(0, 4).map(job => `truck:${job.serverId}:${job.uuid}`)];
      for (const language of LANGUAGES) for (const online of [true, false]) for (const busyKey of new Set(keys)) guard(() => compare(canon, `fixture ${state} online=${online} busy=${JSON.stringify(busyKey)}`, { ...args, online, busyKey }, language, 'fixtureRenders'));
      // every row's own Cancel / Plunder Again busy key
      const own = [...args.dispatchJobs.map(job => `${job.serverId}:${job.uuid}`), ...args.truckJobs.map(job => `truck:${job.serverId}:${job.uuid}`)];
      for (const online of [true, false]) for (const busyKey of new Set(own)) guard(() => compare(canon, `fixture ${state} own busy`, { ...args, online, busyKey }, 'en', 'busyKeyRenders'));
      // time sweep around every boundary (expire / completion / plunderAt / arrival / protect countdown)
      const times = boundaryTimes([...args.dispatchJobs, ...args.truckJobs], NOW);
      for (const language of ['en']) for (const online of [true, false]) for (const currentTime of times) guard(() => compare(canon, `fixture ${state} time=${currentTime - NOW}`, { ...args, online, busyKey: '', currentTime }, language, 'timeSweepRenders'));
      counters.timeBoundaries = (counters.timeBoundaries || 0) + times.length;
      // different `now` (fixtures are a pure function of now)
      for (const shift of [-86_400_000, 0, 123_456_789]) { const shifted = fixtureArgs(state, NOW + shift); for (const online of [true, false]) guard(() => compare(canon, `fixture ${state} shifted`, { ...shifted, online }, 'ja', 'shiftedRenders')); }
    }
    stage('fixtures');
    // 2. dispatch/ghost matrix: scheduleStatus x lastError x timing x online x kind
    const timings = secretTimings(NOW);
    const secretBase = { serverId: 9001, uuid: '900100001', ownerName: 'Synthetic owner', quality: 4, stolenCount: 0, maxStealCount: 3 };
    for (const language of LANGUAGES) {
      const wide = language === 'en';
      for (const status of STATUSES) for (const lastError of status === 'failed' ? SECRET_ERRORS : [undefined, 'E000000', 'dispatch_des041']) for (const [timingName, timing] of wide && status !== 'failed' ? Object.entries(timings) : [['ready', timings.ready], ['pending', timings.pending]]) for (const online of [true, false]) for (const taskKind of wide ? ['dispatch', 'ghost', undefined] : ['ghost']) {
        const row = { ...secretBase, taskKind, scheduleStatus: status, lastError, ...timing };
        guard(() => compare(canon, `secret ${status}/${String(lastError && lastError.message || lastError)}/${timingName}/${taskKind}`, { dispatchJobs: [row], truckJobs: [], gameTexts, currentTime: NOW, online, busyKey: '' }, language, 'secretMatrixRenders'));
      }
    }
    stage('secret matrix');
    for (const [name, rewards] of rewardVariants.entries()) for (const language of LANGUAGES) for (const isSpecial of [undefined, true, 1, 'yes', false, 0]) for (const owner of [{ ownerName: 'Synthetic' }, { ownerName: 'Synthetic', ownerUid: 'uid-1' }, { ownerName: '', ownerUid: 'uid-1' }, { ownerName: '', ownerUid: '' }, {}]) {
      const row = { ...secretBase, ...owner, scheduleStatus: 'succeeded', isSpecial, rewards, quality: [0, 1, 2, 3, 4, 5, 9, undefined][name] };
      guard(() => compare(canon, `secret rewards#${name}`, { dispatchJobs: [row], truckJobs: [], gameTexts, currentTime: NOW, online: true, busyKey: '' }, language, 'secretRewardRenders'));
    }
    stage('secret rewards');
    // 3. truck matrix
    const variants = truckVariants(NOW);
    for (const language of LANGUAGES) {
      const wide = language === 'en';
      for (const [variantName, variant] of wide ? Object.entries(variants) : [['protectedSoon', variants.protectedSoon], ['ready', variants.ready]]) for (const online of [true, false]) {
        for (const status of STATUSES) for (const lastError of status === 'failed' || status === 'expired' ? (wide ? TRUCK_ERRORS : [undefined, 'E000000', '457567']) : [undefined, 'E000000']) for (const battleWon of status === 'succeeded' ? [true, false, undefined, 'yes', null] : [undefined]) {
          const row = { ...variant, scheduleStatus: status, lastError, battleWon };
          guard(() => compare(canon, `truck ${variantName} ${status}/${String(lastError && lastError.message || lastError)}/${battleWon}`, { dispatchJobs: [], truckJobs: [row], gameTexts, currentTime: NOW, online, busyKey: '' }, language, 'truckMatrixRenders'));
        }
      }
    }
    for (const [name, rewards] of rewardVariants.entries()) for (const language of LANGUAGES) for (const quality of [undefined, 1, 4, 7]) {
      const row = { ...variants.ready, scheduleStatus: 'succeeded', battleWon: true, plunderRewards: rewards, quality, rewards: [{ key: 'ignored', name: 'Must not show', count: 1 }] };
      guard(() => compare(canon, `truck rewards#${name}`, { dispatchJobs: [], truckJobs: [row], gameTexts, currentTime: NOW, online: true, busyKey: '' }, language, 'truckRewardRenders'));
    }
    stage('truck matrix+rewards');
    // 4. duplicate in-flight (`ee2`) matrix: a succeeded row plus every other status for the same serverId:uuid / a different uuid / a numeric-vs-string id
    for (const other of STATUSES) for (const online of [true, false]) for (const sameKey of ['same', 'otherUuid', 'otherServer', 'numericUuid']) {
      const first = { ...variants.ready, scheduleStatus: 'succeeded', battleWon: true, jobId: 'first', serverId: 5, uuid: '77' };
      const second = { ...variants.ready, scheduleStatus: other, jobId: 'second', serverId: sameKey === 'otherServer' ? 6 : 5, uuid: sameKey === 'otherUuid' ? '78' : sameKey === 'numericUuid' ? 77 : '77' };
      for (const order of [[first, second], [second, first]]) guard(() => compare(canon, `dedupe ${other}/${sameKey}`, { dispatchJobs: [], truckJobs: order, gameTexts, currentTime: NOW, online, busyKey: '' }, 'en', 'dedupeRenders'));
    }
    // 5. group filters / counts
    for (const kinds of [[], ['dispatch'], ['ghost'], ['ghost', 'dispatch', undefined, 'other', 'ghost']]) {
      const jobs = kinds.map((taskKind, index) => ({ ...secretBase, uuid: String(900100100 + index), taskKind, scheduleStatus: 'succeeded' }));
      guard(() => compare(canon, `filters ${kinds}`, { dispatchJobs: jobs, truckJobs: [], gameTexts, currentTime: NOW, online: true, busyKey: '' }, 'en', 'filterRenders'));
      for (const trucks of [0, 2]) guard(() => assert.equal(canon.P.scheduledPlunderCount(jobs, Array(trucks).fill({})), jobs.length + trucks, 'tab count'));
      counters.countChecks = (counters.countChecks || 0) + 2;
    }
    stage('dedupe+filters');
    // 6. error translators vs the original functions (all locales, direct)
    for (const language of LANGUAGES) {
      const t = makeT(language);
      for (const error of SECRET_ERRORS.filter(value => value !== undefined)) guard(() => { assert.equal(canon.P.translateDispatchPlunderError(t, error), original.we(t, error), `dispatch error ${String(error)}`); bump('dispatchErrorCases'); });
      for (const error of TRUCK_ERRORS.filter(value => value !== undefined)) guard(() => { assert.equal(canon.P.translateTruckPlunderError(t, error), original.C(t, error), `truck error ${String(error)}`); bump('truckErrorCases'); });
    }
    stage('translators');
    // 7. interactive skeleton (button props / handler wiring / exact row objects) vs the original
    for (const state of fixtureStates.slice(1)) for (const online of [true, false]) for (const busyKey of ['', fixtureArgs(state).busyKey, 'truck:9002:900200005']) guard(() => compareInteractions(canon, { ...fixtureArgs(state), online, busyKey }));
  } catch (error) { if (exhaustive || !(error instanceof Mismatch || error instanceof assert.AssertionError)) throw error; }
  return failures;
}

// Interaction skeleton -------------------------------------------------------------------------------------------
function expand(node, out = []) {
  if (node == null || typeof node === 'boolean' || typeof node === 'string' || typeof node === 'number') return out;
  if (Array.isArray(node)) { node.forEach(child => expand(child, out)); return out; }
  if (typeof node.type === 'function') return expand(node.type(node.props), out);
  if (node.type === React.Fragment) return expand(node.props.children, out);
  out.push(node); expand(node.props?.children, out); return out;
}
const textOf = node => node == null || typeof node === 'boolean' ? '' : Array.isArray(node) ? node.map(textOf).join('') : typeof node === 'object' ? textOf(node.props.children) : String(node);
function makeSpy(log) { return { cancelDispatch: row => log.push(['cancelDispatch', row]), cancelTruck: row => log.push(['cancelTruck', row]), again: row => log.push(['again', row]), clear: kind => log.push(['clear', kind]) }; }
function compareInteractions(canon, args) {
  const language = 'en';
  const logOriginal = [], logCanonical = [], logFenced = [];
  i18nContext.current = { language, t: makeT(language) };
  const originalButtons = expand(originalElement({ ...args, spy: makeSpy(logOriginal) })).filter(node => node.type === 'button');
  const enabledButtons = expand(canonicalElement(canon, { ...args, spy: makeSpy(logCanonical) }, true)).filter(node => node.type === 'button');
  const fencedButtons = expand(canonicalElement(canon, { ...args, spy: makeSpy(logFenced) }, false)).filter(node => node.type === 'button');
  if (!(originalButtons.length > 0 && originalButtons.length === enabledButtons.length && enabledButtons.length === fencedButtons.length)) throw new Mismatch('button count differs');
  originalButtons.forEach((button, index) => {
    const enabled = enabledButtons[index], fenced = fencedButtons[index];
    if (textOf(button) !== textOf(enabled) || textOf(enabled) !== textOf(fenced)) throw new Mismatch(`button label ${index}`);
    if (!!button.props.disabled !== !!enabled.props.disabled) throw new Mismatch(`button ${index} (${textOf(button)}) disabled ${button.props.disabled} vs ${enabled.props.disabled}`);
    if (fenced.props.disabled !== true || fenced.props.onClick !== undefined) throw new Mismatch(`fenced button ${index} (${textOf(button)}) is not disabled / carries a handler`);
    if (typeof enabled.props.onClick !== 'function' || typeof button.props.onClick !== 'function') throw new Mismatch(`button ${index} has no handler`);
    const before = [logOriginal.length, logCanonical.length];
    button.props.onClick(); enabled.props.onClick();
    if (logOriginal.length !== before[0] + 1 || logCanonical.length !== before[1] + 1) throw new Mismatch(`button ${index} handler call count`);
    const [originalName, originalArg] = logOriginal.at(-1), [canonicalName, canonicalArg] = logCanonical.at(-1);
    if (originalName !== canonicalName || originalArg !== canonicalArg) throw new Mismatch(`button ${index} handler (${originalName}) argument identity`);
    if (originalName === 'clear') { if (typeof canonicalArg !== 'string') throw new Mismatch('clear kind'); } else if (![...args.dispatchJobs, ...args.truckJobs].includes(canonicalArg)) throw new Mismatch(`handler ${canonicalName} did not receive the exact row object`);
    bump('interactionButtons');
  });
  assert.equal(logFenced.length, 0); bump('interactionCases');
}

// ================================================================ main
function isPlainData(value) {
  if (value === null || ['string', 'number', 'boolean', 'undefined'].includes(typeof value)) return true;
  if (Array.isArray(value)) return value.every(isPlainData);
  return typeof value === 'object' && Object.getPrototypeOf(value) === Object.prototype && Object.values(value).every(isPlainData);
}

globalThis.Date = FixedDate;
const report = { task: 'LWB317-UI-MAP-INTERACTIONS-001/scheduled', result: '', counts: {}, source: {}, mutation: {}, baseline: {}, fence: {}, keys: {}, fixtures: {}, limits: '' };
try {
  const canon = await buildCanonical(canonicalSources());

  // (a) differential render + interactions
  const failures = await runSuite(canon, { exhaustive: true });
  assert.deepEqual(failures, [], `differential failures (${failures.length}):\n` + failures.slice(0, 10).join('\n'));
  assert.deepEqual(reactWarnings, [], 'React warnings were emitted: ' + reactWarnings.slice(0, 3).join(' | '));
  const mainCounts = { ...counters }; // counters of the real (non-mutated) differential only

  // Real GameAssetImage (original `v`): same markup except the placeholder keeps `aria-label={alt}` instead of canonical `aria-hidden`.
  let realImageCases = 0, placeholderNormalisations = 0;
  for (const state of fixtureStates.slice(1)) for (const language of LANGUAGES) {
    const args = fixtureArgs(state);
    i18nContext.current = { language, t: makeT(language) };
    const real = renderToStaticMarkup(originalElement({ ...args }, withRealImage));
    const stub = renderToStaticMarkup(originalElement({ ...args }));
    const normalised = real.replace(/game-asset-placeholder" aria-label="[^"]*"/g, () => { placeholderNormalisations++; return 'game-asset-placeholder" aria-hidden="true"'; });
    assert.equal(normalised, stub, 'real GameAssetImage markup differs beyond the placeholder aria attribute'); realImageCases++;
  }
  assert.ok(placeholderNormalisations > 0);

  // (e) key presence
  const missing = [];
  // Recovered keys = every literal / table-driven key of the original groups. Keys requested only because the matrix feeds
  // deliberately unknown error codes (`error.DISPATCH_PLUNDER_FOO`, prototype names, objects) are the original's
  // "return the key itself" fallback and are not recovered keys.
  const usedKeys = new Set(originalStaticKeys);
  const hostileInputKeys = [...recordedOriginal].filter(key => !originalStaticKeys.has(key)).sort();
  assert.ok(hostileInputKeys.every(key => key.startsWith('error.')), 'unexpected non-error key outside the recovered set');
  for (const key of usedKeys) for (const language of LANGUAGES) if (!(key in catalogs[language])) missing.push(`${language}:${key}`);
  assert.deepEqual(missing, [], 'keys missing from locales');
  assert.deepEqual([...recordedCanonical].sort(), [...recordedOriginal].sort(), 'canonical requested a different t() key set than the original');
  const unexercised = [...originalStaticKeys].filter(key => !recordedOriginal.has(key));
  assert.deepEqual(unexercised, [], 'recovered keys never exercised by the matrix');
  report.keys = { locales: LANGUAGES.length, usedKeys: usedKeys.size, exercisedKeys: recordedOriginal.size, hostileInputKeys, hostileKeysPresentInAllLocales: hostileInputKeys.filter(key => LANGUAGES.every(language => key in catalogs[language])), missingInAnyLocale: missing, keys: [...usedKeys].sort() };

  // CSS parity: every class token the original markup emits has a selector in canonical CSS iff it has one in the original CSS asset
  const canonicalCss = read(path.join(srcDir, 'reference.css')) + read(path.join(srcDir, 'styles.css'));
  const originalCss = fs.readFileSync(path.join(repo, assets, 'index-rIL9Fpht.css'), 'utf8');
  const classTokens = new Set();
  for (const state of fixtureStates.slice(1)) for (const match of renderOriginal({ ...fixtureArgs(state) }, 'en').matchAll(/class="([^"]*)"/g)) match[1].split(/\s+/).filter(Boolean).forEach(token => classTokens.add(token));
  const hasSelector = (css, token) => new RegExp('\\.' + token.replace(/[^\w]/g, '\\$&') + '(?![\\w-])').test(css);
  const cssRows = [...classTokens].sort().map(token => ({ token, canonical: hasSelector(canonicalCss, token), original: hasSelector(originalCss, token) }));
  assert.deepEqual(cssRows.filter(row => row.canonical !== row.original), [], 'CSS selector presence differs from the original asset');
  report.css = { classTokens: cssRows.length, withSelector: cssRows.filter(row => row.canonical).map(row => row.token), withoutSelectorInBoth: cssRows.filter(row => !row.canonical).map(row => row.token), note: 'Tokens without a selector exist in neither the original CSS asset nor canonical CSS (markup-only modifier classes); no CSS was added.' };

  // fixtures: determinism, whitelist, coverage
  const fixtureReport = { states: {} };
  for (const state of fixtureStates) {
    const first = fixtures.plunderFixtureFor(state, NOW), second = fixtures.plunderFixtureFor(state, NOW);
    assert.deepEqual(first, second); assert.notEqual(first.dispatchJobs, second.dispatchJobs);
    assert.ok(isPlainData(first), 'fixture must be plain data');
    for (const job of [...first.dispatchJobs, ...first.truckJobs]) {
      for (const key of Object.keys(job)) assert.ok(readFields.has(key), `fixture field not read by the original: ${key}`);
      for (const item of [...(job.rewards || []), ...(job.plunderRewards || [])]) for (const key of Object.keys(item)) assert.ok(['key', 'name', 'nameKey', 'count', 'iconPath'].includes(key), key);
      for (const label of [job.ownerName, job.allianceName]) assert.ok(label === undefined || label === '' || /^Synthetic/.test(label), 'unlabelled synthetic name');
    }
    assert.equal(typeof first.online, 'boolean'); assert.equal(typeof first.busyKey, 'string');
    fixtureReport.states[state] = { dispatchRows: first.dispatchJobs.filter(job => job.taskKind !== 'ghost').length, ghostRows: first.dispatchJobs.filter(job => job.taskKind === 'ghost').length, truckRows: first.truckJobs.length, online: first.online, busyKey: first.busyKey, count: canon.P.scheduledPlunderCount(first.dispatchJobs, first.truckJobs) };
  }
  assert.equal(fixtures.plunderFixtureFor('map-city', NOW), null); assert.equal(fixtures.plunderFixtureFor('map-scheduled-x', NOW), null);
  assert.deepEqual(fixtureReport.states['map-scheduled'], { dispatchRows: 0, ghostRows: 0, truckRows: 0, online: false, busyKey: '', count: 0 });
  assert.throws(() => fixtures.plunderFixtureFor('map-scheduled', Number.NaN), TypeError);
  assert.notDeepEqual(fixtures.plunderFixtureFor('map-scheduled-populated', NOW), fixtures.plunderFixtureFor('map-scheduled-populated', NOW + 1));
  assert.deepEqual(fixtures.plunderFixtureFor('map-scheduled-populated', NOW + 5000).dispatchJobs[0].completionTime - 5000, fixtures.plunderFixtureFor('map-scheduled-populated', NOW).dispatchJobs[0].completionTime);
  // coverage of recovered branches, by recomputing with the ORIGINAL functions over the fixture rows as presented
  const coverage = new Set();
  const all = state => fixtures.plunderFixtureFor(state, NOW);
  for (const state of fixtureStates.slice(1)) {
    const fixture = all(state), t = makeT('en');
    for (const online of [true, false]) for (const job of fixture.dispatchJobs) {
      coverage.add(`status:${job.scheduleStatus}`); coverage.add(`kind:${job.taskKind}`);
      coverage.add(`secretTiming:${original.F(job, NOW)}`);
      if (job.isSpecial) coverage.add('secret:isSpecial'); if (job.rewards?.length) coverage.add('secret:rewards'); else coverage.add('secret:noRewards');
      if (!(job.ownerName)) coverage.add(job.ownerUid ? 'secret:ownerUid' : 'secret:ownerUuid');
      if (job.scheduleStatus === 'failed') coverage.add(`secretError:${!job.lastError ? 'missing' : job.lastError === 'E000000' ? 'E000000' : gameTexts[job.lastError] ? 'gameText' : /DISPATCH_PLUNDER_[A-Z_]+/.test(String(job.lastError)) ? 'code' : /^\d+$/.test(job.lastError) ? 'numeric' : original.b.some(([needle]) => String(job.lastError).toLowerCase().includes(needle)) ? 'table' : 'unknown'}`);
      for (const key of ['completionTime', 'plunderAt']) { const value = Number(job[key]); if (!Number.isFinite(value) || value <= 0) coverage.add('secret:invalidTimestamp'); else if (value < 1e12) coverage.add('secret:secondsTimestamp'); }
    }
    const inflight = new Set(fixture.truckJobs.filter(job => ['scheduled', 'waiting_connection', 'running'].includes(job.scheduleStatus)).map(job => `${job.serverId}:${job.uuid}`));
    for (const job of fixture.truckJobs) {
      const truck = original.Ie(job, NOW);
      coverage.add(`truckStatus:${job.scheduleStatus}`); coverage.add(`truckState:${truck}`);
      if (job.scheduleStatus === 'succeeded') coverage.add(`battleWon:${job.battleWon}`);
      if (job.isSpecialURQuality) coverage.add('truck:reindeer'); if (job.plunderRewards?.length) coverage.add('truck:rewards'); else coverage.add('truck:noRewards');
      if (!job.ownerName) coverage.add(job.allianceName ? 'truck:allianceName' : 'truck:uuidOwner');
      if (job.scheduleStatus === 'failed' || job.scheduleStatus === 'expired') coverage.add(`truckError:${!job.lastError ? 'missing' : job.lastError === 'E000000' ? 'E000000' : original.Te[String(job.lastError).replace(/\s+/g, ' ').trim().toLowerCase()] ? 'table' : /^\d+$/.test(job.lastError) ? 'numeric' : 'unknown'}`);
      const again = fixture.online && job.scheduleStatus === 'succeeded' && !['invalid', 'full', 'expired'].includes(truck) && !inflight.has(`${job.serverId}:${job.uuid}`);
      if (again) coverage.add('truck:againShown'); else if (job.scheduleStatus === 'succeeded') coverage.add(inflight.has(`${job.serverId}:${job.uuid}`) ? 'truck:againHiddenInflight' : fixture.online ? 'truck:againHiddenState' : 'truck:againHiddenOffline');
      if (!Number.isFinite(Number(job.robTimes))) coverage.add('truck:nonNumericRobTimes'); if (Number(job.robTimes) < 0) coverage.add('truck:negativeRobTimes');
    }
  }
  const requiredCoverage = [...['scheduled', 'waiting_connection', 'running', 'succeeded', 'failed', 'cancelled', 'expired', 'synthetic-unknown-status'].map(s => `status:${s}`), 'kind:ghost', 'kind:dispatch',
    ...['pending', 'protected', 'ready', 'full', 'expired'].map(s => `secretTiming:${s}`), 'secret:isSpecial', 'secret:rewards', 'secret:noRewards', 'secret:ownerUid', 'secret:ownerUuid', 'secret:invalidTimestamp', 'secret:secondsTimestamp',
    ...['missing', 'E000000', 'gameText', 'code', 'numeric', 'table', 'unknown'].map(s => `secretError:${s}`),
    ...['scheduled', 'waiting_connection', 'running', 'succeeded', 'failed', 'cancelled', 'expired', 'synthetic-unknown-status'].map(s => `truckStatus:${s}`), ...['ready', 'protected', 'full', 'expired', 'invalid'].map(s => `truckState:${s}`),
    'battleWon:true', 'battleWon:false', 'battleWon:undefined', 'truck:reindeer', 'truck:rewards', 'truck:noRewards', 'truck:allianceName', 'truck:uuidOwner', ...['missing', 'E000000', 'table', 'numeric', 'unknown'].map(s => `truckError:${s}`),
    'truck:againShown', 'truck:againHiddenInflight', 'truck:againHiddenState', 'truck:againHiddenOffline', 'truck:nonNumericRobTimes', 'truck:negativeRobTimes'];
  const absent = requiredCoverage.filter(key => !coverage.has(key));
  assert.deepEqual(absent, [], 'fixture branch coverage gaps');
  fixtureReport.coveredBranches = [...coverage].sort(); fixtureReport.requiredBranches = requiredCoverage.length;
  report.fixtures = fixtureReport;

  // (d) fence checks on the rendered element props
  report.fence = { fencedButtonsDisabledWithoutHandler: mainCounts.interactionButtons, note: 'Every fixture button: actionsEnabled=false => disabled===true and onClick===undefined; actionsEnabled=true => disabled flag, label, handler name and exact row object equal the original.' };

  // (c) old surface baseline (pre-campaign commit 3e8617c), expected to FAIL parity
  const baselineCommit = '3e8617c';
  const baselineSource = execFileSync('git', ['show', `${baselineCommit}:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`], { cwd: repo, encoding: 'utf8' }).replace(/\r\n/g, '\n');
  const oldSurface = '<div className="map-table-scroll"><table className="map-table map-table--scheduled-plunder"><tbody><tr><td className="map-empty">{t("map.scheduledPlunder")} · {t("map.empty")}</td></tr></tbody></table></div>';
  assert.ok(baselineSource.includes(oldSurface), 'baseline surface not found in 3e8617c');
  assert.ok(baselineSource.includes('{key === "scheduledPlunder" ? "0" : summaryReady ? counts[key] || 0 : "—"}'), 'baseline tab count not found');
  const oldRender = language => `<div class="map-table-scroll"><table class="map-table map-table--scheduled-plunder"><tbody><tr><td class="map-empty">${catalogs[language]['map.scheduledPlunder']} · ${catalogs[language]['map.empty']}</td></tr></tbody></table></div>`;
  const baselineRows = [];
  for (const state of fixtureStates) {
    const args = fixtureArgs(state);
    for (const language of LANGUAGES) {
      const expected = renderOriginal(args, language);
      assert.notEqual(oldRender(language), expected, `old surface unexpectedly equals the original (${state}/${language})`);
    }
    const expectedCount = args.dispatchJobs.length + args.truckJobs.length;
    baselineRows.push({ state, originalCount: expectedCount, oldCount: 0, markupDiffers: true, countDiffers: expectedCount !== 0, oldTableRows: 1, oldHeaders: 0, originalSections: 3 });
  }
  assert.ok(baselineRows.filter(row => row.countDiffers).length === 2);
  report.baseline = { commit: baselineCommit, oldSurface, oldTabCount: '"0" (hardcoded)', result: 'old surface fails the differential in all 3 fixture states (markup) and 2 populated states (count)', rows: baselineRows };

  // (b) negative / mutation proof
  const mutations = [
    ['ScheduledPlunder.jsx', 'swap kind label', 'const title = t(kind === "ghost" ? "map.ghostScout" : "map.secretTask");', 'const title = t(kind === "ghost" ? "map.secretTask" : "map.ghostScout");'],
    ['ScheduledPlunder.jsx', 'wrong column order (completion/plunderAt)', '<th scope="col">{t("map.completionTime")}</th>\n              <th scope="col">{t("map.plunderAt")}</th>', '<th scope="col">{t("map.plunderAt")}</th>\n              <th scope="col">{t("map.completionTime")}</th>'],
    ['ScheduledPlunder.jsx', 'wrong column order (truck result/rewards)', '<th scope="col">{t("map.plunderResult")}</th>\n              <th scope="col">{t("map.plunderRewards")}</th>', '<th scope="col">{t("map.plunderRewards")}</th>\n              <th scope="col">{t("map.plunderResult")}</th>'],
    ['ScheduledPlunder.jsx', 'secret column width', 'const SECRET_WIDTHS = [78, 150, 92, 280, 135, 135, 180, 74]', 'const SECRET_WIDTHS = [78, 150, 92, 281, 135, 135, 180, 74]'],
    ['ScheduledPlunder.jsx', 'truck column width', 'const TRUCK_WIDTHS = [78, 150, 86, 110, 200, 280, 92]', 'const TRUCK_WIDTHS = [78, 150, 86, 110, 200, 281, 92]'],
    ['ScheduledPlunder.jsx', 'secret owner fallback order', '{job.ownerName || job.ownerUid || job.uuid}', '{job.ownerUid || job.ownerName || job.uuid}'],
    ['ScheduledPlunder.jsx', 'isSpecial strictness', '{job.isSpecial ? t("map.specialQuality")', '{job.isSpecial === true ? t("map.specialQuality")'],
    ['ScheduledPlunder.jsx', 'reward empty check', 'return rewards?.length ? (', 'return rewards ? ('],
    ['ScheduledPlunder.jsx', 'truck title', 'title={result === "-" ? "" : result}', 'title={result}'],
    ['ScheduledPlunder.jsx', 'truck cancel busy key', 'disabled={!actionsEnabled || busyKey === busy} onClick={actionsEnabled ? () => onCancel(job) : undefined}>{t("common.cancel")}</button>}\n                    {again', 'disabled={!actionsEnabled || busyKey === plunderJobKey(job)} onClick={actionsEnabled ? () => onCancel(job) : undefined}>{t("common.cancel")}</button>}\n                    {again'],
    ['ScheduledPlunder.jsx', 'fence: drop actionsEnabled from secret Clear', 'disabled={!actionsEnabled || plunderClearDisabled(jobs, busyKey)} onClick={actionsEnabled ? () => onClear(kind) : undefined}', 'disabled={plunderClearDisabled(jobs, busyKey)} onClick={actionsEnabled ? () => onClear(kind) : undefined}'],
    ['ScheduledPlunder.jsx', 'fence: handler live while fenced', 'onClick={actionsEnabled ? () => onPlunderAgain(job) : undefined}', 'onClick={() => onPlunderAgain(job)}'],
    ['ScheduledPlunder.jsx', 'fence: drop actionsEnabled from truck Plunder Again', 'disabled={!actionsEnabled || busyKey === busy} onClick={actionsEnabled ? () => onPlunderAgain(job)', 'disabled={busyKey === busy} onClick={actionsEnabled ? () => onPlunderAgain(job)'],
    ['ScheduledPlunder.jsx', 'handler argument (secret clear kind)', 'onClick={actionsEnabled ? () => onClear(kind) : undefined}', 'onClick={actionsEnabled ? () => onClear("dispatch") : undefined}'],
    ['ScheduledPlunder.jsx', 'handler argument (clear kind)', 'onClick={actionsEnabled ? () => onClear("truck") : undefined}', 'onClick={actionsEnabled ? () => onClear("ghost") : undefined}'],
    ['ScheduledPlunder.jsx', 'handler argument (cancel row)', 'onClick={actionsEnabled ? () => onCancel(job) : undefined}>{t("common.cancel")}</button>}\n                    {!cancellable && "-"}', 'onClick={actionsEnabled ? () => onCancel({ ...job }) : undefined}>{t("common.cancel")}</button>}\n                    {!cancellable && "-"}'],
    ['ScheduledPlunder.jsx', 'ghost group uses dispatch filter', 'kind="ghost" jobs={ghostGroupJobs(dispatchJobs)}', 'kind="ghost" jobs={dispatchGroupJobs(dispatchJobs)}'],
    ['ScheduledPlunder.jsx', 'group order', '<SecretPlunderGroup jobs={dispatchGroupJobs(dispatchJobs)}', '<SecretPlunderGroup kind="ghost" jobs={ghostGroupJobs(dispatchJobs)}'],
    ['mapPlunderPresentation.js', 'swap dispatch/ghost filter', 'return jobs.filter((job) => job.taskKind !== "ghost");', 'return jobs.filter((job) => job.taskKind === "ghost");'],
    ['mapPlunderPresentation.js', 'ghost filter strictness', 'return jobs.filter((job) => job.taskKind === "ghost");', 'return jobs.filter((job) => job.taskKind === "ghost" || job.taskKind === "other");'],
    ['mapPlunderPresentation.js', 'drop duplicate in-flight check (ee2)', ' && !inflightKeys.has(plunderJobKey(job));', ';'],
    ['mapPlunderPresentation.js', 'in-flight status set', '["scheduled", "waiting_connection", "running"]', '["scheduled", "waiting_connection"]'],
    ['mapPlunderPresentation.js', 'terminal status set', '["succeeded", "failed", "cancelled", "expired"]', '["succeeded", "failed", "cancelled"]'],
    ['mapPlunderPresentation.js', 'cancel visibility', 'job.scheduleStatus === "scheduled" || job.scheduleStatus === "waiting_connection"; }', 'job.scheduleStatus === "scheduled"; }'],
    ['mapPlunderPresentation.js', 'clear disabled ignores busyKey', 'return !!busyKey || !plunderHasFinishedJobs(jobs);', 'return !plunderHasFinishedJobs(jobs);'],
    ['mapPlunderPresentation.js', 'plunder again offline', 'return online && job.scheduleStatus === "succeeded"', 'return job.scheduleStatus === "succeeded"'],
    ['mapPlunderPresentation.js', 'plunder again full state', ' && state !== "full" && state !== "expired"', ' && state !== "expired"'],
    ['mapPlunderPresentation.js', 'tab count', 'return dispatchJobs.length + truckJobs.length;', 'return dispatchJobs.length;'],
    ['mapPlunderPresentation.js', 'secret waiting class', '["waiting", t("map.waitingConnection")]', '["pending", t("map.waitingConnection")]'],
    ['mapPlunderPresentation.js', 'secret E000000 game text bypass', 'job.lastError !== "E000000" && gameTexts[job.lastError] ||', 'gameTexts[job.lastError] ||'],
    ['mapPlunderPresentation.js', 'secret expired status label', '["expired", t("map.taskExpired")]', '["expired", t("map.plunderFailed")]'],
    ['mapPlunderPresentation.js', 'dispatch table mapping', '["game disconnected", "DISPATCH_PLUNDER_GAME_DISCONNECTED"]', '["game disconnected", "DISPATCH_PLUNDER_UNKNOWN"]'],
    ['mapPlunderPresentation.js', 'dispatch error compaction length', 'text.length > 60 ? `${text.slice(0, 57)}...` : text', 'text.length > 60 ? `${text.slice(0, 56)}...` : text'],
    ['mapPlunderPresentation.js', 'dispatch empty detail falls back to code', 'compactPlunderErrorText(remainder || text) || code;', 'compactPlunderErrorText(remainder || text);'],
    ['mapPlunderPresentation.js', 'truck E000000', 'if (text === "E000000") return', 'if (text === "E00000") return'],
    ['mapPlunderPresentation.js', 'truck table entry', 'trade_person_tips1013: "TRUCK_PLUNDER_ATTACK_LIMIT_REACHED"', 'trade_person_tips1013: "TRUCK_PLUNDER_ALLIED_TARGET"'],
    ['mapPlunderPresentation.js', 'truck lowercase lookup', 'TRUCK_PLUNDER_ERROR_TABLE[text.toLowerCase()]', 'TRUCK_PLUNDER_ERROR_TABLE[text]'],
    ['mapPlunderPresentation.js', 'truck won/lost', 'job.battleWon === true ? t("map.plunderWon")', 'job.battleWon === true ? t("map.plunderLost")'],
    ['mapPlunderPresentation.js', 'truck succeeded without outcome', ': job.scheduleStatus === "succeeded" ? "-"', ': job.scheduleStatus === "succeeded" ? t("map.plunderSucceeded")'],
    ['mapPlunderPresentation.js', 'truck expired uses error text', 'job.scheduleStatus === "expired" ? errorText', 'job.scheduleStatus === "expired" ? t("map.taskExpired")'],
    ['mapPlunderPresentation.js', 'truck protected countdown', 'mapDuration(Number(job.protectTime) - now)', 'mapDuration(Number(job.protectTime) - now + 1000)'],
    ['mapPlunderPresentation.js', 'truck full label', 't(state === "full" ? "map.truckPlunderFull" : "map.truckReady")', 't(state === "full" ? "map.truckReady" : "map.truckPlunderFull")'],
    ['mapTablePresentation.js', 'task expire boundary (< vs <=)', 'expires && now >= expires', 'expires && now > expires'],
    ['mapTablePresentation.js', 'task completion boundary', '!completed || now < completed', '!completed || now <= completed'],
    ['mapTablePresentation.js', 'task plunderAt boundary', 'plunderAt && now < plunderAt', 'plunderAt && now <= plunderAt'],
    ['mapTablePresentation.js', 'task full boundary', 'maximum > 0 && stolen >= maximum ? "full"', 'maximum > 0 && stolen > maximum ? "full"'],
    ['mapTablePresentation.js', 'truck arrival boundary', 'arrival > 0 && arrival <= now', 'arrival > 0 && arrival < now'],
    ['mapTablePresentation.js', 'truck protect boundary', '(Number(row.protectTime) || 0) > now', '(Number(row.protectTime) || 0) >= now'],
    ['mapTablePresentation.js', 'truck full boundary', 'maximum > 0 && robbed >= maximum ? "full"', 'maximum > 0 && robbed > maximum ? "full"'],
    ['mapTablePresentation.js', 'truck reindeer maximum', 'if (row.isSpecialURQuality) return 1;', 'if (row.isSpecialURQuality) return 2;'],
    ['mapTablePresentation.js', 'timestamp seconds threshold', 'number < 1_000_000_000_000 ? number * 1000', 'number <= 1_000_000_000_000 ? number * 1000'],
  ];
  const detected = [], survivors = [];
  const mutationList = process.env.LWB317_SKIP_MUTATIONS ? [] : mutations; // env switch is a developer aid only; recorded runs never skip
  for (const [file, name, from, to] of mutationList) {
    const sources = canonicalSources();
    assert.ok(sources[file].includes(from), `mutation target not found: ${name}`);
    sources[file] = sources[file].replace(from, to);
    assert.notEqual(sources[file], canonicalSources()[file], name);
    let failed = false;
    try { const mutated = await buildCanonical(sources); failed = (await runSuite(mutated, { exhaustive: false })).length > 0; } catch (error) { failed = error instanceof Mismatch || error instanceof assert.AssertionError || error instanceof TypeError; if (!failed) throw error; }
    (failed ? detected : survivors).push(`${file}: ${name}`);
  }
  assert.deepEqual(survivors, [], `mutations survived: ${survivors.join('; ')}`);
  if (process.env.LWB317_SKIP_MUTATIONS) assert.ok(!process.argv.includes('--record') && !process.argv.includes('--verify-record'), 'mutation proof skipped; refusing to record');
  report.mutation = { total: mutations.length, detected: detected.length, survivors, detectedList: detected };

  // locators (for the evidence README / results.json)
  const startLine = needle => prettyLine(needle);
  report.source = {
    panel: { path: panelPath, sha256: sha(panel) }, main: { path: mainPath, sha256: sha(main) }, rewardDisplay: { path: rewardPath, sha256: sha(rewardsText) }, gameAssetImage: { path: assetImagePath, sha256: sha(assetImageText) }, pretty: { path: prettyPath },
    panelLocators: Object.fromEntries([...Object.entries(fnNodes), ...Object.entries(varNodes)].map(([name, node]) => [name, locator(panel, node)])),
    mainLocators: Object.fromEntries(Object.entries(truckNodes).map(([name, node]) => [name, locator(main, node)])), rewardLocator: locator(rewardsText, rewardNode),
    prettyLines: { b: startLine('var b = [['), x: startLine('function x(e2)'), Ce: startLine('function Ce(e2)'), we: startLine('function we(e2, t2)'), Te: startLine('var Te = {'), S: startLine('function S(e2)'), C: startLine('function C(e2, t2)'), ot: startLine('function ot({ jobs'), st: startLine('function st({ jobs'),
      mainRender: startLine('children: S2(`map.scheduledPlunder`) }), (0, E.jsx)(`span`, { className: `map-tab-count`, children: ln.length + dn.length })'), refreshHandlers: startLine('bridge://dispatch-plunder-changed'), ticker: startLine('window.setInterval(() => an(Date.now()), 1e3)'), cancelHandlers: startLine('async function hr(e2)'), clearHandler: startLine('async function mr(e2)') },
    mainComponentOffsets: { filters: utf8Offset(panel, panel.indexOf('ln.filter')), tabCountAndItemCount: [...panel.matchAll(/ln\.length\+dn\.length/g)].map(match => utf8Offset(panel, match.index)) },
    canonicalFiles: Object.fromEntries(['ScheduledPlunder.jsx', 'mapPlunderPresentation.js', 'mapPlunderFixtures.js'].map(file => [`src/LWBridge.UI-0.3.17/src/${file}`, sha(read(path.join(srcDir, file)))])),
  };
  // original job-feed natives named by the original main component (for README only; not called here)
  report.source.nativeCommandsNamedByOriginal = ['map_plunder_jobs_list', 'map_dispatch_plunder_cancel', 'map_dispatch_plunder_clear', 'map_truck_plunder_cancel', 'map_truck_plunder_clear', 'map_truck_plunder_schedule', 'map_dispatch_plunder_schedule'].map(command => ({ command, count: main.split(`\`${command}\``).length - 1, utf8ByteOffset: utf8Offset(main, main.indexOf(`\`${command}\``)) }));
  assert.ok(report.source.nativeCommandsNamedByOriginal.every(entry => entry.count > 0));
  report.counts = { ...mainCounts, realGameAssetImageCases: realImageCases, placeholderNormalisations, mutationsTotal: mutations.length, mutationsDetected: detected.length, usedLocaleKeys: usedKeys.size, reactWarnings: 0 };
  report.realGameAssetImageNormalisation = 'Differential runs use a placeholder stub for original `v` (GameAssetImage) identical to the canonical Map table placeholder; a second pass runs the REAL GameAssetImage extracted from its asset and shows the only difference is the original placeholder `aria-label={alt}` vs canonical `aria-hidden="true"` (the parent .map-reward-item already carries the same aria-label).';
  report.limits = 'Source-level differential (static markup, interaction props) with synthetic rows and stubbed useI18n/GameAssetImage; no browser pixels, no original-runtime job rows, no native/gameplay execution. Row shapes beyond the fields read by the original are UNKNOWN (job producer is a native command; its schema is not in the frontend assets).';
  report.result = 'LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK';
} finally { globalThis.Date = RealDate; console.error = realConsoleError; }
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'results.json'), JSON.stringify(report, null, 2) + '\n');
if (process.argv.includes('--verify-record')) assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, 'results.json'), 'utf8')), JSON.parse(JSON.stringify(report)));
console.log(JSON.stringify({ result: report.result, ...report.counts, mutationSurvivors: report.mutation.survivors.length, baselineFailsParity: report.baseline.rows.length, fixtureBranchesCovered: report.fixtures.requiredBranches, seconds: Math.round((performance.now() - startedAt) / 100) / 10 }, null, 2));
