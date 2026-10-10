import assert from "node:assert/strict";
import fs from "node:fs";
import { createBackendBridge } from "../src/backendBridge.js";
import {
  AUTO_LAUNCH_GAME_STORAGE_KEY,
  readAutoLaunchGamePreference,
  writeAutoLaunchGamePreference,
} from "../src/autoLaunchPreference.js";
import { connectionState, createMapApi } from "../src/mapBackend.js";
import { createAutomationFlagAdapter, createProfileConfigDraftRegistry } from "../src/profileConfigDraft.js";

function nativeHost(profileId = "") {
  let messageHandler;
  let requestSequence = 0;
  const posted = [];
  const host = {
    __LWBridgeBootstrap: {
      mode: "live",
      sessionId: "home-session",
      profiles: profileId ? { selectedProfileId: profileId } : {},
    },
    chrome: {
      webview: {
        addEventListener: (name, handler) => {
          if (name === "message") messageHandler = handler;
        },
        removeEventListener: () => {},
        postMessage: (message) => posted.push(message),
      },
    },
    setTimeout,
    clearTimeout,
    crypto: { randomUUID: () => `home-request-${++requestSequence}` },
  };
  return { host, posted, getMessageHandler: () => messageHandler };
}

function deliver(fixture, message) {
  fixture.getMessageHandler()({ data: { sessionId: "home-session", ...message } });
}

{
  const fixture = nativeHost();
  const bridge = createBackendBridge(fixture.host);
  assert.equal(bridge.available, true);
  await assert.rejects(
    () => bridge.invokeProfileScoped("set_automation", { name: "autoForceUpdateReload", enabled: true }),
    (error) => error.code === "PROFILE_REQUIRED",
  );
  assert.deepEqual(fixture.posted, [], "missing profile must fail before native dispatch");
  bridge.dispose();
}

{
  const fixture = nativeHost("profile-home");
  const bridge = createBackendBridge(fixture.host);
  const request = bridge.invokeProfileScoped("set_automation", { name: "autoForceUpdateReload", enabled: true });
  assert.deepEqual(fixture.posted[0], {
    kind: "invoke",
    sessionId: "home-session",
    id: "home-request-1",
    command: "set_automation",
    payload: {
      name: "autoForceUpdateReload",
      enabled: true,
      profileId: "profile-home",
    },
  });
  fixture.getMessageHandler()({
    data: {
      kind: "response",
      sessionId: "home-session",
      id: "home-request-1",
      ok: true,
      result: { enabled: true },
    },
  });
  assert.deepEqual(await request, { enabled: true });
  bridge.dispose();
}

{
  // The original Home start/reconcile/repair caller does not cancel a live
  // lifecycle request after a fixed frontend timeout. The native command
  // retains its own deadlines and the bridge still retires it on teardown.
  const fixture = nativeHost("profile-home");
  let scheduled = 0;
  fixture.host.setTimeout = () => { scheduled += 1; return scheduled; };
  const bridge = createBackendBridge(fixture.host);
  const longStart = bridge.invokeProfileScoped("profile_instance_start", { closeUnmanaged: true }, null);
  assert.equal(scheduled, 0, "original Home start must not install a frontend timeout");
  const request = fixture.posted.at(-1);
  deliver(fixture, { kind: "response", id: request.id, ok: true, result: { phase: "running" } });
  assert.equal((await longStart).phase, "running", "late native Home completion should still be accepted");
  const shortStatus = bridge.invokeProfileScoped("profile_instance_status", {});
  assert.equal(scheduled, 1, "short native status requests retain a finite deadline");
  const statusRequest = fixture.posted.at(-1);
  deliver(fixture, { kind: "response", id: statusRequest.id, ok: true, result: { phase: "stopped" } });
  await shortStatus;
  const retiringStart = bridge.invokeProfileScoped("profile_instance_start", { closeUnmanaged: true }, null);
  bridge.dispose();
  await assert.rejects(() => retiringStart, (error) => error.code === "NATIVE_BRIDGE_CLOSED",
    "teardown must retire unbounded native requests");
}

