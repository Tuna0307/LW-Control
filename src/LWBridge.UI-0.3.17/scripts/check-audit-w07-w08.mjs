// Audit 2026-10-23 W07 (visual-settings rollback stays on its own profile) and W08 (Hotkeys and
// Mini Games switch saves never undo each other). Adapted from the audit's frontend probe.
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../src");
const req = createRequire(path.resolve(here, "../package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const { createRoot } = req("react-dom/client");

const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/?view=overview",
});
for (const key of [
  "window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent",
  "KeyboardEvent", "Node", "MutationObserver", "localStorage",
]) globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("audit W07/W08 check forbids external network access"); };
window.matchMedia = () => ({ matches: true });
localStorage.setItem("lwbridge.language", "en");
localStorage.setItem("lwbridge.theme", "light");
document.documentElement.dataset.theme = "light";

const intervals = new Map();
const timeouts = new Map();
let timerId = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerId, { fn, delay }); return timerId; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerId, { fn, delay }); return timerId; };
window.clearTimeout = (id) => timeouts.delete(id);

const calls = [];
const listeners = [];
let generation = 1;
let invokeHandler = async (command) => { throw new Error(`Unexpected R29 visual metrics command: ${command}`); };
const bridge = {
  mode: "native",
  available: true,
  profileId: "profile-a",
  setSelectedProfile(id) {
    if (id !== this.profileId) generation += 1;
    this.profileId = id;
    return this.currentProfileOwner();
  },
  currentProfileOwner() { return { profileId: this.profileId, generation }; },
  isCurrentProfileOwner(owner) {
    return owner?.profileId === this.profileId && owner?.generation === generation;
  },
  listen(eventName, fn) {
    const state = { eventName, fn, active: true };
    listeners.push(state);
    return () => { state.active = false; };
  },
  invokeProfileScoped(command, args = {}) {
    const payload = { ...args, profileId: this.profileId };
    calls.push({ command, args: payload, scoped: true });
    return Promise.resolve().then(() => invokeHandler(command, payload));
  },
  invoke(command, args = {}) {
    calls.push({ command, args, scoped: false });
    return Promise.resolve().then(() => invokeHandler(command, args));
  },
};

globalThis.__r29VisualMetrics = { bridge };
const bundle = await build({
  stdin: {
    contents: 'export { SettingsPage } from "./SettingsPage.jsx"; export { HotkeysPage, MiniGamesPage } from "./HotkeyPages.jsx"; export { App } from "./App.jsx"; export { I18nProvider, useI18n } from "./i18n.jsx";',
    resolveDir: src,
    loader: "jsx",
  },
  bundle: true,
  write: false,
  platform: "browser",
  format: "cjs",
  jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" },
  logLevel: "silent",
  plugins: [{
    name: "r29-visual-metrics-native-transport",
    setup(builder) {
      builder.onLoad({ filter: /backendBridge\.js$/ }, () => ({
        contents: "export const backendBridge=globalThis.__r29VisualMetrics.bridge;",
        loader: "js",
      }));
    },
  }],
});
const mod = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);


const container=document.getElementById("root");
const flush=async(fn)=>React.act(async()=>{if(fn)await fn();for(let i=0;i<35;i++)await Promise.resolve();});
const wrap=child=>React.createElement(mod.exports.I18nProvider,null,child);
bridge.invokeProfileScoped=function(command,args={}) {
 const owner=this.currentProfileOwner(),payload={...args,profileId:owner.profileId};calls.push({command,args:payload,scoped:true});
 return Promise.resolve().then(()=>invokeHandler(command,payload)).then(value=>{
  if(!this.isCurrentProfileOwner(owner))throw Object.assign(new Error('retired'),{code:'PROFILE_GENERATION_RETIRED'});return value;
 },error=>{if(!this.isCurrentProfileOwner(owner))throw Object.assign(new Error('retired'),{code:'PROFILE_GENERATION_RETIRED'});throw error;});
};
let rejectSave;
invokeHandler=async(command,args)=>{
 if(command==='visual_metrics_config_get')return {showFps:false,showPing:false};
 if(command==='feature018_visual_metrics_configure')return {};
 if(command==='visual_metrics_config_save')return new Promise((resolve,reject)=>{rejectSave=reject;});
 throw Error(command);
};
let root=createRoot(container);
await flush(()=>root.render(wrap(React.createElement(mod.exports.SettingsPage,{online:true}))));
await flush(()=>container.querySelector('[role="switch"]').dispatchEvent(new MouseEvent('click',{bubbles:true})));
assert.equal(typeof rejectSave,'function');
bridge.setSelectedProfile('profile-b');await flush(()=>root.unmount());
await flush(()=>rejectSave(Error('retired disk-save result')));
const wrong=calls.filter(c=>c.command==='feature018_visual_metrics_configure'&&c.args.profileId==='profile-b');
assert.equal(wrong.length,0,'W07: rollback must not reach profile-b');
console.log('FIXED settings-rollback: no game call to profile-b');

bridge.setSelectedProfile('profile-a');calls.length=0;
let saved={attack:true,recall:true,shieldUse:true,equipment:true,randomRelocate:false,allianceRelocate:false,frontlineReinforce:true,treasureChestHint:false,frontlineReinforceCount:5};
let hold=false,gets=[];
invokeHandler=async(command,args)=>{
 if(command==='hotkey_config_get'){if(hold)return new Promise(resolve=>gets.push(resolve));return structuredClone(saved);}
 if(command==='hotkey_config_save'){const {profileId,...config}=args;saved=structuredClone(config);return structuredClone(saved);}
 throw Error(command);
};
root=createRoot(container);
await flush(()=>root.render(wrap(React.createElement(React.Fragment,null,
 React.createElement('div',{id:'keys'},React.createElement(mod.exports.HotkeysPage,{online:false})),
 React.createElement('div',{id:'mini'},React.createElement(mod.exports.MiniGamesPage,{online:false}))))));
hold=true;
await flush(()=>{
 container.querySelector('#keys .hotkey-card button').dispatchEvent(new MouseEvent('click',{bubbles:true}));
 container.querySelector('#mini button[role="switch"]').dispatchEvent(new MouseEvent('click',{bubbles:true}));
});
assert.equal(gets.length,1,'W08: second save waits for the first');
for(let i=0;i<5&&gets.length;i++){const r=gets.shift();await flush(()=>r(structuredClone(saved)));}
assert.equal(saved.attack,false);assert.equal(saved.treasureChestHint,true);
console.log('FIXED hotkey-multiview-save: attack='+saved.attack+' treasureChestHint='+saved.treasureChestHint+' (both kept)');
await flush(()=>root.unmount());
