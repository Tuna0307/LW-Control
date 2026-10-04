import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const bridgePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/backendBridge.js");
const helperPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/profileConfigDraft.js");
const nativePath = path.join(repo, "src/LWBridge.Desktop/LWBridgeBackend.cs");
const bridge = fs.readFileSync(bridgePath, "utf8");
const helper = fs.readFileSync(helperPath, "utf8");
const native = fs.readFileSync(nativePath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

for (const marker of [
  'const profileId = bootstrap.profiles?.selectedProfileId || ""',
  "function invokeProfileScoped(command, payload = {}, timeoutMs = DEFAULT_TIMEOUT_MS)",
  "if (!profileId) return Promise.reject(profileRequiredError());",
  "return invoke(command, { ...payload, profileId }, timeoutMs);",
]) assert.ok(bridge.includes(marker), `backend bridge lost ownership marker: ${marker}`);

for (const marker of [
  "if (!profileId || !bridge?.profileId) throw profileRequiredError();",
  "if (bridge.profileId !== profileId) throw profileScopeMismatchError();",
  'bridge.invokeProfileScoped("get_status", {})',
  'bridge.invokeProfileScoped("set_automation", { name, enabled })',
]) assert.ok(helper.includes(marker), `profile draft adapter lost ownership marker: ${marker}`);

for (const marker of [
  'case "set_automation":',
  "RequireOptionalProfile(payload);",
  'if (name != "autoForceUpdateReload")',
  "AutoReconnect = enabled",
  "auto_force_update_reload = config.Snapshot.AutoReconnect",
  'case "local_config_get":',
]) assert.ok(native.includes(marker), `native adapter mapping marker missing: ${marker}`);

assert.ok(!native.includes("AutoReconnectByProfile"), "unexpected per-profile reconnect persistence appeared without separate native phase");

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  date: "2026-10-04",
  result: "LWB317_HOME_RECONNECT_NATIVE_BOUNDARY_OK",
  files: {
    backendBridge: { path: path.relative(repo, bridgePath).replaceAll("\\", "/"), sha256: sha256(bridge) },
    profileDraftAdapter: { path: path.relative(repo, helperPath).replaceAll("\\", "/"), sha256: sha256(helper) },
    nativeBackend: { path: path.relative(repo, nativePath).replaceAll("\\", "/"), sha256: sha256(native) },
  },
  verified: [
    "frontend bridge captures one bootstrap selected profile and rejects missing profile before profile-scoped dispatch",
    "001B adapter rejects missing or mismatched selected/store ownership before calling the bridge",
    "set_automation autoForceUpdateReload writes AutoReconnect through the existing native config update path",
    "get_status reports config.auto_force_update_reload from the same clone AutoReconnect snapshot",
  ],
  persistenceLimitation: "The current desktop clone stores AutoReconnect in its single local config snapshot. No recovered per-profile native reconnect store exists here. 001B reproduces the original profile-owned frontend draft/save lifetime and prevents wrong-profile attribution, but per-profile native persistence remains a separate native phase.",
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "native-boundary-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, limitationRecorded: true }, null, 2));
