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
const productionHelperPath = 'src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
const productionPagePath = 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx';
const source = fs.readFileSync(path.join(repo, sourcePath), 'utf8');
const productionHelperSource = fs.readFileSync(path.join(repo, productionHelperPath), 'utf8').replace(/\r\n/g, '\n');
const productionPageSource = fs.readFileSync(path.join(repo, productionPagePath), 'utf8').replace(/\r\n/g, '\n');
const sha256 = value => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();

function stringLocator(textValue, expression) {
  const index = textValue.indexOf(expression);
  assert.ok(index >= 0, `missing source expression: ${expression}`);
  return { utf8ByteOffset: Buffer.byteLength(textValue.slice(0, index)), expression };
}

assert.equal(sha256(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');

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
  const matches = walk(ast).filter(node => node.type === 'FunctionDeclaration' && node.id?.name === name);
  assert.equal(matches.length, 1, `expected one function ${name}, found ${matches.length}`);
  return matches[0];
}

function variable(ast, name) {
  const matches = ast.program.body
    .filter(node => node.type === 'VariableDeclaration')
    .flatMap(node => node.declarations)
    .filter(node => node.id?.name === name);
  assert.equal(matches.length, 1, `expected one variable ${name}, found ${matches.length}`);
  return matches[0];
}

function locator(text, node) {
  return {
    utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)),
    expression: text.slice(node.start, node.end),
  };
}

const sourceAst = parse(source, { sourceType: 'module' });
const productionHelperAst = parse(productionHelperSource, { sourceType: 'module' });
const productionPageAst = parse(productionPageSource, { sourceType: 'module', plugins: ['jsx'] });

// These are the recovered declarations actually used by the original Map table.
const helperNames = ['k', 'A', 'j', 'M', 'De', 'Oe', 'ke', 'w', 'T', 'Ke', 'qe', 'Je', 'N', 'P', 'F', 'Qe', '$e', 'I', 'L', 'et', 'nt'];
const helperNodes = Object.fromEntries(helperNames.map(name => [name, declaration(sourceAst, name)]));
const liveKinds = variable(sourceAst, 'O');
const treasureTypes = variable(sourceAst, 'Ee');
const originalSelectNode = declaration(sourceAst, 'me');
const originalStatusNode = declaration(sourceAst, 'pe');
const ntCalls = walk(sourceAst).filter(node => node.type === 'CallExpression' && node.callee?.type === 'Identifier' && node.callee.name === 'nt');
assert.ok(ntCalls.length >= 1, 'expected original nt call');

const now = 1_799_000_000_000;
const RealDate = globalThis.Date;
class FixedDate extends RealDate {
  constructor(...args) { super(...(args.length ? args : [now])); }
  static now() { return now; }
}

const original = new Function(
  'Date',
  helperNames.map(name => source.slice(helperNodes[name].start, helperNodes[name].end)).join('\n')
    + `\nconst ${source.slice(liveKinds.start, liveKinds.end)};`
    + `\nconst ${source.slice(treasureTypes.start, treasureTypes.end)};`
    + '\nreturn { columns: nt, taskState: F, timestamp: P };',
)(FixedDate);

const jsx = {
  jsx: (type, props) => ({ type, props: props || {} }),
  jsxs: (type, props) => ({ type, props: props || {} }),
};

const originalSelect = kind => new Function(
  'e', 'o', 'c', 'l', 'g', 'E', 'h', 'ne', 'F', 'P', 'k', 'r',
  source.slice(originalSelectNode.start, originalSelectNode.end) + ';return me;',
)(kind, now, new Set(), new Set(), key => key, jsx, () => {}, () => {}, original.taskState, original.timestamp, (row, key) => row[key], () => 'ready');

const originalStatus = t => new Function(
  'o', 'g', 'E', 'F',
  source.slice(originalStatusNode.start, originalStatusNode.end) + ';return pe;',
)(now, t, jsx, original.taskState);

