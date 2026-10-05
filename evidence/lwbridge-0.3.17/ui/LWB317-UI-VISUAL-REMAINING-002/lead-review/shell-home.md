# Independent lead review: shared shell / Home

Date: 2026-10-05. Reviewed HEAD: `967a92c3553e4bc81272741f333803ee54ae5e44`.
Decision: **CHANGES_REQUIRED** for the finite source/local findings below.

Read `AGENTS.md`, `docs/AI_WORK_PROTOCOL.md`, `task.md`, the assignment and current
UI master, milestone-4 audits/closeout, canonical components, renderer adapters,
and the production diff from `6919816b4382e16c48dc8694493c06879e34f310`.
No protected original execution, service calls, gameplay, production edits,
record regeneration, Git mutation, or browser/process control was performed.
Only this fresh review report was written.

## Authority and actual checks

- Reference EXE independently hashed: SHA-256
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Original asset `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`:
  SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
- Original `Pe`: UTF-8 byte 201013, length 262, SHA-256
  `5510D9A66A0226021B48ACEC69D2ACE60C04EA0544162659F5032A7B2E7594AB`.
- Original `Gi`: UTF-8 byte 361306, length 14461, SHA-256
  `AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5`.
- Current `src/LWBridge.UI-0.3.17/src/App.jsx`: SHA-256
  `E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`.
- Inspected then ran `node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/shell-source-render/validate-read-only.mjs`.
  Result: `SHELL_SOURCE_RENDER_VALIDATE_OK`, 16 cases, 16 browser pairs,
  zero console issues, two mutation detections, 1,659,712 raw changed pixels.
- Ran the AST-extracted, in-memory, inert callback reproductions embedded below.
  They execute current callback bytes and original callback bytes without editing
  them, making requests to a real bridge, or writing artifacts.

## 1. Recovery listener accepts a different profile's envelope

**EXACT_CONTRACT mismatch; pre-existing envelope omission retained by this diff.**

Original `Wt` at byte 205467 delegates to `Pe`. `Pe` inspects the event payload
envelope: when both `profileId` and `payload` exist, a profile ID unequal to `A()`
returns before delivering the inner payload. `Gi` additionally guards its
selected-profile acknowledgement with `!t && A() === e` at byte 369331.

Current `App.jsx:380-382` simply extracts `event.payload`; it never compares
`event.profileId`. The `profileId === selectedProfileId` expression compares two
values captured from the same effect render and does not inspect event ownership.
The inert replay selected A and delivered B's failed-recovery envelope. Current
accepted `{state:'failed', error:'B_ERROR'}`; exact original `Pe` rejected it.

This is an existing supported envelope shape: current `mapBackend.js:175-179`
already provides `unwrapProfileEvent`, and `LWBridgeWindow.cs:2806-2810` envelopes
profile-scoped events, including recovery (`LWBridgeWindow.cs:29`). This review
does not claim the fixed-profile current native host actually emits B into A's
session; it proves the recoverable local consumer contract differs for that input.

Required finite correction: use the existing profile-envelope ownership rule
before acknowledging recovery, preserving raw unwrapped same-profile events and
the effect's closed/cleanup lifetime. Verify A accepts A/raw events and rejects B,
and verify the replacement/cleanup boundary using inert callbacks.

## 2. Recurring status refresh can overlap and restore stale Home proxy state

**EXACT_CONTRACT mismatch; pre-existing defect, still within the closure claim.**

The original `Gi` periodic async arrow begins at byte 369938. Its outer `o` flag
prevents a second periodic status/proxy request while the first is pending; `finally`
clears that flag. Before acknowledging, it checks `t || A() !== e` and returns.
Its five-second interval calls this guarded arrow, not the manual refresh handler.

Current `App.jsx:427-428` starts `refreshStatus` initially and calls that same
async callback every five seconds without an in-flight guard. `App.jsx:344-374`
permits simultaneous status/proxy requests; line 359 acknowledges every fulfilled
proxy result. The config-result generation fence at lines 361-365 does not protect
proxy status. Even runtime status at lines 356-357 has only a profile generation,
not a request-order fence within one unchanged profile.

Reproduction: start two refreshes as two successive interval ticks with the first
provider response still pending; complete the newer proxy response with
`gameRunning:false`, then the older response with `gameRunning:true`. Current
proxy writes are `[false,true]`. Exact original periodic arrow receives two calls
but starts only one `Vt` request. This is applicable to the fixed single-profile
host: its 30-second request timeout exceeds the five-second poll interval.
No actual delayed native operation was invoked. The changed Home status is a
local asynchronous acknowledgement result, not native functionality proof.

