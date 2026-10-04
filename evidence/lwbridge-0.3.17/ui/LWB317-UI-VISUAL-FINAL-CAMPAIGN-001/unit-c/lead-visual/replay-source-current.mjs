import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const original=fs.readFileSync(path.join(here,'../check-accepted-source-replay.mjs'),'utf8');
let code=original.replaceAll("from './accepted-harness.mjs'","from '../accepted-harness.mjs'").replaceAll('../../../../../src/','../../../../../../src/');
code=code.replace("Switch:'Switch',PreviewConfigError:'Error'", "Switch:'Switch',PreviewConfigError:'Error',useId:()=> 'current-proof-id',AutomationSettingsTrigger:compile(automationPage,'AutomationSettingsTrigger',{h,Fragment,useI18n:()=>({t,language:'en'})})");
const file=path.join(here,'.current-source-replay-adapter.mjs');
fs.writeFileSync(file,code);try {const output=execFileSync(process.execPath,[file],{encoding:'utf8'});fs.writeFileSync(path.join(here,'source-current-replay.log'),output);console.log(output);}finally {fs.unlinkSync(file);}
