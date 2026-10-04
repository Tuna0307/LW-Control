import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const unit=path.resolve(here,'../../unit-b');
const own=path.join(here,'corrected-renderer');
const write=fs.writeFileSync.bind(fs),mkdir=fs.mkdirSync.bind(fs);
mkdir(own,{recursive:true});
fs.mkdirSync=(file,options)=>{const relative=path.relative(unit,String(file));if(!relative.startsWith('..'))return mkdir(path.join(own,relative),options);return mkdir(file,options);};
fs.writeFileSync=(file,...args)=>{const relative=path.relative(unit,String(file));if(relative.startsWith('..'))throw Error('Unexpected replay write '+file);const target=path.join(own,relative);mkdir(path.dirname(target),{recursive:true});return write(target,...args);};
try{await import(pathToFileURL(path.join(unit,'compare-integrated-map.mjs')).href);}finally{fs.writeFileSync=write;fs.mkdirSync=mkdir;}
