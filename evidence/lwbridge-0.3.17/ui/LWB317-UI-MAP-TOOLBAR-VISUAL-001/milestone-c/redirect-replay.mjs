import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const targetArg = process.argv[2];
const label = process.argv[3] || "replay";
const targetArgs = process.argv.slice(4);
if (!targetArg) throw new Error("redirect-replay requires a script path");
const target = path.resolve(repo, targetArg);
const targetDir = path.dirname(target);
const outputRoot = path.join(here, "replays", label);
fs.mkdirSync(outputRoot, { recursive: true });

const originalWriteFileSync = fs.writeFileSync;
const originalMkdirSync = fs.mkdirSync;
const originalArgv = [...process.argv];

function mapped(file) {
  const resolved = path.resolve(file);
  if (resolved.startsWith(path.resolve(here) + path.sep)) return resolved;
  let relative = path.relative(targetDir, resolved);
  if (relative.startsWith("..")) relative = path.basename(resolved);
  return path.join(outputRoot, relative);
}

fs.mkdirSync = (dir, options) => originalMkdirSync(mapped(dir), options);
fs.writeFileSync = (file, data, ...args) => {
  const destination = mapped(file);
  originalMkdirSync(path.dirname(destination), { recursive: true });
  return originalWriteFileSync(destination, data, ...args);
};

try {
  process.argv = [originalArgv[0], target, ...targetArgs];
  await import(pathToFileURL(target).href);
} finally {
  process.argv = originalArgv;
  fs.writeFileSync = originalWriteFileSync;
  fs.mkdirSync = originalMkdirSync;
}
