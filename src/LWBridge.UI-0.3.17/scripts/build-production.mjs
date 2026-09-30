import fs from "node:fs";
import path from "node:path";
import { build as viteBuild } from "vite";
import { createBuildIdentity, identityFileName, uiRoot } from "./production-build-identity.mjs";

function readOutDir() {
  const index = process.argv.indexOf("--outDir");
  if (index < 0) return path.join(uiRoot, "dist");
  if (index + 1 >= process.argv.length) throw new Error("--outDir requires a path");
  return path.resolve(process.argv[index + 1]);
}

const outDir = readOutDir();
await viteBuild({
  root: uiRoot,
  build: {
    outDir,
    emptyOutDir: true,
    manifest: true,
  },
});

const identity = createBuildIdentity(outDir);
fs.writeFileSync(path.join(outDir, identityFileName), JSON.stringify(identity, null, 2) + "\n");
console.log(`LWB317_PRODUCTION_UI_BUILD_OK ${identity.sourceFingerprint} ${identity.artifactFingerprint}`);
