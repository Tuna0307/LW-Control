import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
http.createServer((req,res)=>{
 const pathname=new URL(req.url,'http://127.0.0.1').pathname;
 const isMap=pathname.startsWith('/map/');
 const root=path.join(here,isMap?'map/generated':'home/generated');
 const name=pathname.slice(isMap?5:1);
 if(!/^(en|ja)-(light|dark)-[a-z-]+-(original|current)\.html$/.test(name)){res.writeHead(404);res.end();return;}
 const f=path.join(root,name);if(!fs.existsSync(f)){res.writeHead(404);res.end();return;}
 res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; img-src data:"});res.end(fs.readFileSync(f));
}).listen(4337,'127.0.0.1',()=>console.log('LWB317_HOME_VISUAL_REFERENCE http://127.0.0.1:4337'));
