import { motion, AnimatePresence, useReducedMotion } from "./vendor/equipmentMotion317.js";

export { motion, AnimatePresence, useReducedMotion };

// Exact source props in SquadPanel-HC3-DJei.js, Equipment branch only.
// Defaults omitted by the original remain omitted: the recovered engine owns
// per-property springs, interpolation, presence and layout projection.
export function equipmentMotionProps(kind, reduced, { index = 0, presetId, equipUuid, hasSavedEquip = false } = {}) {
  switch (kind) {
    case "presets":
      return { initial: reduced ? false : { opacity: 0, y: 8 }, animate: { opacity: 1, y: 0 }, exit: reduced ? undefined : { opacity: 0, y: -6 }, transition: { duration: .18 } };
    case "squad":
      return { initial: reduced ? false : { opacity: 0, y: 10 }, animate: { opacity: 1, y: 0 }, transition: { delay: index * .05, duration: .2 } };
    case "slot":
      return { layout: true, layoutId: hasSavedEquip ? `preset-equip-${presetId}-${equipUuid}` : undefined, whileHover: reduced ? undefined : { y: -3, scale: 1.06 }, whileTap: reduced ? undefined : { scale: .97 } };
    case "position":
      return { layout: true, whileHover: reduced ? undefined : { y: -2 } };
    case "rename":
      return { initial: reduced ? false : { opacity: 0, y: 8, scale: .98 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: reduced ? undefined : { opacity: 0, y: 6, scale: .98 } };
    case "toast":
      return { initial: reduced ? false : { opacity: 0, y: 10, scale: .96 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: reduced ? undefined : { opacity: 0, y: 8, scale: .98 } };
    case "progress":
      return { initial: reduced ? false : { opacity: 0, y: 10, scale: .97 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: reduced ? undefined : { opacity: 0, y: 8, scale: .98 } };
    default:
      throw new Error("Unknown recovered Equipment motion branch");
  }
}
