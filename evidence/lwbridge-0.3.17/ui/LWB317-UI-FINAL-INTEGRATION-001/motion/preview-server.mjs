import path from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json"));
const { createServer } = await import(pathToFileURL(req.resolve("vite")).href);
const server = await createServer({ configFile: path.join(ui, "vite.config.js"), root: ui, cacheDir: path.join(ui, "node_modules", `.vite-lwb317-motion-${process.pid}`),
  resolve: { alias: [ { find: /^react$/, replacement: req.resolve("react") }, { find: /^react\/jsx-runtime$/, replacement: req.resolve("react/jsx-runtime") }, { find: /^react-dom\/client$/, replacement: req.resolve("react-dom/client") } ] },
  server: { host: "127.0.0.1", port: 4333, strictPort: true, fs: { allow: [repo] } },
});
await server.listen();
console.log("LWB317_MOTION_PREVIEW_READY http://127.0.0.1:4333/@fs/" + path.join(here, "preview.html").replaceAll("\\", "/"));
async function stop() { await server.close(); process.exit(0); }
process.on("SIGINT", stop); process.on("SIGTERM", stop);