Required finite correction: reproduce original periodic in-flight ownership and
closed/current-profile acknowledgement rules, preserving the separately recovered
explicit Refresh Status behavior. Verify two interval ticks while a deferred
read is pending do not create the second periodic read, and old-profile/closed
proxy acknowledgements do not mutate the current UI.

## 3. Abandoned preview load clears the next selected profile's loading state

**IMPLEMENTED_NOT_VALIDATED defect demonstrated in newly added local code.**

Current `App.jsx:112-120` captures an uncached profile ID, awaits two animation
frames, then unconditionally adds that ID to the cache and sets global loading
false. It has no selection generation/owner guard. Controls remain selectable
during this local loading interval (`App.jsx:775` passes no busy state).

Original `Gi` starts the selected-profile bundle at byte 369239 and only clears
`Xe` through the guarded `n2` acknowledgement (`!t && A() === e`, byte 369331).
Consequently, B's abandoned bootstrap cannot end C's loading interval.

Inert actual-current-callback replay: select uncached B; execute its first frame;
select uncached C; execute B's second frame. After B completes, selected profile
is C, loading is false, cache contains A/B but not C, and C still has a pending
animation frame. This is the explicit browser-only local profile fixture, not a
claim of missing native multi-profile provider functionality.

Required finite correction: fence completion's loading acknowledgement to its
selected profile generation, and verify B -> C before B resolves keeps C loading
until its own completion. Retain the recovered cache/return semantics and the
preview-only provider fence; this finding does not prescribe a new cache policy.

## Render coverage and scope assessment

The restored sidebar wrapper, exit sibling ancestry, four-store ordering, modal
event rules and recovered Home markup are supported by the inspected source.
No additional concrete Home markup defect was demonstrated. Original account /
authorization Upgrade controls are deliberately excluded; unavailable updater,
Refresh Status, lifecycle and profile-run providers are explicitly fenced. These
are not requested product corrections.

The whole-shell packet executes original `Gi` and current App's return expression,
but both page bodies are inert sentinels and React effects are inert. The Home
packet executes actual `qr`/`HomePage` inside a synthetic shell, while the mounted
current packet is current-only. These are useful bounded render checks; they do
not exercise the three callback defects above. The shell validator verifies saved
raw differences and two ancestry mutations, and is not a concurrency/ownership
validator. Its passing label therefore does not rebut these findings.

Representative base EN/light original/current images were inspected. The original
Account control contributes the reported header/layout geometry change; current
Refresh Status disabled presentation remains visible. The 16 raw non-exact pairs
are honestly recorded, not pixel-exact parity. This review does not invent an
additional pixel defect from that aggregate or require a broader campaign merely
because some states were unsampled.

## Executable read-only reproduction

From repository root, pass the following JavaScript to `node` on standard input
(PowerShell single-quoted here-string preserves it). It reads/parses repository
source and runs deferred inert callbacks only. No output files are created.

