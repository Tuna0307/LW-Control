import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const uiRoot = path.resolve(here, "..");
export const identityFileName = "lwbridge-ui-build.json";

const sourceInputs = [
  "index.html",
  "package.json",
  "package-lock.json",
  "vite.config.js",
  "src",
];

function listFiles(root, inputs) {
  const files = [];
  function visit(fullPath) {
    const stat = fs.statSync(fullPath);
    if (stat.isDirectory()) {
      for (const entry of fs.readdirSync(fullPath).sort()) visit(path.join(fullPath, entry));
      return;
    }
    files.push(fullPath);
  }
  for (const input of inputs) visit(path.join(root, input));
  return files.sort((left, right) => left.localeCompare(right));
}

function fingerprint(root, files) {
  const hash = crypto.createHash("sha256");
  for (const fullPath of files) {
    const relativePath = path.relative(root, fullPath).replaceAll("\\", "/");
    hash.update(relativePath, "utf8");
    hash.update("\0");
    hash.update(fs.readFileSync(fullPath));
    hash.update("\0");
  }
  return hash.digest("hex");
}

export function sourceFingerprint() {
  return fingerprint(uiRoot, listFiles(uiRoot, sourceInputs));
}

export function artifactFingerprint(outDir) {
  const files = listFiles(outDir, ["."]).filter(
    (fullPath) => path.basename(fullPath) !== identityFileName,
  );
  return fingerprint(outDir, files);
}

export function createBuildIdentity(outDir) {
  return {
    schemaVersion: 1,
    project: "LWBridge.UI-0.3.17",
    canonicalSource: "src/LWBridge.UI-0.3.17",
    sourceFingerprint: sourceFingerprint(),
    artifactFingerprint: artifactFingerprint(outDir),
    entrypoint: "index.html",
  };
}

export function readBuildIdentity(outDir) {
  const markerPath = path.join(outDir, identityFileName);
  if (!fs.existsSync(markerPath)) throw new Error(`Missing production UI build identity: ${markerPath}`);
  return JSON.parse(fs.readFileSync(markerPath, "utf8"));
}
