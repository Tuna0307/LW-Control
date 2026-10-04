import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {here,scenarios,original,current,markup,normalizeHtml,sourceLocator,hash,read,canonical} from './harness.mjs';
const baseline=process.argv.includes('--baseline'),record=process.argv.includes('--record');
const cases=[];
for(const lang of ['en','ja'])for(const scenario of scenarios){
 const expected=normalizeHtml(markup(original(scenario,lang)));
 const actual=normalizeHtml(markup(current(scenario,lang,()=>{},baseline)));
 cases.push({id:scenario.id,language:lang,equal:expected===actual,originalSha256:hash(expected),currentSha256:hash(actual)});
 if(record){const folder=path.join(here,baseline?'baseline-render':'render');fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,`${lang}-${scenario.id}-original.html`),expected);fs.writeFileSync(path.join(folder,`${lang}-${scenario.id}-current.html`),actual);}
}
const result={baseline,sourceLocator,currentSha256:hash(baseline?fs.readFileSync(path.join(here,'SquadsPage.baseline.jsx'),'utf8'):read(canonical)),cases,mismatches:cases.filter(c=>!c.equal).length};
if(record)fs.writeFileSync(path.join(here,baseline?'baseline-results.json':'renderer-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({baseline,cases:cases.length,mismatches:result.mismatches}));
if(baseline)assert.ok(result.mismatches>0);else assert.equal(result.mismatches,0);
