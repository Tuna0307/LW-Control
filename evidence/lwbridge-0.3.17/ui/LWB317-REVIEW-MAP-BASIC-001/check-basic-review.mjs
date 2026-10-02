import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
const { transformSync } = require('esbuild');
const integrated = process.argv.includes('--current');
const output = integrated ? 'current-results.json' : 'baseline-results.json';
const hash = s => crypto.createHash('sha256').update(s).digest('hex').toUpperCase();
const read = relative => fs.readFileSync(path.join(repo, relative), 'utf8');
const originalPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const indexPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js';
const source = read(originalPath), index = read(indexPath);
assert.equal(hash(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
assert.equal(hash(index), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
const manifest = JSON.parse(fs.readFileSync(path.join(here, 'baseline-manifest.json'), 'utf8'));
for (const entry of manifest.files) assert.equal(hash(fs.readFileSync(path.join(repo, entry.snapshot))), entry.sha256);
const pagePath = integrated ? 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx' : 'evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/baseline/MapDataPage.jsx';
const helperPath = integrated ? 'src/LWBridge.UI-0.3.17/src/mapTablePresentation.js' : 'evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/baseline/mapTablePresentation.js';
const pages = read(pagePath);
const helpers = await import(pathToFileURL(path.join(repo, helperPath)));
function walk(n, out = []) {
  if (!n || typeof n !== 'object') return out;
  if (n.type) out.push(n);
  for (const v of Object.values(n)) {
    if (Array.isArray(v)) v.forEach(c => walk(c, out));
    else if (v && typeof v === 'object') walk(v, out);
  }
  return out;
}
const ast = parse(source, { sourceType: 'module' }), indexAst = parse(index, { sourceType: 'module' });
const pageAst = parse(pages, { sourceType: 'module', plugins: ['jsx'] });
const fn = (tree, name) => { const n = tree.program.body.map(n => n.declaration || n).find(n => n.type === 'FunctionDeclaration' && n.id?.name === name); assert.ok(n, name); return n; };
const variable = name => { const n = ast.program.body.flatMap(n => n.declarations || []).find(n => n.id.name === name); assert.ok(n, name); return n; };
const cut = (text, n) => text.slice(n.start, n.end);
const locator = (text, n) => ({ byte: Buffer.byteLength(text.slice(0, n.start)), expression: cut(text, n) });
const tableNode = variable('at');
const tableExpression = tableNode.init.arguments[0];
const basicFunctions = ['k', 'A', 'j', 'qe', 'N', 'Qe', '$e', 'I', 'et', 'nt'];
const now = 1799000000000;
const RealDate = globalThis.Date;
class FixedDate extends RealDate { constructor(...args) { super(...(args.length ? args : [now])); } static now() { return now; } }
const originalHelpers = new Function('Date', basicFunctions.map(n => cut(source, fn(ast, n))).join('\n')
  + ';const ' + cut(source, variable('O')) + ';return {columns:nt};')(FixedDate);
function h(type, props, ...children) {
  const p = { ...props, ...(children.length ? { children } : {}) };
  if (typeof type === 'function') return type(p);
  return { type, props: p, key: p.key };
}
const jsx = { jsx: (type, props, key) => h(type, { ...props, key }), jsxs: (type, props, key) => h(type, { ...props, key }), Fragment: 'Fragment' };
const iconNode = fn(indexAst, 'Vr');
const Icon = new Function('M', cut(index, iconNode) + ';return Vr;')(jsx);
const tags = { city: 'map.playerCity', resource: 'map.resourcePoint', monster: 'map.monster' };
const locators = Object.fromEntries(basicFunctions.map(name => [name, locator(source, fn(ast, name))]));
for (const name of ['ue', 'de', 'he']) locators[name] = locator(source, walk(tableNode).find(n => n.type === 'FunctionDeclaration' && n.id.name === name));
locators.icon = locator(index, iconNode);
const failures = [], assertions = [];
function compare(id, expected, actual, context = {}) {
  try { assert.deepEqual(actual, expected); assertions.push({ id, ...context }); }
  catch { failures.push({ id, ...context, expected, actual }); }
}
function nodes(n, out = []) {
  if (Array.isArray(n)) n.forEach(c => nodes(c, out));
  else if (n && typeof n === 'object') { out.push(n); nodes(n.props?.children, out); }
  return out;
}
function text(n) {
  if (n == null || typeof n === 'boolean') return '';
  if (Array.isArray(n)) return n.map(text).join('');
  if (typeof n === 'object') return text(n.props.children);
  return String(n);
}
const metadata = columns => columns.map(c => Object.fromEntries(Object.entries(c).filter(([,v]) => typeof v !== 'function' && v !== undefined)));
const coord = tree => {
  const td = nodes(tree).find(n => n.type === 'td' && n.props.className === 'map-column-coordinate');
  const button = nodes(td).find(n => n.type === 'button');
  return { text: text(td), button: Boolean(button), disabled: button ? Boolean(button.props.disabled) : null };
};
const rowNodes = tree => nodes(tree).filter(n => n.type === 'tr' && n.props.className?.startsWith('map-row') && !n.props.className.includes('map-head'));
const iconShape = tree => nodes(tree).filter(n => n.type === 'svg' || n.type === 'path').map(n => ({ type: n.type, className: n.props.className, d: n.props.d, viewBox: n.props.viewBox }));
globalThis.Date = FixedDate;
try {
  for (const language of ['en', 'ja']) {
    const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
    const t = (key, values = {}) => { assert.ok(key in catalog, key); return catalog[key].replace(/\{(\w+)\}/g, (m, k) => String(values[k] ?? m)); };
    const original = new Function('u', 'y', 'E', 'se', 'Ie', 'Date', basicFunctions.map(n => cut(source, fn(ast, n))).join('\n')
      + ';const ' + cut(source, variable('O')) + ';return (' + cut(source, tableExpression) + ');')(
      () => ({ language, t }), { useMemo: f => f() }, jsx, Icon, Object.entries(tags).map(([key, label]) => ({ key, label })), FixedDate);
    const currentTable = fn(pageAst, 'MapTable');
    const code = transformSync(cut(pages, currentTable), { loader: 'jsx', jsxFactory: 'h' }).code;
    const coordinateNode = pageAst.program.body.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'coordinateText');
    const extraCode = coordinateNode ? cut(pages, coordinateNode) : '';
    const injected = { ...helpers, h, Icon, useI18n: () => ({ language, t }), useMemo: f => f(), useState: f => [typeof f === 'function' ? f() : f, () => {}], useEffect: () => {}, EMPTY_GAME_TEXTS: {}, SCAN_TYPE_LABEL_KEYS: tags, Date: FixedDate };
    const current = new Function(...Object.keys(injected), extraCode + '\n' + code + ';return MapTable;')(...Object.values(injected));
    const gameTexts = { knownResource: 'Resource display', knownMonster: 'Monster display', '300039': 'Occupied display', '372138': 'Idle display', same: 'same' };
    const base = { serverId: 321, uuid: '11', recordKey: 'record', marchUuid: 'march', ownerUid: '42', ownerName: 'Owner', allianceName: 'Alliance', x: 12, y: 34, level: 12, health: 99999, protectEndTime: now + 1000, updatedAt: now - 1, resourceNameKey: 'knownResource', monsterNameKey: 'knownMonster', distanceFromHome: 1000 };
    const render = (factory, kind, row, disabled = false, jumpingKey = '', sorts = [], busy = false, loading = false) => factory === original
      ? original({ kind, rows: row ? [row] : [], loading, gameTexts, currentTime: now, sortState: sorts, selectedDispatchKeys: new Set(), selectedTruckKeys: new Set(), jumpingKey, jumpDisabled: disabled || busy, onSort: () => {}, onCoordinateJump: () => {}, onPlayerMark: () => {} })
      : current({ kind, rows: row ? [row] : [], loading, gameTexts, sorts, selectedKeys: new Set(), jumpingKey, actionBusy: busy, actionDisabled: disabled, onSort: () => {}, onCoordinateJump: () => {}, onPlayerMark: () => {} });
    for (const kind of Object.keys(tags)) {
      const cols = originalHelpers.columns(kind, t, language, gameTexts, '', false), actual = helpers.buildMapColumns(kind, t, language, gameTexts, '', false);
      compare('column metadata', metadata(cols), metadata(actual), { language, kind });
      const cases = [base, {}, { ...base, level: null, health: 'bad', updatedAt: 0, protectEndTime: now, distanceFromHome: -1 }, { ...base, protectEndTime: 0, shieldEndTime: now + 1, updatedAt: 1799000000 }, { ...base, ownerName: 0, allianceName: '', resourceNameKey: 'same', monsterNameKey: 'unknown', gatherUid: ' 0 ' }, { ...base, gatherMarchUuid: 'g', protectEndTime: now - 1 }, { ...base, gatherUid: '12', resourceNameKey: ' knownResource ', monsterNameKey: ' knownMonster ' }];
      for (const [index, row] of cases.entries()) for (const [column, c] of cols.entries()) if (c.value) compare('column value', c.value(row), actual[column].value(row), { language, kind, index, column });
      for (const [caseName, row, disabled, jumpingKey] of [ ['valid', base, false, ''], ['jumping', base, true, '321:12:34'], ['zero', { ...base, x: 0 }, false, ''], ['negative', { ...base, y: -1 }, false, ''], ['fractional', { ...base, x: 1.5 }, false, ''], ['missing', {}, true, ''] ]) {
        const expected = render(original, kind, row, disabled, jumpingKey), actualTree = render(current, kind, row, disabled, jumpingKey);
        compare('coordinate presentation', coord(expected), coord(actualTree), { language, kind, caseName, input: row, disabled, jumpingKey });
      }
      for (const row of [base, { ...base, uuid: '', marchUuid: 'march', recordKey: 'record' }, { pointIndex: 0, ownerUid: '42', updatedAt: 123 }, { serverId: 0, recordKey: 'r' }]) compare('row identity', rowNodes(render(original, kind, row))[0].key, rowNodes(render(current, kind, row))[0].key, { language, kind, input: row });
      for (const state of ['', 'missing', 'replaced']) {
        const row = { ...base, marked: true, trackerState: state };
        compare('row classes', rowNodes(render(original, kind, row))[0].props.className, rowNodes(render(current, kind, row))[0].props.className, { language, kind, input: row });
      }
      const sorts = [{ sortBy: 'level', sortOrder: 'asc' }, { sortBy: 'updatedAt', sortOrder: 'desc' }];
      const headShape = tree => nodes(tree).filter(n => n.type === 'th').map(n => ({ text: text(n), sort: n.props['aria-sort'], labels: nodes(n).filter(n => n.type === 'button').map(n => n.props['aria-label']) }));
      compare('header sorting', headShape(render(original, kind, base, false, '', sorts)), headShape(render(current, kind, base, false, '', sorts)), { language, kind });
      compare('empty text', text(render(original, kind, null)), text(render(current, kind, null)), { language, kind });
      compare('loading empty text', text(render(original, kind, null, false, '', [], false, true)), text(render(current, kind, null, false, '', [], false, true)), { language, kind });
      for (const disabled of [false, true]) for (const busy of [false, true]) compare('coordinate busy predicate', coord(render(original, kind, base, disabled, '', [], busy)), coord(render(current, kind, base, disabled, '', [], busy)), { language, kind, disabled, busy });
    }
    for (const row of [{ ...base, marked: false }, { ...base, marked: true, trackerState: 'missing' }, { ...base, trackerState: 'replaced' }, { ...base, ownerUid: '' }]) {
      const expected = nodes(render(original, 'city', row)).find(n => n.type === 'button' && n.props.className.startsWith('map-mark-button'));
      const actual = nodes(render(current, 'city', row)).find(n => n.type === 'button' && n.props.className.startsWith('map-mark-button'));
      const shape = n => ({ className: n.props.className, disabled: Boolean(n.props.disabled), title: n.props.title, label: n.props['aria-label'], icons: iconShape(n) });
      compare('city mark presentation', shape(expected), shape(actual), { language, kind: 'city', input: row });
    }
    // Current-client explicit occupancy epistemic states are intentional extensions.
    assert.equal(helpers.mapResourceStatus({ rebuildGatherOccupancyKnown: false }, t, gameTexts), '—');
    assert.equal(helpers.mapResourceStatus({ rebuildGatherOccupancyKnown: true, rebuildGatherOccupied: true }, t, gameTexts), 'Occupied display');
    for (const disabled of [false, true]) for (const busy of [false, true]) {
      const marker = nodes(render(current, 'city', base, disabled, '', [], busy)).find(n => n.type === 'button' && n.props.className.startsWith('map-mark-button'));
      assert.equal(Boolean(marker.props.disabled), disabled || busy);
    }
  }
} finally { globalThis.Date = RealDate; }
const results = { mode: integrated ? 'current' : 'immutable-baseline', baselineHead: manifest.head, status: failures.length ? 'CHANGES_REQUIRED' : 'ACCEPT_SOURCE_LOCAL', files: { [pagePath]: hash(pages), [helperPath]: hash(read(helperPath)), [originalPath]: hash(source), [indexPath]: hash(index) }, locators, passedAssertions: assertions.length, failedAssertions: failures.length, failures, scope: 'City Resource Monster normal table source/local presentation only; no original pixels or native execution' };
fs.writeFileSync(path.join(here, output), JSON.stringify(results, null, 2) + '\n');
console.log(`LWB317_REVIEW_MAP_BASIC ${results.status} ${assertions.length} pass / ${failures.length} mismatch; ${output}`);
if (integrated && failures.length) process.exitCode = 1;
