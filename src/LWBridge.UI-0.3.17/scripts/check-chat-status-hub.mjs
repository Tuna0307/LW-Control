import assert from 'node:assert/strict';
import { createChatStatusSubscription } from '../src/chatStatusHub.js';

const timers=new Set();
const clock={setInterval: fn => {timers.add(fn);return fn;},clearInterval: fn => timers.delete(fn)};
const subscribe=createChatStatusSubscription(clock);
let generation=1, profileId='a', reads=0, resolve;
const bridge={get profileId(){return profileId;}, currentProfileOwner:()=>({profileId,generation}),
  isCurrentProfileOwner: o=>o.profileId===profileId&&o.generation===generation,
  invokeProfileScoped: async command=>{assert.equal(command,'feature018_chat_status');reads++;return new Promise(r=>{resolve=r;});}};
const delivered=[[],[],[],[]];
const unsubscribe=delivered.map(rows=>subscribe(bridge,'a',v=>rows.push(v),e=>{throw e;}));
assert.equal(reads,1);assert.equal(timers.size,1);
for(const tick of timers)void tick();assert.equal(reads,1,'No overlapping polls');
resolve({n:1});await Promise.resolve();await Promise.resolve();await Promise.resolve();
assert.deepEqual(delivered.map(r=>r.length),[1,1,1,1]);
for(const tick of timers)void tick();assert.equal(reads,2);
const priorResolve=resolve;
profileId='b';generation++;profileId='a';generation++;
const successor=[];const stop=subscribe(bridge,'a',v=>successor.push(v));
const successorResolve=resolve;
// The prior request resolves after A -> B -> A. Only the new generation may deliver.
priorResolve({n:2});await Promise.resolve();await Promise.resolve();await Promise.resolve();
assert.deepEqual(delivered.map(r=>r.length),[1,1,1,1]);assert.equal(successor.length,0);
successorResolve({n:3});await Promise.resolve();await Promise.resolve();await Promise.resolve();
assert.equal(successor.length,1);assert.equal(timers.size,1);
unsubscribe.forEach(fn=>fn());assert.equal(timers.size,1,'Old unmount cannot retire successor');
stop();assert.equal(timers.size,0);
const before=reads;const late=[];const off=subscribe(bridge,'a',v=>late.push(v));off();
resolve({n:4});await Promise.resolve();await Promise.resolve();await Promise.resolve();
assert.equal(late.length,0);assert.equal(reads,before+1);assert.equal(timers.size,0);
console.log('CHAT_STATUS_HUB_PASS four cards one poll no overlap generation fence teardown late-result discard');
