import "@vitejs/plugin-react/preamble";
import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { motion, AnimatePresence, useReducedMotion, equipmentMotionProps } from "/src/EquipmentMotion.jsx";
import "/src/reference.css";

const branchKinds = ["rename", "toast", "progress"];
function Fixture() {
  const reduced = useReducedMotion(), [preset, setPreset] = useState("A"), [visible, setVisible] = useState(true), [position, setPosition] = useState(false), [trace, setTrace] = useState([]);
  const refs = useRef({}), observed = useRef([]);
  useEffect(() => {
    let frame, disposed = false, previous = "";
    function sample(time) {
      const style = (node) => node ? getComputedStyle(node) : null;
      const transient = (node) => node ? `${style(node).opacity}|${style(node).transform}` : "removed";
      const values = { preset: refs.current.presets?.dataset.preset, presetOpacity: style(refs.current.presets)?.opacity || "removed", presetTransform: style(refs.current.presets)?.transform || "removed", slot: style(refs.current.slot)?.transform || "none", position: style(refs.current.position)?.transform || "none", positionX: Math.round(refs.current.position?.getBoundingClientRect().x || 0), rename: transient(refs.current.rename), toast: transient(refs.current.toast), progress: transient(refs.current.progress), reduced: String(reduced) };
      const key = JSON.stringify(values);
      if (key !== previous) { previous = key; observed.current.push({ time: Math.round(time), ...values }); if (observed.current.length > 160) observed.current.shift(); setTrace([...observed.current]); }
      if (!disposed) frame = requestAnimationFrame(sample);
    }
    frame = requestAnimationFrame(sample);
    return () => { disposed = true; cancelAnimationFrame(frame); };
  }, [reduced]);
  return <main className="panel" style={{ margin: 24, maxWidth: 960 }}>
    <h1>Recovered Equipment motion proof</h1><p>Browser-local presentation only; no provider or native action.</p>
    <p id="reduced-state">Reduced motion: {String(reduced)}</p>
    <div style={{ display: "flex", gap: 12 }}><button onClick={() => setPreset((value) => value === "A" ? "B" : "A")}>Switch preset</button><button onClick={() => setVisible((value) => !value)}>Toggle transient surfaces</button><button onClick={() => setPosition((value) => !value)}>Move layout card</button></div>
    <AnimatePresence mode="wait"><motion.div key={preset} data-preset={preset} ref={(node) => { refs.current.presets = node; }} {...equipmentMotionProps("presets", reduced)} className="equipment-preset-squads" style={{ margin: "24px 0" }}>
      <strong>Preset {preset}</strong>{[0, 1, 2, 3].map((index) => <motion.section key={index} {...equipmentMotionProps("squad", reduced, { index })} className="equipment-preset-squad">Squad {index + 1}</motion.section>)}
    </motion.div></AnimatePresence>
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, minHeight: 100 }}>
      <motion.article ref={(node) => { refs.current.position = node; }} {...equipmentMotionProps("position", reduced)} className="equipment-position-card" style={{ gridColumn: position ? "2" : "1" }}><strong>Layout / hover card</strong>
        <motion.div ref={(node) => { refs.current.slot = node; }} {...equipmentMotionProps("slot", reduced, { presetId: preset, equipUuid: 42, hasSavedEquip: true })} className="preset-equipment-slot quality-5" style={{ minHeight: 54 }} aria-label="Recovered Equipment slot">Hover or press this slot</motion.div>
      </motion.article>
    </div>
    <AnimatePresence>{visible ? branchKinds.map((kind) => <motion.div key={kind} ref={(node) => { refs.current[kind] = node; }} {...equipmentMotionProps(kind, reduced)} className="equipment-preset-squad" style={{ marginTop: 16 }}><strong>{kind}</strong> exact source entry / exit</motion.div>) : null}</AnimatePresence>
    <details><summary>Captured physical frame trace</summary><pre id="frame-trace" style={{ maxHeight: 320, overflow: "auto", fontSize: 11 }}>{JSON.stringify(trace, null, 2)}</pre></details>
  </main>;
}
const host = document.getElementById("root");
const root = host.__motionProofRoot || (host.__motionProofRoot = createRoot(host));
root.render(<Fixture />);
