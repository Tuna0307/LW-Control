import { createHarness as previous } from '../LWB317-UI-MAP-TREASURE-PICKER-001/picker-harness.mjs';
export * from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
// Historical tests submit DOM strings to selects. Preserve their assertions,
// translating this test-only entry point to the actual goods parent callback.
// Actual menu/keys/closing are compared independently in check-picker.mjs.
export async function createHarness(source,label,options={}) {
  const h=await previous(source,label,options), find=h.findNodes;
  h.findNodes=predicate=>{
    const found=find(predicate);
    const picker=find(n=>typeof n.type==='function' && n.type.name==='MapRetainedGoodsFilter')[0];
    if(picker) {
      const virtual={type:'select',props:{'aria-label':picker.props.label,value:picker.props.value,children:[],onChange:event=>{
        const raw=event.target.value, chosen=picker.props.items.find(item=>String(item.key)===raw);
        picker.props.onChange(raw ? chosen?.key ?? raw : '');
      }}};
      if(predicate(virtual))found.push(virtual);
    }
    return found;
  };
  return h;
}
