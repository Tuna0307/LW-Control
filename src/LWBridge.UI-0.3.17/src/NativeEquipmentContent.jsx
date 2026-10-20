import { useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { backendBridge } from "./backendBridge.js";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { createLiveEquipmentRuntime, equipmentSlots } from "./equipmentRuntime.js";

export function NativeEquipmentContent({ profileId = "", online = true, active = true, refreshCount = 0, bridge = backendBridge }) {
  const { t, language } = useI18n();
  const [snapshot, setSnapshot] = useState(null), [busy, setBusy] = useState(false), [healthy, setHealthy] = useState(false);
  const [error, setError] = useState(""), [notice, setNotice] = useState(""), [revision, setRevision] = useState(0);
  const [localized, setLocalized] = useState({ context: "", values: {} });
  const [dragged, setDragged] = useState(null), [dropTarget, setDropTarget] = useState("");
  const runtime = useMemo(() => {
    if (!profileId || !bridge.available) return null;
    try { return createLiveEquipmentRuntime(bridge, profileId); } catch { return null; }
  }, [bridge, profileId]);
  const activeRuntime = useRef(runtime), action = useRef(false), mutationGeneration = useRef(0), syncError = useRef(false);
  activeRuntime.current = runtime;
  const textContext = `${profileId}|${language}`;
  const texts = localized.context === textContext ? localized.values : {};
  useEffect(() => {
    let retired = false, reading = false;
    setSnapshot(null); setHealthy(false); setError(""); setNotice(""); syncError.current = false;
    if (!runtime || !online || !active) return undefined;
    const read = async () => {
      if (retired || reading || action.current || runtime.pending) return;
      reading = true;
      const generation = mutationGeneration.current;
      try {
        const fresh = await runtime.snapshot();
        if (!retired && generation === mutationGeneration.current) {
          setSnapshot(fresh); setHealthy(true);
          if (syncError.current) { setError(""); syncError.current = false; }
        }
      } catch (failure) {
        if (!retired && generation === mutationGeneration.current) { syncError.current = true; setHealthy(false); setError(String(failure.message || failure)); }
      } finally { reading = false; }
    };
    read();
    const timer = window.setInterval(read, 1500);
    return () => { retired = true; window.clearInterval(timer); };
  }, [runtime, online, active, refreshCount, revision]);
  const keys = useMemo(() => [...new Set([
    ...(snapshot?.items || []).map((item) => item.nameKey),
    ...(snapshot?.squads || []).flatMap((squad) => squad.positions.map((position) => position.heroNameKey || position.nameKey)),
  ].filter((key) => typeof key === "string" && key))].sort(), [snapshot]);
  const keySignature = keys.join("\0");
  useEffect(() => {
    let retired = false;
    setLocalized({ context: "", values: {} });
    if (!runtime || !keys.length) return undefined;
    (async () => {
      try {
        const header = await runtime.localize(language, []);
        const identity = (response) => {
          if (response?.language !== language || !response.buildId || !response.gameRoot || !response.localeSha256 || !response.englishSha256 || !response.texts) throw new Error("Game text source could not be verified.");
          return [response.buildId, response.gameRoot.toLowerCase(), response.localeSha256, response.englishSha256].join("|");
        };
        const expected = identity(header), values = {};
        for (let offset = 0; offset < keys.length; offset += 200) {
          if (retired) return;
          const batch = keys.slice(offset, offset + 200), response = await runtime.localize(language, batch);
          if (identity(response) !== expected || batch.some((key) => typeof response.texts[key] !== "string")) throw new Error("Game text source changed during equipment name loading.");
          Object.assign(values, response.texts);
        }
        if (!retired && activeRuntime.current === runtime) setLocalized({ context: textContext, values });
      } catch { /* Native identifiers remain visible if an installed text is unavailable. */ }
    })();
    return () => { retired = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runtime, language, keySignature]);
  const name = (key, fallback) => texts[key] && texts[key] !== key ? texts[key] : fallback;
  const items = useMemo(() => new Map((snapshot?.items || []).map((item) => [String(item.equipUuid), item])), [snapshot]);

  const disabled = busy || !online || !runtime || !snapshot || !healthy || runtime.pending;
  const gearName = (gear) => gear ? name(gear.nameKey, gear.name || `#${gear.itemId || gear.equipUuid}`) : t("squad.emptyEquipment");
  const perform = async (operation) => {
    if (action.current || !runtime) return;
    const owner = runtime;
    action.current = true; mutationGeneration.current += 1; syncError.current = false; setBusy(true); setError(""); setNotice(""); setDragged(null); setDropTarget("");
    try {
      const fresh = await operation();
      owner.assertOwner();
      if (activeRuntime.current === owner) { setSnapshot(fresh); setHealthy(true); setNotice("squad.liveEquipmentUpdated"); }
    } catch (failure) {
      if (activeRuntime.current === owner) {
        setError(String(failure.message || failure));
        try { const fresh = await owner.snapshot(); if (activeRuntime.current === owner) setSnapshot(fresh); } catch { setHealthy(false); }
      }
    } finally {
      action.current = false;
      if (activeRuntime.current === owner) setBusy(false);
    }
  };
  const change = (position, slot, equipUuid) => {
    if (disabled) return;
    if (String(position.equips.find((item) => item.slot === slot)?.equipUuid || "") === equipUuid) return;
    perform(() => runtime.edit(position, slot, equipUuid, snapshot));
  };
  const startDrag = (event, gear) => {
    if (disabled) { event.preventDefault(); return; }
    event.stopPropagation();
    if (event.dataTransfer) { event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", gear.equipUuid); }
    setDragged(gear); setError("");
  };
  return <div className="equipment-preset-main equipment-live" data-native-equipment="true" data-equipment-busy={busy || undefined}>
    <div className="equipment-preset-toolbar">
      <div><strong>{t("squad.liveEquipment")}</strong><span>{t("squad.liveEquipmentHelp")}</span></div>
      <div className="equipment-preset-actions">
        <button type="button" disabled={busy || !runtime || runtime.pending} onClick={() => setRevision((value) => value + 1)}>{t("common.refresh")}</button>
        {runtime?.pending ? <button type="button" data-equipment-action="resume" disabled={busy || !online} onClick={() => perform(() => runtime.resume())}>{t("squad.liveEquipmentConfirm")}</button> : null}
      </div>
    </div>
    <div role="status" data-equipment-sync={busy || runtime?.pending ? "updating" : healthy ? "synced" : "offline"}>{t(busy || runtime?.pending ? "squad.liveEquipmentUpdating" : healthy ? "squad.liveEquipmentSynced" : "status.gameDisconnectedDisabled")}</div>
    {error ? <div className="equipment-result equipment-result-rejected" role="alert">{error}</div> : null}
    {notice && !busy ? <div className="equipment-result equipment-result-success" role="status">{t(notice)}</div> : null}
    <div className="equipment-preset-squads">{(snapshot?.squads || []).map((squad) => <section className="equipment-preset-squad" key={squad.squadIndex}>
      <div className="equipment-preset-squad-header"><strong>{t("squad.number", { number: squad.squadIndex })}</strong></div>
      <div className="equipment-preset-positions">{squad.positions.map((position) => {
        const heroName = name(position.heroNameKey || position.nameKey, position.heroName || `#${position.heroItemId || position.heroUuid}`);
        return <article className="equipment-position-card" data-equipment-position={`${squad.squadIndex}:${position.position}`} data-hero-uuid={position.heroUuid} key={position.position}>
          <div className="equipment-position-header"><strong>{t("squad.position", { number: position.position })}</strong><span>{t("squad.heroFixed")}</span></div>
          <div className="equipment-position-hero">{position.iconPath ? <GameAssetImage assetPath={position.iconPath} alt={heroName} className="equipment-position-hero-icon" /> : null}<div><strong>{heroName}</strong>{position.level != null ? <span>Lv.{position.level}</span> : null}</div></div>
          <div className="equipment-position-items" style={{ gridTemplateColumns: "repeat(2,minmax(0,1fr))" }}>{equipmentSlots.map((slot) => {
            const equipUuid = String(position.equips.find((item) => item.slot === slot)?.equipUuid || ""), gear = items.get(equipUuid);
            const key = `${squad.squadIndex}:${position.position}:${slot}`;
            return <label key={slot} data-equipment-slot={key} draggable={Boolean(gear) && !disabled} className={`preset-equipment-slot quality-${gear?.quality || 0} ${dropTarget === key ? "drop-target" : ""}`}
              onDragStart={(event) => { if (gear) startDrag(event, gear); else event.preventDefault(); }}
              onDragEnd={() => { setDragged(null); setDropTarget(""); }}
              onDragOver={(event) => { if (!dragged || disabled) return; event.preventDefault(); event.stopPropagation(); if (dragged.slot === slot) { setDropTarget(key); } else { setDropTarget(""); setError(t("squad.sameSlotRequired")); } }}
              onDragLeave={() => setDropTarget("")}
              onDrop={(event) => { event.preventDefault(); event.stopPropagation(); setDropTarget(""); if (!dragged || disabled) return; if (dragged.slot !== slot) { setError(t("squad.sameSlotRequired")); return; } change(position, slot, dragged.equipUuid); }}>
              <strong>{t(`squad.equipmentSlot${slot}`)}</strong>
              {gear?.iconPath ? <GameAssetImage assetPath={gear.iconPath} alt={gearName(gear)} className="equipment-icon" /> : null}
              <span>{gearName(gear)}</span>
              <select aria-label={key} value={equipUuid} disabled={disabled} onChange={(event) => change(position, slot, event.target.value)}>
                <option value="">{t("squad.emptyEquipment")}</option>
                {[...items.values()].filter((entry) => entry.slot === slot).map((entry) => <option value={entry.equipUuid} key={entry.equipUuid}>{gearName(entry)} · {entry.equipUuid}</option>)}
              </select>
            </label>;
          })}</div>
        </article>;
      })}</div>
    </section>)}</div>
    {snapshot ? <section className="equipment-preset-squad"><strong>{t("squad.liveEquipmentInventory")}</strong><div className="equipment-live-inventory">{snapshot.items.filter((item) => !item.heroUuid).map((gear) => <div key={gear.equipUuid} data-inventory-equip={gear.equipUuid} className="preset-equipment-slot" draggable={!disabled} onDragStart={(event) => startDrag(event, gear)} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>
      {gear.iconPath ? <GameAssetImage assetPath={gear.iconPath} alt={gearName(gear)} className="equipment-icon" /> : null}<span>{gearName(gear)}</span><small>{t(`squad.equipmentSlot${gear.slot}`)}</small>
    </div>)}</div></section> : null}
  </div>;
}
