import { createHarness as originalHarness } from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
export * from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';

// Historical lifecycle tests submit DOM string values to a native select. The
// product now has source-like menu buttons. Adapt only that test entry point to
// the actual parent's menu callback; renderer/close/key strictness is independently
// checked by check-picker.mjs and real browser controls. No product source edits.
export async function createHarness(source, label, options = {}) {
  const h = await originalHarness(source, label, options);
  const findNodes = h.findNodes;
  h.findNodes = predicate => {
    const found = findNodes(predicate);
    const picker = findNodes(n => typeof n.type === 'function' && n.type.name === 'MapTreasureTypeFilter')[0];
    if (picker) {
      const virtual = { type: 'select', props: {
        'aria-label': (options.translate || (key => key))('map.treasureType'),
        value: picker.props.value,
        children: [],
        onChange: event => {
          const raw = event.target.value;
          const chosen = picker.props.items.find(item => String(item.key) === raw);
          picker.props.onChange(raw ? chosen?.key ?? raw : '');
        },
      } };
      if (predicate(virtual)) found.push(virtual);
    }
    return found;
  };
  return h;
}