function h(type, props, ...children) {
  return { type, props: { ...(props || {}), ...(children.length ? { children } : {}) } };
}

function text(tree) {
  if (tree == null || typeof tree === 'boolean') return '';
  if (Array.isArray(tree)) return tree.map(text).join('');
  if (typeof tree === 'object') return text(tree.props?.children);
  return String(tree);
}

function nodes(tree, out = []) {
  if (Array.isArray(tree)) tree.forEach(item => nodes(item, out));
  else if (tree && typeof tree === 'object') {
    out.push(tree);
    nodes(tree.props?.children, out);
  }
  return out;
}

const mapTableNode = declaration(productionPageAst, 'MapTable');
const coordinateTextNode = declaration(productionPageAst, 'coordinateText');
const mapTableCall = walk(productionPageAst).find(node => node.type === 'JSXElement' && node.openingElement?.name?.name === 'MapTable');
assert.ok(mapTableCall, 'production MapTable call');
const mapTableCode = transformSync(productionPageSource.slice(mapTableNode.start, mapTableNode.end), { loader: 'jsx', jsxFactory: 'h' }).code;
const coordinateText = new Function(productionPageSource.slice(coordinateTextNode.start, coordinateTextNode.end) + ';return coordinateText;')();

const scanTypeLabels = { dispatch: 'map.secretTask', ghost: 'map.ghostScout', treasure: 'map.treasure' };

function renderTable(kind, row, language, t, gameTexts = {}, treasureStatesRefreshing = false) {
  const effects = [];
  const Table = new Function(
    'helpers', 'h', 'useI18n', 'useMemo', 'useState', 'useEffect', 'coordinateText', 'SCAN_TYPE_LABEL_KEYS', 'EMPTY_GAME_TEXTS', 'window',
    'const {buildMapColumns,mapNumber,mapResourceStatus,mapRewardCount,mapRewardName,mapTaskLabel,mapTaskSelectable,mapTaskState}=helpers;\n'
      + mapTableCode + ';return MapTable;',
  )(
    current,
    h,
    () => ({ language, t }),
    fn => fn(),
    initial => [typeof initial === 'function' ? initial() : initial, () => {}],
    effect => effects.push(effect),
    coordinateText,
    scanTypeLabels,
    {},
    { setInterval: () => 1, clearInterval: () => {} },
  );
  const tree = Table({
    kind,
    rows: [row],
    loading: false,
    sorts: [],
    onSort: () => {},
    onCoordinateJump: () => {},
    onPlayerMark: () => {},
    actionBusy: false,
    actionDisabled: true,
    selectedKeys: new Set(),
    onSelect: () => {},
    gameTexts,
    itemKey: '',
    treasureStatesRefreshing,
  });
  return { tree, flat: nodes(tree), effects };
}

function metadata(columns) {
  return columns.map(column => Object.fromEntries(Object.entries(column).filter(([, value]) => typeof value !== 'function' && value !== undefined)));
}

async function translator(language) {
  const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
  return (key, values = {}) => {
    assert.ok(key in catalog, `${language} catalog missing ${key}`);
    return catalog[key].replace(/\{(\w+)\}/g, (match, token) => String(values[token] ?? match));
  };
}

const baseTask = {
  serverId: 321,
  recordKey: 'review-task',
  uuid: '12345',
  x: 410,
  y: 520,
  ownerName: 'Review Owner',
  level: 6,
  quality: 2,
  rewards: [],
  completionTime: now - 10_000,
  plunderAt: now - 5_000,
  taskExpireTime: now + 60_000,
  maxStealCount: 2,
  stolenCount: 0,
  updatedAt: now,
};

