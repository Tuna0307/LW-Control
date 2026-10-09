import fs from "node:fs";
import path from "node:path";
import {
  artifactFingerprint,
  identityFileName,
  readBuildIdentity,
  sourceFingerprint,
  uiRoot,
} from "./production-build-identity.mjs";

const outDir = process.argv[2] ? path.resolve(process.argv[2]) : path.join(uiRoot, "dist");
const identity = readBuildIdentity(outDir);
const expectedSource = sourceFingerprint();
const expectedArtifact = artifactFingerprint(outDir);

if (identity.schemaVersion !== 1) throw new Error("Unsupported production UI build identity schema.");
if (identity.project !== "LWBridge.UI-0.3.17") throw new Error("Production UI project identity mismatch.");
if (identity.canonicalSource !== "src/LWBridge.UI-0.3.17") throw new Error("Production UI canonical source mismatch.");
if (identity.entrypoint !== "index.html") throw new Error("Production UI entrypoint mismatch.");
if (identity.sourceFingerprint !== expectedSource) {
  throw new Error(`Production UI output is stale: ${identity.sourceFingerprint} != ${expectedSource}`);
}
if (identity.artifactFingerprint !== expectedArtifact) {
  throw new Error(`Production UI artifact fingerprint mismatch: ${identity.artifactFingerprint} != ${expectedArtifact}`);
}
if (!fs.existsSync(path.join(outDir, "index.html"))) throw new Error("Production UI index.html is missing.");
if (!fs.existsSync(path.join(outDir, ".vite", "manifest.json"))) throw new Error("Production UI Vite manifest is missing.");

console.log(`LWB317_PRODUCTION_UI_PACKAGE_OK ${identity.sourceFingerprint} ${identity.artifactFingerprint}`);
