import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { compile, flatten, fn, Fragment, h, hooks, jsx, nodes, raw, read, require, squad, text, transformSync } from '../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs';
import * as equipment from '../../../../src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js';
import en from '../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = read('src/LWBridge.UI-0.3.17/src/Pages.jsx');
const t = (key, vars = {}) => String(en[key] || key).replace(/\{(\w+)\}/g, (match, key) => vars[key] ?? match);
const originalFd = raw(squad, fn(squad, 'fd'));
const effectNode = nodes(originalFd).find(n => n.type === 'CallExpression' && n.callee.type === 'SequenceExpression' && raw(originalFd, n.callee).includes('useEffect') && raw(originalFd, n.arguments[0]).includes('addEventListener(`keydown`'));
assert.ok(effectNode, 'Original actual Alt effect missing');
const originalEffect = raw(originalFd, effectNode.arguments[0]);
const originalOffset = Buffer.byteLength(squad.slice(0, fn(squad, 'fd').start)) + Buffer.byteLength(originalFd.slice(0, effectNode.start));
const listeners = new Map();
const windowStub = {
  addEventListener: (name, cb) => listeners.set(name, cb),
  removeEventListener: (name, cb) => { if (listeners.get(name) === cb) listeners.delete(name); },
  setTimeout: () => 1, clearTimeout() {},
};
const state = hooks();
const effects = [];
const component = compile(pages, 'EquipmentContent', { h, Fragment, ...state, useEffect: (effect) => effects.push(effect), useI18n: () => ({ t }), ...equipment, EquipmentDialog: 'Dialog', window: windowStub });
const render = () => { state.begin(); effects.length = 0; return component({ previewEnabled: true, previewState: 'squads-equipment' }); };
render();
const currentCleanup = effects[0]();
assert.equal(typeof currentCleanup, 'function');
const parentState = hooks();
const parent = compile(pages, 'SquadsPage', { h, ...parentState, useI18n: () => ({ t }), AfkContent: 'AfkContent', EquipmentContent: 'EquipmentContent' });
const renderParent = () => { parentState.begin(); return parent({ previewState: 'squads-equipment' }); };
let parentTree = renderParent();
flatten(parentTree).find(n => n.type === 'button' && n.props.role === 'tab' && text(n) === en['squad.tabAfk']).props.onClick();
parentTree = renderParent();
const hiddenWrapper = flatten(parentTree).find(n => n.props?.style?.display === 'none' && flatten(n.props.children).some(c => c.type === 'EquipmentContent'));
assert.ok(hiddenWrapper, 'Pinned baseline must retain Equipment under display:none');
let prevented = false;
listeners.get('keydown')({ altKey: true, repeat: false, key: '2', target: { tagName: 'BODY' }, preventDefault() { prevented = true; } });
const hiddenAction = render().props['data-preview-action'];
assert.equal(prevented, true);
assert.equal(hiddenAction, 'apply-all:equipment-preset-fixed-2');
currentCleanup();
assert.equal(listeners.size, 0);

let originalActions = 0;
const fixture = equipment.previewEquipmentFixture('squads-equipment', t);
const originalEffectFn = new Function('window', 'f', '_', 't', 'xe', `return (${originalEffect});`)(windowStub, { equipmentPresets: fixture.presets }, '', true, () => originalActions++);
const originalCleanup = originalEffectFn();
listeners.get('keydown')({ altKey: true, repeat: false, key: '2', target: { tagName: 'BODY' }, preventDefault() {} });
assert.equal(originalActions, 1);
originalCleanup();
assert.equal(listeners.size, 0);
const originalParentState = hooks();
const originalParent = compile(squad, 'pd', { O: { ...originalParentState, Activity: require('react').Activity, useCallback: fn => fn }, A: jsx, d: () => ({ t }), x: 'afk', g: ['afk', 'equipment'], I: 'AfkContent', fd: 'EquipmentContent', f: () => { throw Error('Native refresh is forbidden'); } });
originalParentState.begin(); originalParent({ activeTab: 'equipment' });
originalParentState.begin(); const originalHiddenTree = originalParent({ activeTab: 'afk' });
assert.ok(flatten(originalHiddenTree).some(n => n.type === require('react').Activity && n.props.mode === 'hidden' && n.props.children?.type === 'EquipmentContent'));

// Execute the recovered rename callback with a pending local acknowledgement.
const renameNode = nodes(originalFd).find(n => n.type === 'FunctionDeclaration' && n.start > 0 && raw(originalFd, n).includes('le.trim()'));
const renameSource = raw(originalFd, renameNode);
const originalRenameState = { busy: '', closed: false };
let acknowledge;
const acknowledgement = new Promise(resolve => { acknowledge = resolve; });
const renameCode = transformSync(renameSource, { loader: 'js' }).code;
const renameEnv = { le: 'Renamed fixture', f: { equipmentPresets: fixture.presets }, _: '', ce: true, N: fixture.presets[0], j: [], ve: () => { originalRenameState.closed = true; }, v: value => { originalRenameState.busy = value; }, P: () => acknowledgement };
const originalRename = new Function(...Object.keys(renameEnv), `${renameCode}; return ${renameNode.id.name};`)(...Object.values(renameEnv));
const pendingRename = originalRename();
assert.deepEqual(originalRenameState, { busy: 'rename', closed: false });
acknowledge(null); await pendingRename;
assert.deepEqual(originalRenameState, { busy: '', closed: false });
let currentTree = render();
flatten(currentTree).find(n => n.type === 'button' && text(n) === en['common.rename']).props.onClick();
currentTree = render();
flatten(currentTree).find(n => n.type === 'input').props.onChange({ target: { value: 'Renamed fixture' } });
currentTree = render();
flatten(currentTree).filter(n => n.type === 'button' && text(n) === en['common.saveConfig']).at(-1).props.onClick();
currentTree = render();
assert.ok(!flatten(currentTree).some(n => n.type === 'Dialog'));
assert.equal(currentTree.props['data-equipment-busy'], undefined);

const result = {
  submittedCommit: '4bafcd437929f2f905b557ca700657467afbdcbc', decision: 'CHANGES_REQUIRED',
  hiddenTab: { originalUsesActivity: true, originalEffectCleanupRemovesListener: true, originalAltEffectUtf8Byte: originalOffset, currentHiddenEquipmentRetained: true, currentHiddenAltPrevented: prevented, currentHiddenAltAction: hiddenAction },
  renameProofGap: { originalPendingBusy: 'rename', originalPendingDialogOpen: true, originalFailedAcknowledgementDialogOpen: true, currentPreviewSaveSynchronous: true, currentDialogClosesImmediately: true },
  limits: 'Inert actual callbacks/effects and original hidden Activity boundary. No browser input, React DOM Activity scheduling, native provider or gameplay executed. Rename proves the missing acknowledgement branch, not a native persistence result.',
};
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'independent-results.json'), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result, null, 2));