{
  const fixture = nativeHost("profile-a");
  const bridge = createBackendBridge(fixture.host);
  const mapApi = createMapApi(bridge);
  assert.deepEqual(bridge.currentProfileOwner(), { profileId: "profile-a", generation: 0 });

  const staleA = mapApi.readStatus();
  const staleARequest = fixture.posted.find((message) => message.kind === "invoke");
  assert.equal(staleARequest.payload.profileId, "profile-a", "first A status read must scope at call time");

  bridge.setSelectedProfile("profile-b");
  assert.deepEqual(bridge.currentProfileOwner(), { profileId: "profile-b", generation: 1 });
  deliver(fixture, { kind: "response", id: staleARequest.id, ok: true, result: { xluaOnline: true, marker: "stale-a" } });
  await assert.rejects(() => staleA, (error) => error.code === "PROFILE_GENERATION_RETIRED");

  const bStatus = mapApi.readStatus();
  const bRequest = fixture.posted.filter((message) => message.kind === "invoke").at(-1);
  assert.equal(bRequest.payload.profileId, "profile-b", "B status read must use current profile at call time");
  deliver(fixture, { kind: "response", id: bRequest.id, ok: true, result: { xluaOnline: false, marker: "b" } });
  assert.equal((await bStatus).marker, "b");

  const seen = [];
  const offB = mapApi.listenStatus((status) => seen.push(status.marker));
  deliver(fixture, { kind: "event", event: "bridge://status", payload: { profileId: "profile-a", payload: { marker: "wrong-a" } } });
  deliver(fixture, { kind: "event", event: "bridge://status", payload: { profileId: "profile-b", payload: { marker: "live-b" } } });
  assert.deepEqual(seen, ["live-b"], "B listener must filter foreign A events");

  bridge.setSelectedProfile("profile-a");
  assert.deepEqual(bridge.currentProfileOwner(), { profileId: "profile-a", generation: 2 });
  deliver(fixture, { kind: "event", event: "bridge://status", payload: { profileId: "profile-b", payload: { marker: "late-b" } } });
  deliver(fixture, { kind: "event", event: "bridge://status", payload: { profileId: "profile-a", payload: { marker: "old-listener-a" } } });
  assert.deepEqual(seen, ["live-b"], "retired B listener must stay retired after A/B/A");
  offB();

  const seenA2 = [];
  const offA2 = mapApi.listenStatus((status) => seenA2.push(status.marker));
  deliver(fixture, { kind: "event", event: "bridge://status", payload: { profileId: "profile-a", payload: { marker: "fresh-a" } } });
  assert.deepEqual(seenA2, ["fresh-a"], "fresh A generation must accept only its own events");

  const a2Status = mapApi.readStatus();
  const a2Request = fixture.posted.filter((message) => message.kind === "invoke").at(-1);
  assert.equal(a2Request.payload.profileId, "profile-a", "returned A read must use the new A generation");
  deliver(fixture, { kind: "response", id: a2Request.id, ok: true, result: { xluaOnline: true, marker: "fresh-a" } });
  assert.equal((await a2Status).marker, "fresh-a");
  offA2();
  bridge.dispose();
}

{
  const connectedStatus = { xluaOnline: true };
  const disconnectedStatus = { xluaOnline: false };
  const runningProxy = { gameRunning: true };
  assert.equal(connectionState(connectedStatus, runningProxy, "native", true), "connected");
  assert.equal(connectionState(connectedStatus, runningProxy, "native", false), "checking", "read start/deferred pair must invalidate stale connected availability");
  assert.equal(connectionState(connectedStatus, runningProxy, "native", false, "STATUS_READ_FAILED"), "unavailable", "rejected paired read must remain unavailable even with stale connected values");
  assert.equal(connectionState(disconnectedStatus, runningProxy, "native", true), "disconnected");
  assert.equal(connectionState(connectedStatus, runningProxy, "native", true), "connected", "fresh paired status must restore connected availability");
}

