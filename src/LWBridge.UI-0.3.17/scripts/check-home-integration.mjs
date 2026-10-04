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
assert.match(appSource, /\[null, null, autoReconnectStore, null\]/, "reconnect store must occupy the source third save-error slot");

const pagesSource = fs.readFileSync(new URL("../src/HomePage.jsx", import.meta.url), "utf8");
assert.match(
  pagesSource,
  /disabled=\{state\.autoReconnect == null \|\| !state\.production\}/,
  "Home reconnect must remain editable while its profile draft is saving",
);

console.log("LWB317_HOME_UI_INTEGRATION_CHECKS_OK");
