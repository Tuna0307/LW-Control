import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "generated");
const portArg = process.argv.indexOf("--port");
const port = portArg >= 0 ? Number(process.argv[portArg + 1]) : 4340;
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error(`invalid port ${port}`);

const server = http.createServer((req, res) => {
  const pathname = new URL(req.url, `http://127.0.0.1:${port}`).pathname;
  const name = pathname.slice(1);
  if (!/^(en|ja)-(light|dark)-[a-z-]+-(original|current)\.html$/.test(name)) {
    res.writeHead(404);
    res.end();
    return;
  }
  const file = path.join(root, name);
  if (!fs.existsSync(file)) {
    res.writeHead(404);
    res.end();
    return;
  }
  res.writeHead(200, {
    "Content-Type": "text/html; charset=utf-8",
    "Cache-Control": "no-store",
    "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; img-src data:",
  });
  res.end(fs.readFileSync(file));
});

server.listen(port, "127.0.0.1", () => {
  console.log(`LWB317_MAP_AUTO_VISUAL_REFERENCE http://127.0.0.1:${port}`);
});