{
  const fixture = nativeHost("profile-home");
  const bridge = createBackendBridge(fixture.host);
  const request = bridge.invokeProfileScoped("set_automation", { name: "autoForceUpdateReload", enabled: false });
  fixture.getMessageHandler()({
    data: {
      kind: "response",
      sessionId: "home-session",
      id: "home-request-1",
      ok: false,
      error: {
        code: "SET_AUTOMATION_FAILED",
        message: "native write rejected",
        details: { name: "autoForceUpdateReload" },
      },
    },
  });
  await assert.rejects(
    () => request,
    (error) => error.code === "SET_AUTOMATION_FAILED" && error.details?.name === "autoForceUpdateReload",
  );
  bridge.dispose();
}

{
  const calls = [];
  const bridge = {
    profileId: "profile-home",
    invokeProfileScoped: async (command, payload) => {
      calls.push({ command, payload });
      if (command === "get_status") return { config: { auto_force_update_reload: true } };
      if (command === "set_automation") return { enabled: payload.enabled };
      throw new Error(`unexpected command: ${command}`);
    },
  };
  const adapter = createAutomationFlagAdapter(
    bridge, "profile-home", "autoForceUpdateReload", "auto_force_update_reload", false,
  );
  assert.equal(await adapter.read(), true);
  assert.equal(await adapter.write(false), false);
  assert.deepEqual(calls, [
    { command: "get_status", payload: {} },
    { command: "set_automation", payload: { name: "autoForceUpdateReload", enabled: false } },
  ]);

  const mismatchCalls = [];
  const mismatch = createAutomationFlagAdapter({
    profileId: "profile-home",
    invokeProfileScoped: async (...args) => { mismatchCalls.push(args); return { enabled: true }; },
  }, "profile-other", "autoForceUpdateReload", "auto_force_update_reload", false);
  await assert.rejects(() => mismatch.write(true), (error) => error.code === "PROFILE_SCOPE_MISMATCH");
  assert.deepEqual(mismatchCalls, [], "profile mismatch must fail before native dispatch");
}

{
  const registry = createProfileConfigDraftRegistry();
  const inert = (value) => ({ read: async () => value, write: async (next) => next });
  const profileA = registry.get("profile-a", "flag:autoForceUpdateReload", false, inert(false));
  const profileB = registry.get("profile-b", "flag:autoForceUpdateReload", true, inert(true));
  profileA.edit(true, false);
  assert.equal(profileA.getSnapshot().draft, true);
  assert.equal(profileB.getSnapshot().draft, true);
  profileB.edit(false, false);
  assert.equal(profileA.getSnapshot().draft, true, "profile B edit changed profile A draft");
  assert.equal(profileB.getSnapshot().draft, false);
  assert.equal(
    registry.get("profile-a", "flag:autoForceUpdateReload", false, inert(false)),
    profileA,
    "returning to a profile must reuse its keyed draft",
  );
}

const appSource = fs.readFileSync(new URL("../src/App.jsx", import.meta.url), "utf8");
const sidebarSource = fs.readFileSync(new URL("../src/ProfileSidebar.jsx", import.meta.url), "utf8");
{
  const noteAction = appSource.slice(
    appSource.indexOf("const updateNativeProfileNote ="),
    appSource.indexOf("// Sidebar actions address retained native owners"),
  );
  assert.match(noteAction, /nativeNoteRevisionsRef\.current\.get\(profileId\) === noteRevision/,
    "H-39 a late confirmed A note must be scoped to the latest exact A mutation");
  assert.match(noteAction, /profile\.id === profileId \? \{ \.\.\.profile, note: saved\.note \} : profile/,
    "H-39 committed note is projected to only its retained profile");
  assert.match(noteAction, /if \(nativeProfileRequestRef\.current !== request\)/,
    "H-39 note projection remains separate from selected Home-view acknowledgement");
  assert.doesNotMatch(noteAction, /setShellProfiles\(authoritative\)/,
    "H-39 a late note may not replace B selection or unrelated registry data");
}
const homeCloseBody = appSource.slice(appSource.indexOf("const stopGame = useCallback"), appSource.indexOf("const updateAndRestartGame"));
assert.match(homeCloseBody, /"profile_instance_status",\s*\{ profileId: owner\.profileId \}/,
  "original Pt Home Close must retain the clicked A owner across pending status and a B selection");
