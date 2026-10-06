import assert from "node:assert/strict";
import fs from "node:fs";
import { createBackendBridge } from "../src/backendBridge.js";
import { createAutomationFlagAdapter, createProfileConfigDraftRegistry } from "../src/profileConfigDraft.js";

function nativeHost(profileId = "") {
  let messageHandler;
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
    crypto: { randomUUID: () => "home-request-1" },
  };
  return { host, posted, getMessageHandler: () => messageHandler };
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
assert.match(appSource, /backendBridge\.invoke\("local_game_launch_status", \{\}\)/, "Home must query clone-internal strict launch admission separately from recovered game_root_status");
const refreshBody = appSource.slice(appSource.indexOf("const refreshStatus"), appSource.indexOf("const selectRoute"));
assert.doesNotMatch(refreshBody, /game_recovery_status/, "recurring status refresh must not overwrite recovery ownership");
assert.match(appSource, /backendBridge\.invoke\("game_recovery_status", \{ profileId \}\)/, "recovery status must have its own selected-profile initial read");
const recoveryInvokeIndex = appSource.indexOf('backendBridge.invoke("game_recovery_status"');
const recoveryEffectStart = appSource.lastIndexOf("useEffect(() => {", recoveryInvokeIndex);
const recoveryEffectEnd = appSource.indexOf("useEffect(() => {", recoveryInvokeIndex + 1);
const recoveryEffect = appSource.slice(recoveryEffectStart, recoveryEffectEnd);
assert.match(recoveryEffect, /backendBridge\.listen\("bridge:\/\/game-recovery"/, "recovery event ownership must share the selected-profile effect");
assert.match(recoveryEffect, /const payload = unwrapProfileEvent\(event, profileId\)/, "recovery events must apply the shared profile-envelope ownership contract");
assert.match(recoveryEffect, /if \(!closed && selectedProfileIdRef\.current === profileId && payload\) setGameRecoveryStatus\(payload\)/, "recovery events must preserve current-profile/closed lifetime guards");
assert.match(recoveryEffect, /return \(\) => \{ closed = true; stop\(\); \}/, "selected-profile recovery effect must unsubscribe and close together");
const periodicStatusIndex = appSource.indexOf("const pollStatus = async () =>");
assert.ok(periodicStatusIndex >= 0, "recurring status refresh must use its own guarded poll callback");
const periodicStatusBody = appSource.slice(appSource.lastIndexOf("useEffect(() => {", periodicStatusIndex), appSource.indexOf("useEffect(() => {", periodicStatusIndex + 1));
assert.match(periodicStatusBody, /let inFlight = false/, "recurring status refresh must own an in-flight fence");
assert.match(periodicStatusBody, /if \(inFlight\) return/, "overlapping periodic status reads must be suppressed");
assert.match(periodicStatusBody, /readStatusSnapshot\(\(\) => !closed && selectedProfileIdRef\.current === profileId\)/, "periodic acknowledgements must remain owned by the live selected profile");
assert.match(periodicStatusBody, /window\.clearInterval\(timer\)/, "recurring status refresh must clear its timer with the effect");

const pagesSource = fs.readFileSync(new URL("../src/HomePage.jsx", import.meta.url), "utf8");
assert.match(pagesSource, /const launchAdmitted = state\.gameLaunchStatus\?\.valid === true/);
assert.match(pagesSource, /const canStart = lifecycleProviderAvailable && launchAdmitted && rootResolved && rootValid/, "Home Start must require strict launch admission without changing the public root predicate");
assert.match(
  pagesSource,
  /disabled=\{state\.autoReconnect == null \|\| !state\.production\}/,
  "Home reconnect must remain editable while its profile draft is saving",
);

console.log("LWB317_HOME_UI_INTEGRATION_CHECKS_OK");