const taskInputs = [
  { id: 'pending-missing-completion', row: { ...baseTask, completionTime: 0 } },
  { id: 'pending-future-completion', row: { ...baseTask, completionTime: now + 1 } },
  { id: 'expired-boundary-over-full', row: { ...baseTask, taskExpireTime: now, completionTime: now + 1, stolenCount: 2 } },
  { id: 'full-over-pending', row: { ...baseTask, taskExpireTime: now + 1, completionTime: now + 1, stolenCount: 2 } },
  { id: 'protected-completion-boundary', row: { ...baseTask, completionTime: now, plunderAt: now + 1 } },
  { id: 'ready-plunder-boundary', row: { ...baseTask, completionTime: now - 1, plunderAt: now } },
  { id: 'ready-missing-plunder-disabled', row: { ...baseTask, plunderAt: 0 } },
  { id: 'ready-seconds-normalized', row: { ...baseTask, completionTime: (now - 2_000) / 1000, plunderAt: (now - 1_000) / 1000, taskExpireTime: (now + 1_000) / 1000 } },
  { id: 'ready-invalid-alpha-uuid', row: { ...baseTask, uuid: '12abc' } },
  { id: 'ready-empty-uuid', row: { ...baseTask, uuid: '' } },
  { id: 'ready-leading-zero-numeric-uuid', row: { ...baseTask, uuid: '000123' } },
  { id: 'ready-numeric-uuid-value', row: { ...baseTask, uuid: 123 } },
  { id: 'ready-missing-counts', row: { ...baseTask, maxStealCount: undefined, stolenCount: undefined } },
  { id: 'ready-invalid-expiry', row: { ...baseTask, taskExpireTime: 'not-a-time' } },
];

const baseTreasure = {
  serverId: 321,
  recordKey: 'review-treasure',
  uuid: 'treasure-review',
  x: 430,
  y: 540,
  treasureType: 1,
  suppliesType: 0,
  remainingBoxes: 3,
  worldClaimState: 'claimable',
  chargePercent: 0.375,
  playerClaimState: 'unclaimed',
  claimBlockReason: '',
  rewardedCount: 1,
  diggingCount: 2,
  expireTime: now + 60_000,
  ownerName: 'Treasure Owner',
  allianceAbbr: 'QA',
  updatedAt: now,
};

const worldInputs = [
  { id: 'charging-rounds-percentage', row: { ...baseTreasure, worldClaimState: 'charging', chargePercent: 0.375 }, refreshing: false },
  { id: 'charging-invalid-percentage', row: { ...baseTreasure, worldClaimState: 'charging', chargePercent: 'bad' }, refreshing: false },
  { id: 'claimable', row: { ...baseTreasure, worldClaimState: 'claimable' }, refreshing: false },
  { id: 'depleted', row: { ...baseTreasure, worldClaimState: 'depleted' }, refreshing: false },
  { id: 'expired', row: { ...baseTreasure, worldClaimState: 'expired' }, refreshing: false },
  { id: 'verifying-explicit', row: { ...baseTreasure, worldClaimState: 'verifying' }, refreshing: false },
  { id: 'unknown-enum', row: { ...baseTreasure, worldClaimState: 'mystery' }, refreshing: false },
  { id: 'missing-state-not-refreshing', row: { ...baseTreasure, worldClaimState: '' }, refreshing: false },
  { id: 'missing-state-refreshing', row: { ...baseTreasure, worldClaimState: '' }, refreshing: true },
];

