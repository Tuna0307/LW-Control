import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createOriginalHarness, optionsReply } from '../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs';
import { createHarness, treeNodes, nodeText, repo } from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
import { treasureName } from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.join(repo, 'src/LWBridge.UI-0.3.17/src');
const require = createRequire(path.join(src, '../package.json'));
const { transformSync } = require('esbuild');
const pickerSource = fs.readFileSync(path.join(src, 'MapTreasureTypeFilter.jsx'), 'utf8');
const code = transformSync(pickerSource.replace(/^import .*;\r?$/gm, '').replace('export function', 'function'), { loader: 'jsx', jsxFactory: 'h' }).code;
const cases = [];
const items = [
  { key: 2, treasureType: 2, suppliesType: 0, count: 4 },
  { key: '2', treasureType: 999, suppliesType: 0, treasureNameKey: 'my_name', count: 0 },
  { key: 0, treasureType: 0, suppliesType: 1, count: 0 },
  { key: 'trial', treasureType: 99, suppliesType: 0, treasureNameKey: 'challenge_zombie_box_title', count: null },
  { key: 'unknown', treasureType: 'NaN', treasureNameKey: ' ', count: undefined },
];
const gameTexts = { my_name: 'Recovered label', season_s2_ice_supplies_11: 'Ice inventory' };
function shape(node) {
  if (Array.isArray(node)) return node.filter(n => n != null && typeof n !== 'boolean').map(shape).flat();
  if (node == null || typeof node === 'boolean') return [];
  if (typeof node !== 'object') return String(node);
  const children = shape(node.props.children);
  return { type: node.type, attrs: Object.fromEntries(['className', 'type', 'aria-label', 'title'].filter(k => node.props[k] !== undefined).map(k => [k, node.props[k]])), children: Array.isArray(children) ? children : [children] };
}
function canonical(t, props) {
  const refs = [];
  const h = (type, attrs, ...children) => ({ type, props: { ...(attrs || {}), children } });
  const component = new Function('useRef', 'useI18n', 'treasureName', 'h', code + '\nreturn MapTreasureTypeFilter;')(
    () => { const ref = { current: null }; refs.push(ref); return ref; }, () => ({ t }), treasureName, h,
  );
  return component(props);
}
for (const language of ['en', 'ja']) {
  const messages = (await import(pathToFileURL(path.join(src, 'locales/' + language + '.js')).href)).default;
  const t = (key, values = {}) => (messages[key] || key).replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? `{${name}}`));
  for (const value of ['', 2, '2', 0, 'trial', 'unknown', 'missing']) {
    for (const choices of [[], items]) {
      const o = await createOriginalHarness({ online: false, initialTab: 'treasure', tabMode: 'controlled', translate: t,
        stubs: { dataOptions: { mode: 'auto', value: optionsReply({ treasureTypes: choices }) } } });
      await o.mount();
      const originalChild = o.findNodes(n => typeof n.type === 'function' && n.type.name === 'lt')[0];
      assert.ok(originalChild);
      const originalTrace = [], currentTrace = [];
      const props = { items: choices, value, gameTexts };
      const original = originalChild.type({ ...props, onChange: key => originalTrace.push(['change', key]) });
      const current = canonical(t, { ...props, onChange: key => currentTrace.push(['change', key]) });
      assert.deepEqual(shape(current), shape(original), `render ${language} / ${JSON.stringify(value)} / ${choices.length}`);
      original.props.ref.current = { removeAttribute: attr => originalTrace.push(['remove', attr]) };
      current.props.ref.current = { removeAttribute: attr => currentTrace.push(['remove', attr]) };
      const ob = treeNodes(original).filter(n => n.type === 'button');
      const cb = treeNodes(current).filter(n => n.type === 'button');
      for (let i = 0; i < cb.length; i++) {
        ob[i].props.onClick(); cb[i].props.onClick();
        assert.deepEqual(currentTrace, originalTrace, 'strict key and change-before-close');
      }
      cases.push({ language, value, options: choices.length, buttons: cb.length, pass: true });
      await o.unmount();
    }
  }
}

// Actual page parent callback: selecting a numeric key resets page and projects
// the source type fields. Selecting All removes them. No native operation.
const page = fs.readFileSync(path.join(src, 'MapDataPage.jsx'), 'utf8');
const h = await createHarness(page, 'picker-parent', { props: { previewState: 'map-treasure' }, dataOptions: optionsReply({ treasureTypes: items })(321) });
await h.mount(); await h.setPage(3);
const child = () => h.findNodes(n => typeof n.type === 'function' && n.type.name === 'MapTreasureTypeFilter')[0];
assert.ok(child());
child().props.onChange(2); await h.settle();
assert.equal(h.getState('treasureType'), 2); assert.equal(h.getState('page'), 1);
assert.equal(h.currentRequest().query.treasureType, 2); assert.equal(h.currentRequest().query.suppliesType, 0);
child().props.onChange('2'); await h.settle();
assert.equal(h.getState('treasureType'), '2'); assert.equal(h.currentRequest().query.treasureType, 999);
child().props.onChange(''); await h.settle();
assert.equal(h.currentRequest().query.treasureType, undefined);
assert.equal(h.currentRequest().query.includeForeignRadarTreasures, false);
assert.equal(h.currentRequest().query.luckyFirst, true);
await h.unmount();

const baseline = execFileSync('git', ['show', 'ff4ed369:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'], { cwd: repo, encoding: 'utf8' });
const old = await createHarness(baseline, 'picker-baseline', { props: { previewState: 'map-treasure' }, dataOptions: optionsReply({ treasureTypes: items })(321) });
await old.mount();
assert.ok(old.findNodes(n => n.type === 'select' && n.props['aria-label'] === 'map.treasureType').length);
assert.equal(old.findNodes(n => typeof n.type === 'function' && n.type.name === 'MapTreasureTypeFilter').length, 0);
await old.unmount();
const result = { comparisons: cases.length, cases, parentCallback: 'PASS', baseline: 'native select differs from source details/menu',
  sourceSha256: crypto.createHash('sha256').update(pickerSource.replace(/\r\n/g, '\n')).digest('hex') };
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'picker-results.json'), JSON.stringify(result, null, 2) + '\n');
console.log('LWB317_MAP_TREASURE_PICKER_OK comparisons=' + cases.length + ' parent=PASS baselineMismatch=YES');
