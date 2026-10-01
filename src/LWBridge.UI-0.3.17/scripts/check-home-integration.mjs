import assert from "node:assert/strict";
import fs from "node:fs";
import { createBackendBridge } from "../src/backendBridge.js";

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

const appSource = fs.readFileSync(new URL("../src/App.jsx", import.meta.url), "utf8");
const busyIndex = appSource.indexOf('setHomeBusy("autoReconnect")');
const invokeIndex = appSource.indexOf('invokeProfileScoped("set_automation", { name: "autoForceUpdateReload", enabled: value })');
const acknowledgeIndex = appSource.indexOf('setLocalConfig((current) => ({ ...(current || {}), autoReconnect: value }))');
const clearBusyIndex = appSource.indexOf('setHomeBusy("")', invokeIndex);
assert.ok(busyIndex >= 0 && invokeIndex > busyIndex, "Home reconnect must enter busy state before the native write");
assert.ok(acknowledgeIndex > invokeIndex, "Home reconnect must update checked state only after native acknowledgement");
assert.ok(clearBusyIndex > acknowledgeIndex, "Home reconnect must clear busy state after acknowledgement/error handling");
assert.match(appSource, /catch \(error\) \{\s*setGameActionError\(error\?\.message \|\| String\(error\)\);/s);

const pagesSource = fs.readFileSync(new URL("../src/Pages.jsx", import.meta.url), "utf8");
assert.match(
  pagesSource,
  /disabled=\{state\.autoReconnect == null \|\| state\.busy === "autoReconnect" \|\| !state\.production\}/,
  "Home reconnect must remain disabled while its acknowledged write is busy",
);

console.log("LWB317_HOME_UI_INTEGRATION_CHECKS_OK");