const playerInputs = [
  { id: 'claimed-over-foreign', row: { ...baseTreasure, playerClaimState: 'claimed', claimBlockReason: 'other_alliance' }, refreshing: false },
  { id: 'claimed-over-no-scout', row: { ...baseTreasure, playerClaimState: 'claimed', claimBlockReason: 'no_scout' }, refreshing: false },
  { id: 'foreign-over-unclaimed', row: { ...baseTreasure, playerClaimState: 'unclaimed', claimBlockReason: 'other_alliance' }, refreshing: false },
  { id: 'foreign-over-failed', row: { ...baseTreasure, playerClaimState: 'failed', claimBlockReason: 'other_alliance' }, refreshing: false },
  { id: 'no-scout', row: { ...baseTreasure, playerClaimState: 'unclaimed', claimBlockReason: 'no_scout' }, refreshing: false },
  { id: 'no-squad', row: { ...baseTreasure, playerClaimState: 'unclaimed', claimBlockReason: 'no_squad' }, refreshing: false },
  { id: 'squad-reserved', row: { ...baseTreasure, playerClaimState: 'unclaimed', claimBlockReason: 'squad_reserved' }, refreshing: false },
  { id: 'failed-falls-back-unclaimed', row: { ...baseTreasure, playerClaimState: 'failed', claimBlockReason: '' }, refreshing: false },
  { id: 'unknown-player-state', row: { ...baseTreasure, playerClaimState: 'mystery', claimBlockReason: '' }, refreshing: false },
  { id: 'missing-player-state', row: { ...baseTreasure, playerClaimState: '', claimBlockReason: '' }, refreshing: false },
  { id: 'missing-player-state-refreshing', row: { ...baseTreasure, playerClaimState: '', claimBlockReason: '' }, refreshing: true },
];

const nameInputs = [
  { id: 'known-radar-type', row: { ...baseTreasure, treasureType: 1, suppliesType: 0, treasureNameKey: '' }, texts: {} },
  { id: 'unknown-type', row: { ...baseTreasure, treasureType: 999, suppliesType: 0, treasureNameKey: '' }, texts: {} },
  { id: 'ice-supplies-fallback', row: { ...baseTreasure, suppliesType: 1, treasureNameKey: '' }, texts: {} },
  { id: 'desert-supplies-fallback', row: { ...baseTreasure, suppliesType: 3, treasureNameKey: '' }, texts: {} },
  { id: 'lucky-cat-supplies-fallback', row: { ...baseTreasure, suppliesType: 4, treasureNameKey: '' }, texts: {} },
  { id: 'trial-gift-fallback', row: { ...baseTreasure, treasureType: 999, suppliesType: 0, treasureNameKey: 'challenge_zombie_box_title' }, texts: {} },
  { id: 'supplied-name', row: { ...baseTreasure, treasureType: 999, suppliesType: 0, treasureNameKey: 'custom_treasure_name' }, texts: { custom_treasure_name: 'Supplied Treasure Name' } },
  { id: 'same-key-is-missing', row: { ...baseTreasure, treasureType: 999, suppliesType: 0, treasureNameKey: 'custom_treasure_name' }, texts: { custom_treasure_name: 'custom_treasure_name' } },
  { id: 'supplies-key-precedes-row-name', row: { ...baseTreasure, suppliesType: 1, treasureNameKey: 'custom_treasure_name' }, texts: { custom_treasure_name: 'Should not win' } },
];

const taskResults = [];
const treasureResults = { world: [], player: [], names: [] };
const renderResults = [];
const timestampResults = [];

