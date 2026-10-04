import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const src = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const archivedSource = fs.readFileSync(path.join(here, "archive/src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js"), "utf8");
const archived = await import("data:text/javascript;base64," + Buffer.from(archivedSource).toString("base64"));
const current = await import(pathToFileURL(path.join(src, "previewAfkFixtures.js")).href);
const adapter = fs.readFileSync(path.join(src, "previewAfkCloseoutFixtures.js"), "utf8")
  .replace(/import\s*\{[\s\S]*?\}\s*from\s*['"]\.\/previewAfkFixtures\.js['"];\s*/, "")
  .replace(/export \{[^}]*\};/g, "").replaceAll("export function", "function");
const names = ["baseInitialAfkToolbarConfig", "previewAllianceMembers", "previewGarrisonBuildings", "baseGarrisonRuntime", "baseMemberFixture", "baseZombieRuntime"];
const compile = (module) => new Function(...names, adapter + "\nreturn initialAfkToolbarConfig;")(
  module.initialAfkToolbarConfig, module.previewAllianceMembers, module.previewGarrisonBuildings,
  module.previewGarrisonRuntime, module.previewMemberFixture, module.previewZombieBusRuntime);
const before = compile(archived), after = compile(current);
const states = new Set(["", "native", "squads", "squads-profile", "squads-profile-drill-unknown", "squads-profile-garrison-unknown", "squads-profile-zombie-unknown", "squads-profile-potion-unknown"]);
for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
  if (!entry.isFile() || !/\.(jsx?|json)$/.test(entry.name)) continue;
  const text = fs.readFileSync(path.join(src, entry.name), "utf8");
  for (const match of text.matchAll(/squads-profile(?:-[A-Za-z0-9]+)*/g)) states.add(match[0]);
}
for (const state of states) assert.deepEqual(after(state), before(state), state);
const result = { marker: "AFK_PENDING_SUPERSEDED_EQUIVALENCE_OK", states: states.size, cases: [...states], method: "Actual accepted adapter executed with archived pending/current restored base modules; complete returned configurations equal" };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "afk-equivalence.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ marker: result.marker, states: result.states }));
