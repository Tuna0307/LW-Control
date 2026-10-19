import assert from "node:assert/strict";
import { createMonsterAfkAdapter, emptyMonsterAfkConfig, createStaminaPotionAdapter, emptyStaminaConfig } from "../src/squadsAfkRuntime.js";

const calls = [];
let saved = emptyMonsterAfkConfig(), stamina = emptyStaminaConfig();
const bridge = { profileId: "A", async invokeProfileScoped(command, payload) {
  calls.push({ command, payload: structuredClone(payload) });
  if (command === "get_status") return { config: { tasks: { monsterSweep: structuredClone(saved), staminaPotion: structuredClone(stamina) } } };
  if (command === "monster_afk_config_save") { saved = structuredClone(payload); return saved; }
  if (command === "stamina_potion_config_save") { stamina = structuredClone(payload); return stamina; }
  return { running: command === "automation_start" };
} };
const adapter = createMonsterAfkAdapter(bridge, "A", () => true);
assert.deepEqual(await adapter.read(), saved);
await adapter.write({ ...saved, enabled: true });
assert.deepEqual(calls.slice(-2).map((c) => c.command), ["monster_afk_config_save", "feature018_afk_configure"]);
assert.deepEqual(calls.at(-1).payload, {}, "Browser must not supply native targets/config/UID");
await adapter.write({ ...saved, enabled: false });
assert.deepEqual(calls.slice(-2).map((c) => c.command), ["automation_stop", "monster_afk_config_save"], "Stop must precede persistence");
const potion = createStaminaPotionAdapter(bridge, "A", () => true);
assert.deepEqual(await potion.read(), emptyStaminaConfig());
await potion.write({ enabled: true, minStamina: 50, preferFifty: true });
assert.deepEqual(calls.slice(-2).map((c) => c.command), ["stamina_potion_config_save", "automation_start"]);
assert.deepEqual(calls.at(-1).payload, { taskName: "staminaPotion" });
await potion.write({ enabled: false, minStamina: 50, preferFifty: true });
assert.deepEqual(calls.slice(-2).map((c) => c.command), ["automation_stop", "stamina_potion_config_save"]);
bridge.profileId = "B";
const before = calls.length;
await assert.rejects(adapter.read, (error) => error.code === "PROFILE_SCOPE_MISMATCH");
await assert.rejects(() => adapter.write(saved), (error) => error.code === "PROFILE_SCOPE_MISMATCH");
assert.equal(calls.length, before, "Retired profile must not send to its successor");
console.log("FEATURE018_R20_AFK_UI_RUNTIME_OK: persisted config, start/stop ordering, no browser targets, profile fence");