globalThis.Date = FixedDate;
try {
  for (const value of [0, null, undefined, 'bad', now, now / 1000, (now + 1_000) / 1000]) {
    const expected = original.timestamp(value);
    const actual = current.mapTimestamp(value);
    assert.equal(actual, expected, `timestamp ${String(value)}`);
    timestampResults.push({ input: value ?? null, expected, actual });
  }

  for (const language of ['en', 'ja']) {
    const t = await translator(language);

    for (const kind of ['dispatch', 'ghost']) {
      const expectedColumns = original.columns(kind, t, language, {}, '', false);
      const actualColumns = current.buildMapColumns(kind, t, language, {}, '', false);
      assert.deepEqual(metadata(actualColumns), metadata(expectedColumns), `${language}/${kind} column metadata`);

      for (const input of taskInputs) {
        const sourceState = original.taskState(input.row, now);
        const currentState = current.mapTaskState(input.row, now);
        assert.equal(currentState, sourceState, `${language}/${kind}/${input.id} state`);

        const sourceStatus = originalStatus(t)(input.row);
        const sourceLabel = text(sourceStatus);
        const sourceClass = sourceStatus.props.className;
        const currentLabel = current.mapTaskLabel(currentState, t);
        assert.equal(currentLabel, sourceLabel, `${language}/${kind}/${input.id} label`);

        const sourceCheckbox = originalSelect(kind)(input.row);
        const sourceDisabled = sourceCheckbox.props.disabled === true;
        const currentDisabled = !current.mapTaskSelectable(kind, input.row, now);
        assert.equal(currentDisabled, sourceDisabled, `${language}/${kind}/${input.id} selection`);

        const rendered = renderTable(kind, input.row, language, t);
        const statusNode = rendered.flat.find(node => String(node.props?.className || '').startsWith('map-task-status '));
        const checkbox = rendered.flat.find(node => node.type === 'input' && node.props?.type === 'checkbox');
        assert.ok(statusNode, `${language}/${kind}/${input.id} rendered status`);
        assert.ok(checkbox, `${language}/${kind}/${input.id} rendered checkbox`);
        assert.equal(statusNode.props.className, sourceClass, `${language}/${kind}/${input.id} rendered class`);
        assert.equal(text(statusNode), sourceLabel, `${language}/${kind}/${input.id} rendered label`);
        assert.equal(checkbox.props.disabled === true, sourceDisabled, `${language}/${kind}/${input.id} rendered selection`);

        if (language === 'en') taskResults.push({
          kind,
          id: input.id,
          state: currentState,
          label: currentLabel,
          className: statusNode.props.className,
          disabled: currentDisabled,
        });
      }

      const rendered = renderTable(kind, baseTask, language, t);
      const headers = rendered.flat.filter(node => node.type === 'th').map(node => text(node));
      const expectedHeaders = expectedColumns.map(column => column.label);
      assert.deepEqual(headers, expectedHeaders, `${language}/${kind} rendered headers`);
      renderResults.push({ language, kind, headers, headerClasses: rendered.flat.filter(node => node.type === 'th').map(node => node.props.className || '') });
    }

    for (const refreshing of [false, true]) {
      const expectedColumns = original.columns('treasure', t, language, {}, '', refreshing);
      const actualColumns = current.buildMapColumns('treasure', t, language, {}, '', refreshing);
      assert.deepEqual(metadata(actualColumns), metadata(expectedColumns), `${language}/treasure/${refreshing} column metadata`);
    }

    for (const input of worldInputs) {
      const expected = original.columns('treasure', t, language, {}, '', input.refreshing)[3].value(input.row);
      const actual = current.buildMapColumns('treasure', t, language, {}, '', input.refreshing)[3].value(input.row);
      assert.equal(actual, expected, `${language}/treasure/world/${input.id}`);
      if (language === 'en') treasureResults.world.push({ id: input.id, value: actual, refreshing: input.refreshing });
    }

    for (const input of playerInputs) {
      const expected = original.columns('treasure', t, language, {}, '', input.refreshing)[4].value(input.row);
      const actual = current.buildMapColumns('treasure', t, language, {}, '', input.refreshing)[4].value(input.row);
      assert.equal(actual, expected, `${language}/treasure/player/${input.id}`);
      if (language === 'en') treasureResults.player.push({ id: input.id, value: actual, refreshing: input.refreshing });
    }

    for (const input of nameInputs) {
      const expected = original.columns('treasure', t, language, input.texts, '', false)[1].value(input.row);
      const actual = current.buildMapColumns('treasure', t, language, input.texts, '', false)[1].value(input.row);
      assert.equal(actual, expected, `${language}/treasure/name/${input.id}`);
      if (language === 'en') treasureResults.names.push({ id: input.id, value: actual });
    }

    const treasureRow = { ...baseTreasure, worldClaimState: 'charging', chargePercent: 0.375, playerClaimState: 'claimed', claimBlockReason: 'other_alliance' };
    const renderedTreasure = renderTable('treasure', treasureRow, language, t);
    const treasureColumns = original.columns('treasure', t, language, {}, '', false);
    const treasureHeaders = renderedTreasure.flat.filter(node => node.type === 'th').map(node => text(node));
    assert.deepEqual(treasureHeaders, treasureColumns.map(column => column.label), `${language}/treasure rendered headers`);
    const scheduleButton = renderedTreasure.flat.find(node => node.type === 'button' && node.props?.className === 'map-schedule-button');
    assert.ok(scheduleButton && scheduleButton.props.disabled === true, `${language}/treasure action disabled`);
    const cells = renderedTreasure.flat.filter(node => node.type === 'td').map(node => text(node));
    assert.equal(cells[3], current.buildMapColumns('treasure', t, language, {}, '', false)[3].value(treasureRow));
    assert.equal(cells[4], current.buildMapColumns('treasure', t, language, {}, '', false)[4].value(treasureRow));
    renderResults.push({ language, kind: 'treasure', headers: treasureHeaders, world: cells[3], player: cells[4], actionDisabled: true });
  }
} finally {
  globalThis.Date = RealDate;
}

