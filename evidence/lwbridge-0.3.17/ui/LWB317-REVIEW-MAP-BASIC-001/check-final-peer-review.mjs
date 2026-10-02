import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as helpers from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
import { getMapPreviewProvider } from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser'), { transformSync } = require('esbuild');
const read = p => fs.readFileSync(path.join(repo, p), 'utf8');
const hash = b => crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const originalPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const pagePath = 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx', previewPath = 'src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
const source = read(originalPath), page = read(pagePath), index = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js');
assert.equal(hash(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
assert.equal(hash(index), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
function walk(n, out = []) { if (!n || typeof n !== 'object') return out; if (n.type) out.push(n); for (const v of Object.values(n)) if (Array.isArray(v)) v.forEach(c => walk(c, out)); else if (v && typeof v === 'object') walk(v, out); return out; }
const originalNodes = walk(parse(source, { sourceType: 'module' })), currentNodes = walk(parse(page, { sourceType: 'module', plugins: ['jsx'] })), indexNodes = walk(parse(index, { sourceType: 'module' }));
const fn = (nodes, name) => { const n = nodes.find(n => n.type === 'FunctionDeclaration' && n.id?.name === name); assert.ok(n, name); return n; };
const cut = (s, n) => s.slice(n.start, n.end);
function h(type, props, ...children) { return { type, props: { ...props, children }, key: props?.key }; }
const jsx = { jsx: (type, props, key) => ({ type, props, key }), jsxs: (type, props, key) => ({ type, props, key }) };
function nodes(n, out = []) { if (Array.isArray(n)) n.forEach(c => nodes(c, out)); else if (n && typeof n === 'object') { out.push(n); nodes(n.props?.children, out); } return out; }
function text(n) { return n == null || typeof n === 'boolean' ? '' : Array.isArray(n) ? n.map(text).join('') : typeof n === 'object' ? text(n.props.children) : String(n); }
const originals = new Function(['k', 'et'].map(name => cut(source, fn(originalNodes, name))).join('\n')
  + '\n' + ['Fe', 'Ie'].map(name => cut(index, fn(indexNodes, name))).join('\n') + ';return {k,et,truckState:Ie};')();
const table = cut(page, fn(currentNodes, 'MapTable'));
const coordinate = cut(page, fn(currentNodes, 'coordinateText'));
let language, t;
const injected = { ...helpers, h, useI18n: () => ({ language, t }), useMemo: f => f(), useState: f => [typeof f === 'function' ? f() : f, () => {}], useEffect: () => {}, SCAN_TYPE_LABEL_KEYS: { truck: 'map.truck', railway: 'map.allianceTrain' }, EMPTY_GAME_TEXTS: {} };
const Table = new Function(...Object.keys(injected), coordinate + '\n' + transformSync(table, { loader: 'jsx', jsxFactory: 'h' }).code + ';return MapTable;')(...Object.values(injected));
const column = (tree, name) => nodes(tree).find(n => n.type === 'td' && n.props.className === name);
const summarize = cell => { const b = nodes(cell).find(n => n.type === 'button'); return { text: text(cell), button: !!b, disabled: b ? !!b.props.disabled : null }; };
const cases = [];
for (language of ['en', 'ja']) {
  const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
  t = (key, values = {}) => catalog[key].replace(/\{(\w+)\}/g, (m, k) => String(values[k] ?? m));
  for (const kind of ['truck', 'railway']) for (const marchUuid of [undefined, null, 0, '0', '\t\n ', ' id ']) {
    const row = { serverId: 0, uuid: ' 123 ', recordKey: 'r', marchUuid };
    const jumpingKey = '0:' + String(marchUuid || '').trim();
    const de = new Function('e', 'k', 'A', 'g', 'd', 'f', 're', 'E', 'et', cut(source, fn(originalNodes, 'de')) + ';return de;')(kind, originals.k, k => ['truck', 'railway'].includes(k), t, jumpingKey, false, () => {}, jsx, originals.et);
    const expected = de(row);
    const tree = Table({ kind, rows: [row], sorts: [], selectedKeys: new Set(), actionDisabled: false, actionBusy: false, liveTargetDisabled: false, jumpingKey });
    const expectedSummary = typeof expected === 'string' ? { text: expected, button: false, disabled: null } : { text: text(expected), button: true, disabled: !!expected.props.disabled };
    assert.deepEqual(summarize(column(tree, 'map-column-coordinate')), expectedSummary);
    const canonical = Table({ kind, rows: [row], sorts: [], selectedKeys: new Set(), actionDisabled: false, actionBusy: false, jumpingKey });
    assert.ok(nodes(canonical).filter(n => n.type === 'button' && n.props.className === 'map-coordinate-button').every(n => n.props.disabled === true));
    const ue = new Function('e', 'k', cut(source, fn(originalNodes, 'ue')) + ';return ue;')(kind, originals.k);
    const currentRow = nodes(tree).find(n => n.type === 'tr' && n.props.className === 'map-row');
    assert.equal(currentRow.key, ue(row));
    cases.push({ language, kind, test: 'zero server and normalized march id', marchUuid: marchUuid ?? null, result: expectedSummary });
  }
  for (const row of [{ serverId: 0, uuid: '', ownerName: '', allianceName: '' }, { uuid: '123', ownerName: 0, allianceName: false }, { serverId: undefined, uuid: undefined }, { serverId: 321, uuid: ' 123 ', recordKey: 'other', ownerName: 'Owner' }]) {
    const sourceKey = `${row.serverId}:${String(row.uuid || '').trim()}`;
    const originalCalls = [], currentCalls = [];
    const me = new Function('e', 'o', 'c', 'l', 'g', 'E', 'h', 'ne', 'F', 'P', 'k', 'r', cut(source, fn(originalNodes, 'me')) + ';return me;')('truck', Date.now(), new Set(), new Set([sourceKey]), t, jsx, () => {}, r => originalCalls.push(r), () => {}, () => {}, originals.k, originals.truckState);
    const original = me(row);
    const ue = new Function('e', 'k', cut(source, fn(originalNodes, 'ue')) + ';return ue;')('truck', originals.k);
    const tree = Table({ kind: 'truck', rows: [row], sorts: [], selectedKeys: new Set([ue(row)]), actionDisabled: false, actionBusy: false, onSelect: (key, selectedRow) => currentCalls.push([key, selectedRow]) });
    const current = nodes(tree).find(n => n.type === 'input');
    assert.equal(current.props['aria-label'], original.props['aria-label']);
    assert.equal(current.props.checked, original.props.checked);
    assert.equal(current.props.disabled, original.props.disabled);
    original.props.onChange(); current.props.onChange();
    assert.deepEqual(originalCalls, [row]); assert.deepEqual(currentCalls, [[ue(row), row]]);
    const off = Table({ kind: 'truck', rows: [row], sorts: [], selectedKeys: new Set(), actionDisabled: false, actionBusy: false });
    assert.equal(nodes(off).find(n => n.type === 'input').props.checked, false);
    cases.push({ language, test: 'source label and paired local selection', input: row, label: current.props['aria-label'], key: ue(row) });
  }
  const rewards = Table({ kind: 'truck', rows: [{ uuid: '123', serverId: 321, currentGoods: [] }], sorts: [], selectedKeys: new Set() });
  assert.equal(nodes(rewards).filter(n => n.type === 'td' && n.props.className === 'map-reward-cell').length, 1);
}
const opening = currentNodes.find(n => n.type === 'JSXOpeningElement' && n.name.name === 'MapTable');
const jumpingNode = opening.attributes.find(n => n.name?.name === 'jumpingKey').value.expression;
const resolveKey = new Function('previewFixture', 'previewState', 'previewJumpingKeys', 'tab', 'actionBusy', 'return ' + cut(page, jumpingNode));
for (const mode of ['native', 'native-unavailable', 'preview']) for (const state of ['', 'map-row-actions', 'map-table-states']) {
  const previewFixture = mode === 'preview' && state.startsWith('map-');
  const provider = getMapPreviewProvider(mode, state);
  const key = resolveKey(previewFixture, state, { truck: 'injected' }, 'truck', '');
  assert.equal(key, mode === 'preview' && state === 'map-row-actions' ? 'injected' : '');
  if (mode !== 'preview' || !state.startsWith('map-')) assert.equal(provider, null);
  cases.push({ mode, state, key });
}
const result = { recommendation: 'ACCEPT focused source/local Map row correction', blockers: [], distinguishingCases: cases.length, cases, sourceLocators: Object.fromEntries(['de', 'me', 'ue', 'he'].map(name => { const n = fn(originalNodes, name); return [name, { byte: Buffer.byteLength(source.slice(0, n.start)), expression: cut(source, n) }]; })), finalHashes: { [pagePath]: hash(page), [previewPath]: hash(read(previewPath)), [originalPath]: hash(source) }, limits: ['Canonical native-follow availability fence remains', 'Local generic selectedKeys intentionally pairs with onSelect producer; source uses separate typed key sets', 'No original pixels or native gameplay', 'Broader filters and claim/scheduling branches excluded'] };
fs.writeFileSync(path.join(here, 'peer-review.json'), JSON.stringify(result, null, 2) + '\n');
console.log(`LWB317_FINAL_MAP_PEER_REVIEW_OK ${cases.length} distinguishing cases`);
