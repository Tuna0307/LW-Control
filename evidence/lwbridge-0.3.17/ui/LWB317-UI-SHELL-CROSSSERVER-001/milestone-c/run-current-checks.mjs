import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const commandShell = process.env.ComSpec || "cmd.exe";

function run(name, command, args, cwd = repo) {
  try {
    const stdout = execFileSync(command, args, { cwd, encoding: "utf8", windowsHide: true });
    return { name, status: "PASS", command: [command, ...args].join(" "), stdout: stdout.trim() };
  } catch (error) {
    const result = {
      name,
      status: "FAIL",
      command: [command, ...args].join(" "),
      stdout: String(error.stdout || "").trim(),
      stderr: String(error.stderr || "").trim(),
      exitCode: error.status ?? null,
    };
    fs.writeFileSync(path.join(here, "current-checks.json"), `${JSON.stringify({ result: "LWB317_CROSSSERVER_CURRENT_CHECKS_FAILED", checks: [result] }, null, 2)}\n`);
    throw error;
  }
}

const checks = [
  run("focused production mount", "node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-b/run-focused.mjs"]),
  run("affected ownership regressions", "node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/run-regressions.mjs"]),
  run("canonical package check", commandShell, ["/d", "/s", "/c", "npm.cmd run check"], ui),
  run("canonical build", commandShell, ["/d", "/s", "/c", "npm.cmd run build"], ui),
  run("production package check", commandShell, ["/d", "/s", "/c", "npm.cmd run check:production-build"], ui),
  run("protected WIP guard", "node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs"]),
  run("repository diff check", "git", ["diff", "--check"]),
];

const report = {
  result: "LWB317_CROSSSERVER_CURRENT_CHECKS_OK",
  checks,
  note: "Historical result files are not rewritten by this runner. The focused and regression adapters write only this task's current result files. Vite's large-chunk advisory may appear on stderr but the build exits successfully.",
};
fs.writeFileSync(path.join(here, "current-checks.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_CROSSSERVER_CURRENT_CHECKS_OK checks=${checks.length}`);
