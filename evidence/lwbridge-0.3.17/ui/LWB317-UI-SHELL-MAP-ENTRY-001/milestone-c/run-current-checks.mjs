import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");

function run(name, file, args, cwd, expected) {
  const stdout = execFileSync(file, args, { cwd, encoding: "utf8" });
  if (expected && !stdout.includes(expected)) {
    throw new Error(`${name}: missing marker ${expected}`);
  }
  const marker = expected || stdout.trim().split(/\r?\n/).filter(Boolean).at(-1) || "PASS";
  console.log(`${name}: ${marker}`);
  return { name, status: "PASS", marker };
}

const checks = [];
checks.push(run(
  "exact original and dispatch baseline",
  process.execPath,
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/recovery/run-baseline.mjs"],
  repo,
  "LWB317_MAP_ENTRY_BASELINE_OK cases=5",
));
checks.push(run(
  "current focused App behavior",
  process.execPath,
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-b/run-focused.mjs"],
  repo,
  "LWB317_MAP_ENTRY_FOCUSED_OK cases=10",
));
checks.push(run(
  "affected retention Cross-server Home and Map ownership",
  process.execPath,
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-b/run-affected.mjs"],
  repo,
  "LWB317_MAP_ENTRY_AFFECTED_OK checks=4",
));

const npmFile = process.platform === "win32" ? "cmd.exe" : "npm";
const npmArgs = (script) => process.platform === "win32"
  ? ["/d", "/s", "/c", `npm.cmd run ${script}`]
  : ["run", script];

checks.push(run("canonical npm check", npmFile, npmArgs("check"), ui, "LWB317_UI_DRAFT_CHECKS_OK"));
checks.push(run("canonical production build", npmFile, npmArgs("build"), ui, "LWB317_PRODUCTION_UI_BUILD_OK"));
checks.push(run("canonical production package check", npmFile, npmArgs("check:production-build"), ui, "LWB317_PRODUCTION_UI_PACKAGE_OK"));
checks.push(run(
  "protected WIP guard",
  process.execPath,
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs"],
  repo,
  "LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7",
));
checks.push(run("repository diff check", "git", ["diff", "--check"], repo, null));

const result = {
  result: "LWB317_MAP_ENTRY_CURRENT_CHECKS_OK",
  checks,
  note: "The canonical npm commands are executed from src/LWBridge.UI-0.3.17. A prior ad-hoc root-directory invocation during this session failed with ENOENT because the repository root has no package.json; these recorded runs use the canonical package directory and pass.",
};

fs.writeFileSync(path.join(here, "current-checks.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(`LWB317_MAP_ENTRY_CURRENT_CHECKS_OK count=${checks.length}`);