assert.match(homeCloseBody, /"profile_instance_stop",\s*\{ profileId: owner\.profileId, instanceId: instance\.instanceId \}/,
  "native Close must target the captured exact owner and instance rather than the current view");
assert.doesNotMatch(homeCloseBody, /invokeProfileScoped/,
  "profile-view retirement may fence display but must not abandon an already admitted exact-owner Close");
{
  // ORIGINAL 0.3.17 Ir at UTF-8 byte 328453 from research archive:
  // function Ir(e){let t=[]; e?.code:string && t.push(e.code);
  //   let n=e instanceof Error?e.message:String(e??"");
  //   return t.push(...n.match(/.../g)||[]),[...new Set(t)].reverse()}
  // R5's first-token inversion was wrong: restore and protect the original
  // last-distinct-code-first evaluation, including explicit native code order.
  const homeSource = fs.readFileSync(new URL("../src/HomePage.jsx", import.meta.url), "utf8");
  const homeFn = new Function(`${homeSource.slice(homeSource.indexOf("function translatedError("), homeSource.indexOf("export function HomePage("))}; return translatedError;`)();
  const sidebarFn = new Function(`${sidebarSource.slice(sidebarSource.indexOf("function errorCodes("), sidebarSource.indexOf("function ProfileIcon("))}; return profileError;`)();
  const translations = new Map([
    ["error.GAME_CLOSE_FAILED", "close failed (earlier token)"],
    ["error.LAUNCH_TASK_FAILED", "launch failed (original later priority)"],
    ["error.INSTANCE_MISMATCH", "explicit native code"],
    ["common.actionFailed", "generic action error"],
  ]);
  const t = (key) => translations.get(key) || key;
  const multi = "GAME_CLOSE_FAILED; retrying after LAUNCH_TASK_FAILED";
  assert.equal(homeFn(t, multi), "launch failed (original later priority)", "H-46 last-distinct-code-first original Home projection");
  assert.equal(sidebarFn(t, new Error(multi)), "launch failed (original later priority)", "profile error line must preserve original reverse-token projection");
  const native = new Error(multi);
  native.code = "INSTANCE_MISMATCH";
  assert.equal(homeFn(t, native), "launch failed (original later priority)", "later recognized message token precedes native code in original Ir");
  assert.equal(sidebarFn(t, native), "launch failed (original later priority)", "sidebar uses original shared Ir ordering");
  assert.equal(homeFn(t, Object.assign(new Error("non-coded failure"), {code:"INSTANCE_MISMATCH"})),
    "explicit native code", "native code remains recognized when no later message code is translated");
  assert.equal(homeFn(t, "GAME_CLOSE_FAILED LAUNCH_TASK_FAILED GAME_CLOSE_FAILED"),
    "launch failed (original later priority)", "deduplication precedes reverse iteration in original Ir");
  assert.equal(homeFn(t, "unknown wording"), "generic action error", "unknown Home errors keep original generic fallback");
  assert.equal(sidebarFn(t, "unknown wording"), "generic action error", "unknown profile errors keep original generic fallback");
}
{
  // Original LWBridge 0.3.17 Kr, recovered at UTF-8 byte 336469 of the
  // identified index-BVfnK1wp.js. Exhaust all seven inputs to compare the
  // actual HomePage source's executable control gates (not a handwritten
  // substitute for the clone) to the original oracle.
  const originalKr = ({rootResolved, rootValid, gameRunning, needsRepair = false, recovering, busy, launching = false}) => ({
    showRootPicker: rootResolved && !rootValid,
    canStart: rootResolved && rootValid && !gameRunning && !recovering && !busy && !launching,
    canStop: rootResolved && rootValid && (gameRunning || recovering) && !busy && !launching,
    repairOnClose: rootResolved && rootValid && gameRunning && needsRepair && !recovering,
  });
  const homeSource = fs.readFileSync(new URL("../src/HomePage.jsx", import.meta.url), "utf8");
  const gates = homeSource.slice(homeSource.indexOf("  const rootResolved ="), homeSource.indexOf("  let status ="));
  assert.ok(gates.includes("const canStart =") && gates.includes("const canStop ="),
    "source executable Home control gates must be extracted, not recreated");
  const actual = new Function("state", "RECOVERY_ACTIVE_STATES", "onStartGame", "onStopGame",
    "onUpdateAndRestart", `${gates}\nreturn {showRootPicker, canStart, canStop, repairOnClose};`);
  const activeStates = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);
  const provider = () => {};
  for (let flags = 0; flags < 128; flags++) {
    const rootResolved = !!(flags & 1);
    const rootValid = !!(flags & 2);
    const gameRunning = !!(flags & 4);
    const needsRepair = !!(flags & 8);
    const recovering = !!(flags & 16);
    const busy = !!(flags & 32);
    const launching = !!(flags & 64);
    const state = {
      rootResolved,
      gameRootStatus: rootValid ? {valid:true} : {valid:false},
      proxyStatus: {gameRunning, repairRequired:needsRepair},
      gameRecoveryStatus: {state:recovering ? "waiting" : "idle"},
      proxyBusy:busy,
      gameLaunchBusy:launching,
      busy:"",
      production:true,
    };
    assert.deepEqual(
      actual(state, activeStates, provider, provider, provider),
      originalKr({rootResolved, rootValid, gameRunning, needsRepair, recovering, busy, launching}),
      `H-02/H-18/H-45 original Kr conditional Home gate parity flags=${flags}`,
    );
  }
}
{
  // Original 0.3.17 Jr immediately after the Home renderer at byte ~337000:
  // every native per-profile connection state resolves a named translated
  // label, including grace/locked/error, without inventing a new state.
  const keysBody = sidebarSource.slice(
    sidebarSource.indexOf("const CONNECTION_KEYS ="),
    sidebarSource.indexOf("const EMPTY_INSTANCES"),
  );
  assert.ok(keysBody.startsWith("const CONNECTION_KEYS ="), "extract actual sidebar source translation table");
  const actualConnectionKeys = new Function(`${keysBody}\nreturn CONNECTION_KEYS;`)();
  assert.deepEqual(actualConnectionKeys, {
    offline:"profile.connection.offline",
    starting:"profile.connection.starting",
    recovering:"profile.connection.recovering",
    awaitingLogin:"profile.connection.awaitingLogin",
    connected:"profile.connection.connected",
    reconnecting:"profile.connection.reconnecting",
    grace:"profile.connection.grace",
    locked:"profile.connection.locked",
    error:"profile.connection.error",
  }, "H-42/H-45 original 0.3.17 Jr connection-state localization mapping");
}
const initialAutoLaunchSource = appSource.slice(
  appSource.indexOf("function initialAutoLaunchGame()"),
  appSource.indexOf("function initialAutoScanConfig("),
);
assert.match(initialAutoLaunchSource, /return readAutoLaunchGamePreference\(localStorage\)/,
  "native startup must read the recovered one-key Auto Launch preference");
