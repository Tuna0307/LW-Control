import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),target=path.resolve(here,'../motion/replay-results.json'),write=fs.writeFileSync.bind(fs),argv=process.argv;
fs.writeFileSync=(file,data,...args)=>write(path.resolve(file)===target?path.join(here,'equipment-afk-results.json'):file,data,...args);
process.argv=[...argv,'--record'];
try{await import('../motion/replay-current.mjs?profile-tabs-current');}finally{fs.writeFileSync=write;process.argv=argv;}
