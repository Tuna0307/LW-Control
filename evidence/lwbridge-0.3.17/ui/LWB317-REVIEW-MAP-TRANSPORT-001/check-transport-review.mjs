import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as current from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
const { transformSync } = require('esbuild');

const sourcePath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const mainPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js';
const rewardPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/rewardDisplay-eZWrd6iS.js';
const pagePath = 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx';
const source = fs.readFileSync(path.join(repo, sourcePath), 'utf8');
const main = fs.readFileSync(path.join(repo, mainPath), 'utf8');
const rewardSource = fs.readFileSync(path.join(repo, rewardPath), 'utf8');
const pageSource = fs.readFileSync(path.join(repo, pagePath), 'utf8').replace(/\r\n/g, '\n');

const sha256 = value => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
assert.equal(sha256(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
assert.equal(sha256(main), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
assert.equal(sha256(rewardSource), '7F65DD3F5B81C96117AF5E83E8310D6C6A3A48787B1F693D387020255C78E73E');

function walk(node, output = []) {
  if (!node || typeof node !== 'object') return output;
  if (node.type) output.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(child => walk(child, output));
    else if (value && typeof value === 'object') walk(value, output);
  }
  return output;
}

function declaration(ast, name) {
  const node = ast.program.body.map(entry => entry.declaration || entry)
    .find(entry => entry.type === 'FunctionDeclaration' && entry.id?.name === name);
  assert.ok(node, `missing declaration ${name}`);
  return node;
}

function variable(ast, name) {
  const node = ast.program.body.filter(entry => entry.type === 'VariableDeclaration')
    .flatMap(entry => entry.declarations).find(entry => entry.id?.name === name);
  assert.ok(node, `missing variable ${name}`);
  return node;
}

function locator(textValue, node) {
  return {
    utf8ByteOffset: Buffer.byteLength(textValue.slice(0, node.start)),
    expression: textValue.slice(node.start, node.end),
  };
}

function text(tree) {
  if (tree == null || typeof tree === 'boolean') return '';
  if (Array.isArray(tree)) return tree.map(text).join('');
  if (typeof tree === 'object') return text(tree.props?.children);
  return String(tree);
}

function nodes(tree, output = []) {
  if (Array.isArray(tree)) tree.forEach(child => nodes(child, output));
  else if (tree && typeof tree === 'object') {
    output.push(tree);
    nodes(tree.props?.children, output);
  }
  return output;
}

function metadata(columns) {
  return columns.map(column => Object.fromEntries(
    Object.entries(column).filter(([, value]) => typeof value !== 'function' && value !== undefined),
  ));
}

const sourceAst = parse(source, { sourceType: 'module' });
const mainAst = parse(main, { sourceType: 'module' });
const rewardAst = parse(rewardSource, { sourceType: 'module' });
const pageAst = parse(pageSource, { sourceType: 'module', plugins: ['jsx'] });

const sourceHelpers = ['k', 'A', 'j', 'M', 'N', 'Qe', 'I', 'L', 'et', 'Je', 'nt']
  .map(name => declaration(sourceAst, name));
const liveKinds = variable(sourceAst, 'O');
const truckFunctions = ['Fe', 'Ie'].map(name => declaration(mainAst, name));
const rewardCompactNode = declaration(rewardAst, 'e');
const tableNode = variable(sourceAst, 'at');
const innerFunctions = Object.fromEntries(
  walk(tableNode).filter(node => node.type === 'FunctionDeclaration' && ['de', 'fe', 'me'].includes(node.id?.name))
    .map(node => [node.id.name, node]),
);
for (const name of ['de', 'fe', 'me']) assert.ok(innerFunctions[name], `missing original JSX helper ${name}`);

const originalItemFilterProps = walk(sourceAst).find(node => node.type === 'ObjectExpression'
  && node.properties.some(property => property.key?.name === 'label'
    && source.slice(property.value.start, property.value.end) === 'S(`map.itemFilter`)'));
assert.ok(originalItemFilterProps, 'missing original item filter');
const originalItemFilter = originalItemFilterProps.properties.find(property => property.key?.name === 'onChange').value;

const currentTableNode = declaration(pageAst, 'MapTable');
const currentCoordinateNode = declaration(pageAst, 'coordinateText');
const currentItemFilterNode = walk(pageAst).find(node => node.type === 'FunctionDeclaration' && node.id?.name === 'changeItemFilter');
assert.ok(currentItemFilterNode, 'missing current changeItemFilter');
const currentTableCode = transformSync(pageSource.slice(currentTableNode.start, currentTableNode.end), {
  loader: 'jsx', jsxFactory: 'h',
}).code;
const coordinateText = new Function(pageSource.slice(currentCoordinateNode.start, currentCoordinateNode.end) + ';return coordinateText;')();

const now = 1_799_000_000_000;
const RealDate = globalThis.Date;
class FixedDate extends RealDate {
  constructor(...args) { super(...(args.length ? args : [now])); }
  static now() { return now; }
}

const original = new Function('Date',
  sourceHelpers.map(node => source.slice(node.start, node.end)).join('\n')
  + '\n' + truckFunctions.map(node => main.slice(node.start, node.end)).join('\n')
  + '\nconst l=Fe,r=Ie;const ' + source.slice(liveKinds.start, liveKinds.end)
  + ';return {columns:nt,rewardName:M,number:I,quality:L,duration:Je,truckMaximum:Fe,truckState:Ie,A,k,coordinates:et};',
)(FixedDate);
const compact = new Function(rewardSource.slice(rewardCompactNode.start, rewardCompactNode.end) + ';return e;')();

const jsxRuntime = {
  jsx: (type, props) => ({ type, props }),
  jsxs: (type, props) => ({ type, props }),
};
const h = (type, props, ...children) => ({
  type,
  props: { ...(props || {}), ...(children.length ? { children } : {}) },
});

function originalLiveTarget(kind, row, t, disabled = true) {
  return new Function('e', 'k', 'A', 'g', 'd', 'f', 're', 'E', 'et',
    source.slice(innerFunctions.de.start, innerFunctions.de.end) + ';return de;',
  )(kind, original.k, original.A, t, '', disabled, () => {}, jsxRuntime, original.coordinates)(row);
}

function originalTruckSelect(row, t) {
  return new Function('e', 'o', 'c', 'l', 'g', 'E', 'h', 'ne', 'F', 'P', 'k', 'r',
    source.slice(innerFunctions.me.start, innerFunctions.me.end) + ';return me;',
  )('truck', now, new Set(), new Set(), t, jsxRuntime, () => {}, () => {}, () => 'ready', () => 0, original.k, original.truckState)(row);
}

function originalRewards(row, field, itemKey, texts, language) {
  return new Function('k', 'a', 'i', 'oe', 'M', 'I', '_', 'E', 'v',
    source.slice(innerFunctions.fe.start, innerFunctions.fe.end) + ';return fe;',
  )(original.k, itemKey, texts, language, original.rewardName, original.number, compact, jsxRuntime, 'RecoveredAsset')(row, field);
}

function renderCurrentTable(kind, row, t, language, itemKey = '', gameTexts = {}, sorts = []) {
  const effects = [];
  const clearIds = [];
  const table = new Function(
    'helpers', 'h', 'useI18n', 'useMemo', 'useState', 'useEffect', 'coordinateText', 'SCAN_TYPE_LABEL_KEYS', 'EMPTY_GAME_TEXTS', 'window',
    'const {buildMapColumns,mapNumber,mapResourceStatus,mapRewardCount,mapRewardName,mapTaskLabel,mapTaskSelectable,mapTaskState}=helpers;\n'
      + currentTableCode + ';return MapTable;',
  )(
    current,
    h,
    () => ({ language, t }),
    fn => fn(),
    initial => [typeof initial === 'function' ? initial() : initial, () => {}],
    fn => effects.push(fn),
    coordinateText,
    { truck: 'map.truck', railway: 'map.allianceTrain' },
    {},
    { setInterval: () => 91, clearInterval: id => clearIds.push(id) },
  );
  const tree = table({
    kind,
    rows: [row],
    loading: false,
    sorts,
    onSort: () => {},
    onCoordinateJump: () => {},
    onPlayerMark: () => {},
    actionBusy: false,
    actionDisabled: true,
    selectedKeys: new Set(),
    onSelect: () => {},
    gameTexts,
    itemKey,
  });
  for (const effect of effects) {
    const cleanup = effect();
    if (typeof cleanup === 'function') cleanup();
  }
  return { tree, clearIds };
}

const mismatches = [];
const observations = [];
const checks = {
  metadata: 0,
  value: 0,
  truckState: 0,
  eligibility: 0,
  selectionLabel: 0,
  liveTarget: 0,
  rewards: 0,
  compactCounts: 0,
  itemSortMetadata: 0,
  itemFilterCallbacks: 0,
};

function compare(category, caseName, expected, actual, details = {}) {
  const equal = JSON.stringify(actual) === JSON.stringify(expected);
  observations.push({ category, case: caseName, equal, expected, actual, ...details });
  if (!equal) mismatches.push({ category, case: caseName, expected, actual, ...details });
  return equal;
}

function describeRewardTree(tree) {
  if (tree === '-') return '-';
  return nodes(tree).filter(node => node.props?.className === 'map-reward-item').map(node => ({
    title: node.props.title,
    ariaLabel: node.props['aria-label'],
    count: text(nodes(node).find(child => child.type === 'strong')),
  }));
}

function currentRewardCell(tree) {
  const rewardList = nodes(tree).find(node => node.props?.className === 'map-reward-list map-reward-list--retained');
  if (!rewardList) {
    const cells = nodes(tree).filter(node => node.type === 'td');
    return cells.some(cell => text(cell) === '-') ? '-' : [];
  }
  return describeRewardTree(rewardList);
}

globalThis.Date = FixedDate;
try {
  for (const language of ['en', 'ja']) {
    const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
    const t = (key, values = {}) => {
      assert.ok(key in catalog, `${language} missing ${key}`);
      return catalog[key].replace(/\{(\w+)\}/g, (match, keyName) => String(values[keyName] ?? match));
    };
    const gameTexts = { reward_name: language === 'ja' ? '提供名' : 'Supplied Name' };

    for (const kind of ['truck', 'railway']) {
      for (const itemKey of ['', 'item-a']) {
        const expected = original.columns(kind, t, language, gameTexts, itemKey, false);
        const actual = current.buildMapColumns(kind, t, language, gameTexts, itemKey, false);
        compare('metadata', `${language}/${kind}/item=${itemKey || 'none'}`, metadata(expected), metadata(actual));
        checks.metadata++;
        const expectedReward = expected.find(column => column.rewards === 'currentGoods');
        const actualReward = actual.find(column => column.rewards === 'currentGoods');
        compare('item-sort-metadata', `${language}/${kind}/item=${itemKey || 'none'}`, expectedReward.sortBy ?? null, actualReward.sortBy ?? null);
        checks.itemSortMetadata++;
      }
    }

    const truckBase = {
      serverId: 321,
      uuid: '12345',
      marchUuid: 'march-123',
      x: 12,
      y: 34,
      ownerName: 'Owner',
      allianceName: 'Alliance',
      quality: 5,
      power: 1_234_567,
      maxLootCount: 3,
      robTimes: 1,
      arriveTs: now + 60_000,
      protectTime: 0,
      updatedAt: now - 2_000,
      currentGoods: [],
    };
    const truckCases = [
      ['ready', truckBase],
      ['arrival-boundary-minus-one', { ...truckBase, arriveTs: now + 1 }],
      ['arrival-boundary', { ...truckBase, arriveTs: now }],
      ['protection-boundary', { ...truckBase, protectTime: now }],
      ['protected-one-second', { ...truckBase, protectTime: now + 1_000 }],
      ['full-over-protected', { ...truckBase, maxLootCount: 2, robTimes: 2, protectTime: now + 60_000 }],
      ['reindeer-cap', { ...truckBase, isSpecialURQuality: true, maxLootCount: 9, robTimes: 1 }],
      ['missing-count', { ...truckBase, maxLootCount: undefined, robTimes: 8 }],
      ['invalid-count', { ...truckBase, maxLootCount: 'bad', robTimes: 8 }],
      ['missing-uuid', { ...truckBase, uuid: '', ownerName: '', allianceName: '', marchUuid: '' }],
      ['server-zero', { ...truckBase, serverId: 0 }],
      ['server-missing', { ...truckBase, serverId: undefined }],
      ['server-string', { ...truckBase, serverId: '321' }],
    ];

    for (const [caseName, row] of truckCases) {
      const expectedState = original.truckState(row, now);
      const actualState = current.truckState(row, now);
      compare('truck-state', `${language}/${caseName}`, expectedState, actualState);
      checks.truckState++;
      const expectedDisabled = originalTruckSelect(row, t).props.disabled;
      const actualDisabled = !current.mapTaskSelectable('truck', row, now);
      compare('truck-eligibility', `${language}/${caseName}`, expectedDisabled, actualDisabled);
      checks.eligibility++;
      const expectedStatus = original.columns('truck', t, language, gameTexts, '', false)
        .find(column => column.sortBy === 'remainingLootCount').value(row);
      const actualStatus = current.buildMapColumns('truck', t, language, gameTexts, '', false)
        .find(column => column.sortBy === 'remainingLootCount').value(row);
      compare('truck-status-label', `${language}/${caseName}`, expectedStatus, actualStatus, { state: expectedState });
      checks.value++;

      const expectedSelect = originalTruckSelect(row, t);
      const rendered = renderCurrentTable('truck', row, t, language, '', gameTexts);
      const currentSelect = nodes(rendered.tree).find(node => node.type === 'input' && node.props?.type === 'checkbox');
      compare('truck-selection-label', `${language}/${caseName}`, expectedSelect.props['aria-label'], currentSelect.props['aria-label'], {
        expectedDisabled: expectedSelect.props.disabled,
        actualDisabled: currentSelect.props.disabled,
      });
      checks.selectionLabel++;
    }

    const columnRows = {
      truck: [
        truckBase,
        { ...truckBase, ownerName: '', allianceName: 'Fallback Alliance', isSpecialURQuality: true, quality: 1, power: 'bad', arriveTs: Math.floor((now + 120_000) / 1000), updatedAt: Math.floor((now - 5_000) / 1000) },
        { ...truckBase, ownerName: '', allianceName: '', quality: 0, power: null, arriveTs: 0, updatedAt: 0 },
      ],
      railway: [
        { serverId: 321, uuid: 'train-1', marchUuid: 'rail-march', x: 21, y: 43, allianceName: 'Alliance Name', allianceAbbr: 'ABR', quality: 6, power: 9_876_543, currentGoods: [], protectTime: now + 60_000, updatedAt: now - 3_000 },
        { serverId: 321, uuid: 'train-2', marchUuid: '', x: 22, y: 44, allianceName: '', allianceAbbr: 'ABR', quality: 4, power: 'bad', currentGoods: [], protectTime: Math.floor((now + 120_000) / 1000), updatedAt: Math.floor((now - 7_000) / 1000) },
        { serverId: 321, uuid: 'train-3', marchUuid: '', x: 23, y: 45, allianceName: '', allianceAbbr: '', quality: 0, power: null, currentGoods: [], protectTime: 0, updatedAt: 0 },
      ],
    };
    for (const kind of ['truck', 'railway']) {
      const expectedColumns = original.columns(kind, t, language, gameTexts, '', false);
      const actualColumns = current.buildMapColumns(kind, t, language, gameTexts, '', false);
      for (const [rowIndex, row] of columnRows[kind].entries()) {
        for (let index = 0; index < expectedColumns.length; index++) {
          if (!expectedColumns[index].value) continue;
          compare('column-value', `${language}/${kind}/row${rowIndex}/${expectedColumns[index].label}`, expectedColumns[index].value(row), actualColumns[index].value(row));
          checks.value++;
        }
      }
    }

    for (const kind of ['truck', 'railway']) {
      for (const row of [
        { ...columnRows[kind][0], marchUuid: '' },
        { ...columnRows[kind][0], marchUuid: 'live-march' },
      ]) {
        const caseName = row.marchUuid ? 'with-march' : 'missing-march';
        const expectedTarget = originalLiveTarget(kind, row, t, true);
        const rendered = renderCurrentTable(kind, row, t, language, '', gameTexts);
        const currentCoordinateCell = nodes(rendered.tree).filter(node => node.type === 'td')[kind === 'truck' ? 1 : 0];
        const expectedDescription = typeof expectedTarget === 'string'
          ? { text: expectedTarget, button: false, disabled: null }
          : { text: text(expectedTarget), button: true, disabled: expectedTarget.props.disabled };
        const targetButton = nodes(currentCoordinateCell).find(node => node.type === 'button' && node.props?.className === 'map-coordinate-button');
        const actualDescription = targetButton
          ? { text: text(targetButton), button: true, disabled: targetButton.props.disabled }
          : { text: text(currentCoordinateCell), button: false, disabled: null };
        compare('live-target', `${language}/${kind}/${caseName}`, expectedDescription, actualDescription);
        checks.liveTarget++;
      }
    }

    const rewardRows = [
      {
        ...truckBase,
        currentGoods: [
          { key: 'item-b', name: 'Beta', count: 999 },
          { key: 'item-a', nameKey: 'reward_name', name: 'Fallback A', count: 1000 },
          { key: 'item-c', count: 1_000_000 },
        ],
      },
      { ...truckBase, currentGoods: [] },
    ];
    for (const kind of ['truck', 'railway']) {
      const populated = kind === 'truck' ? rewardRows[0] : { ...columnRows.railway[0], currentGoods: rewardRows[0].currentGoods };
      const empty = kind === 'truck' ? rewardRows[1] : { ...columnRows.railway[0], currentGoods: [] };
      for (const itemKey of ['', 'item-a']) {
        const expectedReward = describeRewardTree(originalRewards(populated, 'currentGoods', itemKey, gameTexts, language));
        const actualReward = currentRewardCell(renderCurrentTable(kind, populated, t, language, itemKey, gameTexts).tree);
        compare('retained-goods', `${language}/${kind}/item=${itemKey || 'none'}`, expectedReward, actualReward);
        checks.rewards++;
      }
      const expectedEmpty = originalRewards(empty, 'currentGoods', '', gameTexts, language);
      const renderedEmpty = renderCurrentTable(kind, empty, t, language, '', gameTexts).tree;
      const rewardColumnIndex = original.columns(kind, t, language, gameTexts, '', false).findIndex(column => column.rewards === 'currentGoods');
      const actualEmpty = text(nodes(renderedEmpty).filter(node => node.type === 'td')[rewardColumnIndex]);
      compare('retained-goods-empty', `${language}/${kind}`, expectedEmpty, actualEmpty);
      checks.rewards++;
    }

    for (const value of [undefined, null, 0, -1, 999, 1000, 1250, 999_999, 1_000_000, 1_000_000_000, -1234, 'bad']) {
      compare('compact-count', `${language}/${String(value)}`, compact(value), current.mapRewardCount(value));
      checks.compactCounts++;
    }
  }

  for (const kind of ['truck', 'railway']) {
    for (const value of ['', 'item-a']) {
      const initial = {
        truck: [{ sortBy: 'itemCount', sortOrder: 'desc' }, { sortBy: 'updatedAt', sortOrder: 'desc' }],
        railway: [{ sortBy: 'itemCount', sortOrder: 'desc' }, { sortBy: 'updatedAt', sortOrder: 'desc' }],
      };
      let expectedSorts = structuredClone(initial);
      let actualSorts = structuredClone(initial);
      let expectedKey;
      let actualKey;
      let expectedPage;
      let actualPage;
      const expectedCallback = new Function('L', 'Jt', 'zt', 'Yt', 'V',
        'return (' + source.slice(originalItemFilter.start, originalItemFilter.end) + ');',
      )(
        kind,
        expectedSorts,
        fn => { expectedKey = fn({ [kind]: 'old' })[kind]; },
        fn => { expectedSorts = fn(expectedSorts); },
        page => { expectedPage = page; },
      );
      const actualCallback = new Function('tab', 'setItemKey', 'setSortsByKind', 'setPage',
        pageSource.slice(currentItemFilterNode.start, currentItemFilterNode.end) + ';return changeItemFilter;',
      )(
        kind,
        next => { actualKey = next; },
        fn => { actualSorts = fn(actualSorts); },
        page => { actualPage = page; },
      );
      expectedCallback(value);
      actualCallback(value);
      compare('item-filter-callback', `${kind}/value=${value || 'empty'}`, {
        key: expectedKey,
        sorts: expectedSorts,
        page: expectedPage,
      }, {
        key: actualKey || undefined,
        sorts: actualSorts,
        page: actualPage,
      });
      checks.itemFilterCallbacks++;
    }
  }
} finally {
  globalThis.Date = RealDate;
}

const report = {
  task: 'LWB317-REVIEW-MAP-TRANSPORT-001',
  fixedTime: now,
  languages: ['en', 'ja'],
  result: mismatches.length ? 'CHANGES_REQUIRED' : 'ACCEPT',
  checks,
  mismatchCount: mismatches.length,
  mismatches,
  observations,
  source: {
    mapPanel: {
      path: sourcePath,
      sha256: sha256(source),
      columnFactory: locator(source, declaration(sourceAst, 'nt')),
      tableRenderer: locator(source, tableNode),
      liveTargetRenderer: locator(source, innerFunctions.de),
      rewardRenderer: locator(source, innerFunctions.fe),
      selectionRenderer: locator(source, innerFunctions.me),
      itemFilter: locator(source, originalItemFilter),
    },
    main: { path: mainPath, sha256: sha256(main), truckMaximum: locator(main, declaration(mainAst, 'Fe')), truckState: locator(main, declaration(mainAst, 'Ie')) },
    rewards: { path: rewardPath, sha256: sha256(rewardSource), compactCount: locator(rewardSource, rewardCompactNode) },
  },
  current: {
    page: { path: pagePath, mapTable: locator(pageSource, currentTableNode), itemFilter: locator(pageSource, currentItemFilterNode) },
    presentationPath: 'src/LWBridge.UI-0.3.17/src/mapTablePresentation.js',
  },
  limits: [
    'Source/local execution only; no original post-auth pixel comparison.',
    'Current native reward images are placeholders; this checker compares retained-goods order/count/name/title/aria behavior.',
    'Shared Truck/Train item-filter state and broader query/filter ownership are recorded separately from this focused callback check.',
  ],
};

const recordPath = path.join(here, 'transport-review-results.json');
if (process.argv.includes('--record')) fs.writeFileSync(recordPath, JSON.stringify(report, null, 2) + '\n');
if (process.argv.includes('--verify-record')) {
  assert.deepEqual(JSON.parse(fs.readFileSync(recordPath, 'utf8')), JSON.parse(JSON.stringify(report)));
}
console.log(JSON.stringify({ result: report.result, mismatchCount: report.mismatchCount, checks: report.checks }, null, 2));
