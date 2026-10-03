import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
// Evidence-only preview config: canonical source files stay unchanged.
export default {
  root: path.join(repo, "src/LWBridge.UI-0.3.17"),
  resolve: { alias: {
    "react-dom": path.join(repo, "src/LWBridge.UI-0.3.17/node_modules/react-dom"),
    react: path.join(repo, "src/LWBridge.UI-0.3.17/node_modules/react"),
  } },
  esbuild: { jsx: "automatic" },
  server: { host: "127.0.0.1", port: 4321, strictPort: true, fs: { allow: [repo] } },
};