assert.doesNotMatch(initialAutoLaunchSource, /__LWBridgeBootstrap|profileId/,
  "profile/bootstrap state must not replace the recovered global Auto Launch preference");

{
  const values = new Map();
  const storage = {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, String(value)),
  };
  assert.equal(readAutoLaunchGamePreference(storage), true,
    "missing Auto Launch key must preserve recovered default true");
  values.set(AUTO_LAUNCH_GAME_STORAGE_KEY, "false");
  assert.equal(readAutoLaunchGamePreference(storage), false,
    "only literal persisted false must disable Auto Launch");
  values.set(AUTO_LAUNCH_GAME_STORAGE_KEY, "other");
  assert.equal(readAutoLaunchGamePreference(storage), true,
    "non-false legacy/corrupt values must preserve recovered default true");
  writeAutoLaunchGamePreference(storage, false);
  assert.deepEqual([...values.entries()], [[AUTO_LAUNCH_GAME_STORAGE_KEY, "false"]],
    "Auto Launch must remain one application-global storage key across profile changes");
}
const editIndex = appSource.indexOf("autoReconnectStore.edit(value, false)");
const flushIndex = appSource.indexOf("autoReconnectStore.flush().catch(() => {})", editIndex);
assert.ok(editIndex >= 0 && flushIndex > editIndex, "Home reconnect must edit its profile draft before flushing it");
assert.doesNotMatch(appSource, /setHomeBusy\("autoReconnect"\)/, "reconnect save must not create a saving-only Home disabled state");
assert.match(appSource, /autoReconnect: autoReconnectSnapshot\.draft/);
assert.match(
  appSource,
  /\[autoWeekendShieldStore, autoAttackShieldStore, autoReconnectStore, autoClosePopupStore\]/,
  "shell save-error stores must preserve the recovered Weekend/Attack/Reconnect/Close order",
);
assert.match(appSource, /"auto_weekend_shield",\s*true,\s*"auto_shield"/, "Weekend Shield must preserve the recovered legacy auto_shield fallback");
assert.match(appSource, /"auto_attack_shield",\s*true,\s*"auto_shield"/, "Attack Shield must preserve the recovered legacy auto_shield fallback");
const autoLaunchBody = appSource.slice(appSource.indexOf("const updateAutoLaunch"), appSource.indexOf("const updateAutoReconnect"));
assert.doesNotMatch(autoLaunchBody, /setGameActionError\(""\)/, "Auto Launch preference edits must not clear the shared lifecycle action error");
const startupReconcileIndex = appSource.indexOf('"profile_instances_reconcile"');
const startupReconcileEffectStart = appSource.lastIndexOf("useEffect(() => {", startupReconcileIndex);
const startupReconcileEffectEnd = appSource.indexOf("useEffect(() => {", startupReconcileIndex + 1);
const startupReconcileEffect = appSource.slice(startupReconcileEffectStart, startupReconcileEffectEnd);
assert.match(startupReconcileEffect, /autoLaunchAll: startupAutoLaunchGameRef\.current/, "startup reconcile must use the immutable startup Auto Launch snapshot");
assert.doesNotMatch(startupReconcileEffect, /\[autoLaunchGame,/, "changing Auto Launch while startup reconcile is pending must not dispose the one-shot reconcile owner");
assert.match(appSource, /backendBridge\.invoke\("game_root_status", \{\}\)/, "Home must use the recovered root-status command");
const refreshBody = appSource.slice(appSource.indexOf("const refreshStatus"), appSource.indexOf("const selectRoute"));
assert.doesNotMatch(refreshBody, /game_recovery_status/, "recurring status refresh must not overwrite recovery ownership");
assert.match(appSource, /backendBridge\.invokeProfileScoped\("game_recovery_status", \{\}\)/, "recovery status must use the current profile generation");
assert.match(sidebarSource, /const \[runBusyIds, setRunBusyIds\] = useState\(\(\) => new Set\(\)\)/,
  "independent sidebar profile lifecycles must retain distinct visible busy owners");
assert.match(sidebarSource, /runBusyIds\.has\(profile\.id\)/,
  "each sidebar action button must read its own pending owner, not the most recently changed ID");
assert.doesNotMatch(sidebarSource, /setRunBusyId\(/,
  "completion of B may not clear an outstanding A sidebar lifecycle busy marker");
const recoveryInvokeIndex = appSource.indexOf('backendBridge.invokeProfileScoped("game_recovery_status"');
const recoveryEffectStart = appSource.lastIndexOf("useEffect(() => {", recoveryInvokeIndex);
const recoveryEffectEnd = appSource.indexOf("useEffect(() => {", recoveryInvokeIndex + 1);
const recoveryEffect = appSource.slice(recoveryEffectStart, recoveryEffectEnd);
assert.match(recoveryEffect, /backendBridge\.listen\("bridge:\/\/game-recovery"/, "recovery event ownership must share the selected-profile effect");
assert.match(recoveryEffect, /const payload = unwrapProfileEvent\(event, profileId\)/, "recovery events must apply the shared profile-envelope ownership contract");
assert.match(recoveryEffect, /if \(!closed && isCurrentProfileOwner\(owner\) && payload\) setGameRecoveryStatus\(payload\)/, "recovery events must preserve current-generation/closed lifetime guards");
assert.match(recoveryEffect, /return \(\) => \{ closed = true; stop\(\); \}/, "selected-profile recovery effect must unsubscribe and close together");
const periodicStatusIndex = appSource.indexOf("const pollStatus = async () =>");
assert.ok(periodicStatusIndex >= 0, "recurring status refresh must use its own guarded poll callback");
const periodicStatusBody = appSource.slice(appSource.lastIndexOf("useEffect(() => {", periodicStatusIndex), appSource.indexOf("useEffect(() => {", periodicStatusIndex + 1));
assert.match(periodicStatusBody, /let inFlight = false/, "recurring status refresh must own an in-flight fence");
assert.match(periodicStatusBody, /if \(inFlight\) return/, "overlapping periodic status reads must be suppressed");
assert.match(periodicStatusBody, /readStatusSnapshot\(\(\) => !closed && isCurrentProfileOwner\(owner\)\)/, "periodic acknowledgements must remain owned by the live profile generation");
assert.match(periodicStatusBody, /statusReadRevisionRef\.current \+= 1;[\s\S]*acknowledgeRuntimeStatus\(status/,
  "a newer bridge status event must retire any older in-flight paired status read before applying runtime state");
assert.match(periodicStatusBody, /window\.clearInterval\(timer\)/, "recurring status refresh must clear its timer with the effect");
assert.doesNotMatch(periodicStatusBody, /activeRoute/, "status polling must remain active while Home is retained but hidden on another route");

const statusReadBody = appSource.slice(appSource.indexOf("const readStatusSnapshot"), appSource.indexOf("const refreshStatus"));
assert.match(statusReadBody, /setStatusPairReady\(false\)/, "status read start must invalidate stale paired availability");
assert.match(statusReadBody, /!isCurrentProfileOwner\(owner\)/, "status read acknowledgement must reject retired profile generations");
assert.match(statusReadBody, /statusReadRevisionRef\.current !== statusReadRevision/,
  "paired status acknowledgement must accept only the newest request for the current profile owner");
assert.match(statusReadBody, /statusResult\.status === "fulfilled"[\s\S]*proxyResult\.status === "fulfilled"|statusResult\.status === "rejected" \|\| proxyResult\.status === "rejected"/, "status pair must inspect both current status owners");
assert.match(statusReadBody, /setStatusPairReady\(true\)/, "only a fresh paired read may restore availability");
assert.doesNotMatch(statusReadBody, /writeAutoLaunchGamePreference|setAutoLaunchGame/,
  "profile-scoped native config polling must never take ownership of the global Auto Launch preference");
assert.match(statusReadBody, /autoLaunchNativeCommittedByOwnerRef\.current\.set\([\s\S]*profileOwnerKey\(owner\)/,
  "native Auto Launch acknowledgement must remain cached under the exact profile generation owner");
assert.match(appSource, /connectionState\([\s\S]*runtimeStatus,[\s\S]*proxyStatus,[\s\S]*backendBridge\.mode,[\s\S]*statusPairReady,[\s\S]*connectionError,[\s\S]*\)/, "connected availability must consume paired-status freshness and rejection state");

{
  const deferred = () => {
    let resolve;
    let reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    return { promise, resolve, reject };
  };
  const calls = [0, 1].map(() => ({ status: deferred(), proxy: deferred() }));
  let statusIndex = 0;
  let proxyIndex = 0;
  const state = { runtime: null, proxy: null, ready: false, error: "" };
  const ref = (value = 0) => ({ current: value });
  const makeStatusReader = new Function(
    "useCallback",
    "backendBridge",
    "statusReadRevisionRef",
    "selectedProfileOwnerRef",
    "isCurrentProfileOwner",
    "setStatusPairReady",
    "setConnectionError",
    "reconnectStatusGeneration",
    "autoLaunchSaveRevisionRef",
    "autoLaunchNativeCommitEpochRef",
    "autoLaunchConfigPollGenerationRef",
    "mapApi",
    "acknowledgeRuntimeStatus",
    "setProxyStatus",
    "autoLaunchNativeCommittedByOwnerRef",
    "profileOwnerKey",
    `${statusReadBody}\nreturn readStatusSnapshot;`,
  );
  const readStatus = makeStatusReader(
    (callback) => callback,
    { available: true, invoke: async () => ({ autoLaunchGame: false }) },
    ref(),
    { current: { profileId: "A", generation: 1 } },
    () => true,
    (value) => { state.ready = value; },
    (value) => { state.error = value; },
    ref(),
    ref(),
    ref(),
    ref(),
    {
      readStatus: () => calls[statusIndex++].status.promise,
      readProxyStatus: () => calls[proxyIndex++].proxy.promise,
    },
    (value) => { state.runtime = value; },
    (value) => { state.proxy = value; },
    { current: new Map() },
    (owner) => `${owner?.generation ?? -1}:${owner?.profileId || ""}`,
  );
  const older = readStatus();
  const newer = readStatus();
  calls[1].status.reject(new Error("newer native failure"));
  calls[1].proxy.resolve({ gameRunning: false });
  await newer;
  assert.equal(connectionState(state.runtime, state.proxy, "native", state.ready, state.error), "unavailable",
    "newest failed status pair must make connected availability unavailable");
  calls[0].status.resolve({ xluaOnline: true });
  calls[0].proxy.resolve({ gameRunning: true });
  await older;
  assert.equal(connectionState(state.runtime, state.proxy, "native", state.ready, state.error), "unavailable",
    "an older connected pair must not revive availability after a newer request failed");
}

assert.match(appSource, /backendBridge\.invoke\("profile_list", \{\}\)/, "normal App must load the native profile registry");
assert.match(appSource, /backendBridge\.invoke\("profile_select", \{ profileId, focusGame: focusGame === true \}\)/, "normal sidebar selection must dispatch native profile_select");
assert.match(appSource, /adoptNativeProfileSnapshot\(snapshot, profileId\)/, "profile ownership must change only after matching native acknowledgement");
assert.match(appSource, /backendBridge\.setSelectedProfile\(next\.selectedProfileId\)/, "acknowledged native selection must advance bridge profile generation");
assert.match(appSource, /nativeProfileRequestRef\.current === request\) adoptNativeProfileSnapshot\(snapshot\)/, "stale profile_list acknowledgement must not overwrite a newer registry mutation");
assert.match(appSource, /profileDraftGeneration = backendBridge\.mode === "native" \? selectedProfileGeneration : 0/, "native profile drafts must retire by selected profile generation without changing preview draft retention");
assert.match(appSource, /<Activity key=\{route\.key\} mode=\{route\.key === activeRoute \? "visible" : "hidden"\}>/, "Home/Map route transitions must preserve retained Activity ownership");

const pagesSource = fs.readFileSync(new URL("../src/HomePage.jsx", import.meta.url), "utf8");
assert.doesNotMatch(pagesSource, /launchAdmitted|gameLaunchStatus/, "clone-only launch diagnostic must not gate original Home Start");
assert.match(pagesSource, /const canStart = lifecycleProviderAvailable && rootResolved && rootValid/, "Home Start follows original root and busy state gates");
assert.doesNotMatch(appSource, /invoke\("local_game_launch_status"/, "original Home root status must not depend on clone-only launch diagnostics");
assert.match(appSource, /const HOME_LIFECYCLE_TIMEOUT_MS = null/, "original Home lifecycle requests must not impose an extra frontend deadline");
assert.match(
  pagesSource,
  /disabled=\{state\.autoReconnect == null \|\| !state\.production\}/,
  "Home reconnect must remain editable while its profile draft is saving",
);

console.log("LWB317_HOME_UI_INTEGRATION_CHECKS_OK");