```javascript
const fs = require('fs'), assert = require('assert/strict');
const parser = require('./src/LWBridge.UI-0.3.17/node_modules/@babel/parser');
const current = fs.readFileSync('src/LWBridge.UI-0.3.17/src/App.jsx', 'utf8');
const original = fs.readFileSync('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js', 'utf8');
function nodes(source, jsx = false) {
  const result = [];
  function walk(node) {
    if (!node || typeof node !== 'object') return;
    if (node.type) result.push(node);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === 'object') walk(value);
    }
  }
  walk(parser.parse(source, { sourceType: 'module', plugins: jsx ? ['jsx'] : [] }));
  return result;
}
function evaluate(source, node, environment, prefix = '') {
  return new Function(...Object.keys(environment), `${prefix}return (${source.slice(node.start, node.end)});`)(...Object.values(environment));
}
function deferred() {
  let resolve;
  const promise = new Promise(r => { resolve = r; });
  return { promise, resolve };
}
const cn = nodes(current, true), on = nodes(original);
(async () => {
  const effect = cn.find(n => n.type === 'CallExpression' && n.callee.name === 'useEffect'
    && current.slice(n.start, n.end).includes('game_recovery_status')).arguments[0];
  let listener, accepted = [];
  evaluate(current, effect, {
    backendBridge: { available: true, profileId: 'A',
      listen: (_, callback) => { listener = callback; return () => {}; },
      invoke: () => new Promise(() => {}) },
    selectedProfileId: 'A', setGameRecoveryStatus: x => accepted.push(x),
  })();
  listener({ profileId: 'B', payload: { state: 'failed', error: 'B_ERROR' } });
  assert.equal(accepted.length, 1);
  console.log(JSON.stringify({ case: 'current_recovery_cross_profile', accepted }));
  const pe = on.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'Pe');
  let originalListener, originalAccepted = [];
  evaluate(original, pe, { Ne: () => true, A: () => 'A',
    Me: (_, callback) => { originalListener = callback; return Promise.resolve(() => {}); },
  })('bridge://game-recovery', x => originalAccepted.push(x));
  originalListener({ payload: { profileId: 'B', payload: { state: 'failed' } } });
  assert.equal(originalAccepted.length, 0);
  console.log(JSON.stringify({ case: 'original_recovery_cross_profile', rejected: true }));

  const refresh = cn.find(n => n.type === 'VariableDeclarator' && n.id.name === 'refreshStatus').init.arguments[0];
  let pending = [], proxyWrites = [];
  const refreshFn = evaluate(current, refresh, {
    backendBridge: { available: true, invoke: () => Promise.resolve({}) },
    reconnectStatusGeneration: { current: 0 }, autoLaunchSaveRevisionRef: { current: 0 },
    autoLaunchNativeCommitEpochRef: { current: 0 }, autoLaunchConfigPollGenerationRef: { current: 0 },
    autoLaunchCommittedRef: { current: false },
    mapApi: { readStatus: () => Promise.resolve({}), readProxyStatus: () => {
      const request = deferred(); pending.push(request); return request.promise;
    } }, acknowledgeRuntimeStatus() {}, setProxyStatus: x => proxyWrites.push(x), setConnectionError() {},
  });
  const older = refreshFn(), newer = refreshFn();
  pending[1].resolve({ gameRunning: false }); await newer;
  pending[0].resolve({ gameRunning: true }); await older;
  assert.deepEqual(proxyWrites.map(x => x.gameRunning), [false, true]);
  console.log(JSON.stringify({ case: 'current_periodic_overlap', requests: pending.length, proxyWrites }));
  const poll = on.find(n => n.type === 'VariableDeclarator' && n.id.name === 's' && n.init
    && original.slice(n.init.start, n.init.end).includes('Promise.all([ot(e).catch'));
  assert.ok(poll);
  let originalRequests = 0;
  const request = deferred();
  const periodic = evaluate(original, poll.init, {
    ot: () => Promise.resolve({}), Vt: () => { originalRequests++; return request.promise; },
    A: () => 'A', Ot() {}, f() {},
  }, "let o=false,t=false,e='A';");
  const first = periodic(), second = periodic(); await second;
  request.resolve({ gameRunning: true }); await first;
  assert.equal(originalRequests, 1);
  console.log(JSON.stringify({ case: 'original_periodic_overlap', requests: originalRequests, skipped: true }));

  const callbacks = cn.find(n => n.type === 'VariableDeclarator' && n.id.name === 'profilePreviewCallbacks');
  const selectNode = callbacks.init.consequent.properties.find(n => n.key.name === 'onSelect').value;
  let selected = 'A', loading = false, frames = [], cache = new Set(['A']);
  const select = evaluate(current, selectNode, {
    shellProfiles: { selectedProfileId: 'A' }, previewProfileCache: { current: cache },
    setPreviewProfileLoading: x => { loading = x; },
    setShellProfiles: change => { selected = change({ selectedProfileId: selected }).selectedProfileId; },
    window: { requestAnimationFrame: callback => frames.push(callback) },
  });
  const b = select('B'); frames.shift()();
  select('C'); frames.shift()(); await b;
  assert.equal(selected, 'C'); assert.equal(loading, false); assert.equal(cache.has('C'), false);
  console.log(JSON.stringify({ case: 'current_loading_overlap', selected, loading, cache: [...cache], pendingFrames: frames.length }));
})().catch(error => { console.error(error); process.exitCode = 1; });
```

Observed invariant results: current cross-profile recovery accepted / original
rejected; current periodic requests 2 and proxy writes false -> true / original
periodic requests 1; current selected C with loading false, cache A/B, C pending.
The tests are callback execution, not a mounted browser or native live proof.

Continuation: correct the three bounded local ownership/concurrency defects,
then rerun the existing affected checks and add distinguishing inert regression
proof for these exact branches. Project-lead acceptance remains open.
