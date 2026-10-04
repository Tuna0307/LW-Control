import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
// Only explicitly generated local UI reference fixtures; no external proxy.
const files = new Map([['/exit', path.join(here,'profiles/browser-oracle.html')]]);
http.createServer((request,response) => {
  const file = files.get(new URL(request.url,'http://127.0.0.1').pathname);
  if (!file) { response.writeHead(404); response.end(); return; }
  response.writeHead(200, {'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store'});
  response.end(fs.readFileSync(file));
}).listen(4336,'127.0.0.1',()=>console.log('LWB317_ORACLE_SERVER http://127.0.0.1:4336'));