const productionMapTaskState = declaration(productionHelperAst, 'mapTaskState');
const productionMapTaskSelectable = declaration(productionHelperAst, 'mapTaskSelectable');
const productionTreasureName = declaration(productionHelperAst, 'treasureName');
const report = {
  task: 'LWB317-REVIEW-MAP-STATES-001',
  result: 'LWB317_REVIEW_MAP_STATES_SOURCE_LOCAL_OK',
  fixedNow: now,
  counts: {
    timestampCases: timestampResults.length,
    taskCasesPerKindPerLocale: taskInputs.length,
    taskComparisons: taskInputs.length * 2 * 2,
    treasureWorldCasesPerLocale: worldInputs.length,
    treasurePlayerCasesPerLocale: playerInputs.length,
    treasureNameCasesPerLocale: nameInputs.length,
    renderedTables: renderResults.length,
  },
  source: {
    path: sourcePath,
    sha256: sha256(source),
    locators: Object.fromEntries(['P', 'F', 'w', 'T', 'Ke', 'nt'].map(name => [name, locator(source, helperNodes[name])])),
    tableFunctions: {
      me: locator(source, originalSelectNode),
      pe: locator(source, originalStatusNode),
    },
    treasureRefreshing: {
      state: stringLocator(source, '[jn,Mn]=(0,y.useState)(!1)'),
      tableProp: stringLocator(source, 'treasureStatesRefreshing:jn'),
    },
    treasureTypes: locator(source, treasureTypes),
    ntCalls: ntCalls.map(node => locator(source, node)),
  },
  production: {
    helperPath: productionHelperPath,
    pagePath: productionPagePath,
    helperSha256NormalizedLF: sha256(productionHelperSource),
    pageSha256NormalizedLF: sha256(productionPageSource),
    locators: {
      mapTaskState: locator(productionHelperSource, productionMapTaskState),
      mapTaskSelectable: locator(productionHelperSource, productionMapTaskSelectable),
      treasureName: locator(productionHelperSource, productionTreasureName),
      MapTable: locator(productionPageSource, mapTableNode),
      MapTableCall: locator(productionPageSource, mapTableCall),
    },
    treasureRefreshingPropPassedByPage: productionPageSource.slice(mapTableCall.start, mapTableCall.end).includes('treasureStatesRefreshing='),
  },
  timestamps: timestampResults,
  tasks: taskResults,
  treasure: treasureResults,
  renders: renderResults,
  limits: 'Synthetic source/local differential and actual production MapTable render checks only. Native/gameplay/original pixel proof is outside this review.',
};

const recordPath = path.join(here, 'source-local-results.json');
if (process.argv.includes('--record')) fs.writeFileSync(recordPath, JSON.stringify(report, null, 2) + '\n');
if (process.argv.includes('--verify-record')) assert.deepEqual(JSON.parse(fs.readFileSync(recordPath, 'utf8')), report);

console.log(JSON.stringify({ result: report.result, counts: report.counts, treasureRefreshingPropPassedByPage: report.production.treasureRefreshingPropPassedByPage }, null, 2));
