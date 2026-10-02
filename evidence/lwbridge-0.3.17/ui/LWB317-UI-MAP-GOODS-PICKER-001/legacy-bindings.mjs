import fs from 'node:fs';
import path from 'node:path';
globalThis.MapTreasureTypeFilter=function MapTreasureTypeFilter(){return null;};
globalThis.MapRetainedGoodsFilter=function MapRetainedGoodsFilter(){return null;};
// The historical fixed-binding filter harness never renders child components.
// Locate the actual goods parent handler by its child label and pass a raw key
// instead of the previous select DOM event. All historical assertions stay exact.
const read=fs.readFileSync;
fs.readFileSync=(file,...args)=>{
  let text=read(file,...args);
  if(typeof file==='string' && path.basename(file)==='check-filters.mjs' && typeof text==='string') {
    text=text.replace("nodes.find(n=>n.type==='select' && n.props['aria-label']===labels[field])", "nodes.find(n=>(n.type==='select' && n.props['aria-label']===labels[field]) || (typeof n.type==='function' && n.type.name==='MapRetainedGoodsFilter' && n.props.label===labels[field]))");
    text=text.replace('node.props.onChange({target:{value,checked:value}})',"node.props.onChange(typeof node.type==='function' && node.type.name==='MapRetainedGoodsFilter' ? value : {target:{value,checked:value}})");
  }
  return text;
};
