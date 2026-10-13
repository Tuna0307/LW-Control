import fs from 'node:fs';
import crypto from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = fs.readFileSync(path.join(root, 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'), 'utf8');
function slice(start, end) {
  const from = source.indexOf(start), to = source.indexOf(end, from + start.length);
  if (from < 0 || to < 0) throw Error('Current production callback boundary not found');
  return source.slice(from, to);
}
const actions = slice('  async function prepareSelectedGhost()', '  async function scheduleSelectedDispatch()');
const tabChange = slice('  function changeTab(nextTab)', '  function changeSort(sortBy)');
const effect = slice('  useEffect(() => {\n    actionGeneration.current += 1;', '  async function prepareSelectedGhost()');
function createHarness() {
  let release, cleanup;
  const reply = new Promise(resolve => { release = resolve; });
  const state = { busyKey: '', message: '', tab: 'treasure', revisions: 0 };
  const factory = new Function('state', 'reply', 'saveCleanup', `
    let tab = 'treasure', dataServerId = 317;
    const online = true, luckyFirst = true, delay = { valid: true, seconds: 0 };
    const mapApi = { previewTreasureClaim: () => reply };
    const dispatchSelection = {}, actionGeneration = { current: 0 };
    const searchGeneration = { current: 0 }, treasureRefreshGeneration = { current: 0 };
    const tabViewCache = { current: new Map() }, page = 1, rows = [], total = 0;
    const onActiveTabChange = null;
    const setLocalTab = next => { tab = next; state.tab = next; };
    const setBusyKey = value => { state.busyKey = value; };
    const setActionMessage = value => { state.message = value; };
    const setSearchRevision = callback => { state.revisions = callback(state.revisions); };
    const setTreasureStatesRefreshing = () => {}, setPage = () => {}, setRows = () => {};
    const setTotal = () => {}, setLoading = () => {}, setQueryError = () => {};
    const t = (key, value) => JSON.stringify({key, value}), errorText = String;
    const useEffect = callback => { saveCleanup(callback()); };
    ${actions}
    ${tabChange}
    function runOwnerEffect() { ${effect} }
    runOwnerEffect();
    return { previewTreasureClaims, changeTab, runOwnerEffect };
  `);
  const callbacks = factory(state, reply, next => { cleanup = next; });
  return { state, callbacks, release, replaceOwner() { cleanup(); callbacks.runOwnerEffect(); } };
}

const changedServer = createHarness();
const pendingServer = changedServer.callbacks.previewTreasureClaims('boxes');
changedServer.replaceOwner();
changedServer.release({ eligibleCount: 1, candidateCount: 2 });
await pendingServer;

const changedTab = createHarness();
const pendingTab = changedTab.callbacks.previewTreasureClaims('boxes');
changedTab.callbacks.changeTab('city');
changedTab.release({ eligibleCount: 1, candidateCount: 2 });
await pendingTab;

const result = {
  proof: 'Actual production callbacks/effect executed with inert deferred provider; not mounted/live proof',
  sourceSha256: crypto.createHash('sha256').update(source).digest('hex'),
  externalGameActions: 0,
  findings: [
    { id: 'LEADMAP007-01', trigger: 'Browse server/Map API generation changes during pending preview',
      busyAfterOldReply: changedServer.state.busyKey,
      reproduced: changedServer.state.busyKey === 'preview-treasure' },
    { id: 'LEADMAP007-02', trigger: 'Treasure -> City while preview is deferred',
      activeTab: changedTab.state.tab, messageAfterOldReply: changedTab.state.message,
      reproduced: changedTab.state.tab === 'city' && changedTab.state.message.includes('treasurePreviewReady') },
  ],
};
console.log(JSON.stringify(result, null, 2));
// Review probe succeeds when its negative cases are reproduced. This is not
// a product pass; corrected replays must invert the finding predicates.
if (!result.findings.every(row => row.reproduced)) process.exitCode = 2;
