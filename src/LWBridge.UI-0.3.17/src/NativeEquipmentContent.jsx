import { useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { backendBridge } from "./backendBridge.js";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { createLiveEquipmentRuntime, equipmentSlots, normalizeEquipmentConfig } from "./equipmentRuntime.js";

function equipmentErrorText(t, state) {
  if (!state) return "";
  if (state.key) return t(state.key);
  const code = state.failure?.code || state.code || "";
  if (code === "CONFIG_FILE_BUSY") return t("squad.livePresetSaveFailed");
  if (code === "GAME_COMMAND_NOT_PUBLISHED") return t("squad.liveEquipmentFileBusy");
  if (code === "GAME_CONNECTION_UNAVAILABLE" || code === "GAME_DISCONNECTED") return t("status.gameDisconnectedDisabled");
  if (code === "EQUIPMENT_CONFIRMATION_PENDING") return t("squad.liveEquipmentPending");
  if (code === "PROFILE_GENERATION_RETIRED") return t("squad.liveEquipmentProfileChanged");
  if (code === "GAME_ACCOUNT_CHANGED") return t("squad.liveEquipmentAccountChanged");
  if (["GAME_PRECONDITION_FAILED", "EQUIPMENT_EDIT_REJECTED", "GAME_ACTION_UNCONFIRMED", "EQUIPMENT_STATE_PENDING"].includes(code)) return t("squad.liveEquipmentRejected");
  return t("common.actionFailed");
}

export function NativeEquipmentContent({ profileId = "", online = true, active = true, refreshCount = 0, bridge = backendBridge }) {
  const { t, language } = useI18n();
  const [snapshot, setSnapshot] = useState(null), [busy, setBusy] = useState(""), [healthy, setHealthy] = useState(false);
  const [error, setError] = useState(null), [notice, setNotice] = useState("");
  const [presets, setPresets] = useState(null), [selectedPreset, setSelectedPreset] = useState(0), [presetName, setPresetName] = useState("");
  const [localized, setLocalized] = useState({ context: "", values: {} });
  const [dragged, setDragged] = useState(null), [dropTarget, setDropTarget] = useState("");
  const runtime = useMemo(() => {
    if (!profileId || !bridge.available) return null;
    try { return createLiveEquipmentRuntime(bridge, profileId); } catch { return null; }
  }, [bridge, profileId]);
  const activeRuntime = useRef(runtime), action = useRef(false), mutationGeneration = useRef(0);
  activeRuntime.current = runtime;
  const textContext = `${profileId}|${language}`;
  const texts = localized.context === textContext ? localized.values : {};
  useEffect(() => {
    let retired = false, reading = false, presetsLoaded = false;
    setSnapshot(null); setHealthy(false); setError(null); setNotice(""); setPresets(null);
    if (!runtime || !online || !active) return undefined;
    const fail = (source, failure) => { if (!retired) setError({ source, failure }); };
    const recover = (source) => { if (!retired) setError((current) => current?.source === source ? null : current); };
    const read = async () => {
      if (retired || reading || action.current || runtime.pending) return;
      reading = true;
      const generation = mutationGeneration.current;
      try {
        const fresh = await runtime.snapshot();
        if (!retired && generation === mutationGeneration.current) {
          setSnapshot(fresh); setHealthy(true);
          recover("snapshot");
        }
      } catch (failure) {
        if (!retired && generation === mutationGeneration.current) { setHealthy(false); fail("snapshot", failure); }
      }
      if (!retired && !presetsLoaded && generation === mutationGeneration.current) {
        try {
          const value = await runtime.readPresets();
          if (!retired && generation === mutationGeneration.current) {
            const saved = normalizeEquipmentConfig(value, (number) => t("squad.presetDefaultName", { number }));
            presetsLoaded = true; setPresets(saved); setPresetName(saved.equipmentPresets[selectedPreset].name); recover("presets");
          }
        } catch (failure) { fail("presets", failure); }
      }
      reading = false;
    };
    read();
    const timer = window.setInterval(read, 1500);
    return () => { retired = true; window.clearInterval(timer); };
  // Locale changes must not discard a name being edited.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runtime, online, active, refreshCount]);
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
      } catch { /* User-safe generic names remain visible if installed game text is unavailable. */ }
    })();
    return () => { retired = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runtime, language, keySignature]);
  const name = (key, fallback) => texts[key] && texts[key] !== key ? texts[key] : fallback;
  const items = useMemo(() => new Map((snapshot?.items || []).map((item) => [String(item.equipUuid), item])), [snapshot]);

  const disabled = busy || !online || !runtime || !snapshot || !healthy || runtime.pending;
  const gearName = (gear) => gear ? name(gear.nameKey, gear.name || t("squad.liveEquipmentUnknownItem")) : t("squad.emptyEquipment");
  const gearLevel = (gear) => t(gear?.level != null ? "squad.liveEquipmentLevel" : "squad.liveEquipmentLevelUnknown", { level: gear?.level });
  const selected = presets?.equipmentPresets[selectedPreset];
  const savedPositionCount = (preset) => preset?.squads.reduce((count, squad) => count + squad.positions.length, 0) || 0;
  const perform = async (operation, success = "squad.liveEquipmentUpdated") => {
    if (action.current || !runtime) return;
    const owner = runtime;
    action.current = true; mutationGeneration.current += 1; setBusy(["squad.livePresetCaptured", "squad.livePresetRenamed"].includes(success) ? "preset-save" : "game-change"); setError(null); setNotice(""); setDragged(null); setDropTarget("");
    try {
      const result = await operation(), fresh = result.snapshot || result;
      owner.assertOwner();
      if (activeRuntime.current === owner) {
        setSnapshot(fresh); setHealthy(true); setNotice(success);
        if (result.config) { setPresets(result.config); setPresetName(result.config.equipmentPresets[selectedPreset].name); }
      }
    } catch (failure) {
      if (activeRuntime.current === owner) {
        setError({ source: "action", failure });
        try { const fresh = await owner.snapshot(); if (activeRuntime.current === owner) { setSnapshot(fresh); setHealthy(true); } }
        catch (refreshFailure) { setHealthy(false); setError((current) => current?.source === "action" ? current : { source: "snapshot", failure: refreshFailure }); }
      }
    } finally {
      action.current = false;
      if (activeRuntime.current === owner) setBusy("");
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
    setDragged(gear); setError(null);
  };
  return <div className="equipment-preset-main equipment-live" data-native-equipment="true" data-equipment-busy={busy || undefined}>
    <div className="equipment-preset-toolbar">
      <div><strong>{t("squad.liveEquipment")}</strong><span>{t("squad.liveEquipmentHelp")}</span></div>
    </div>
    <div role="status" data-equipment-sync={busy ? "updating" : runtime?.pending ? "pending" : healthy ? "synced" : "offline"}>{t(busy === "preset-save" ? "common.processing" : busy ? "squad.liveEquipmentUpdating" : runtime?.pending ? "squad.liveEquipmentPending" : healthy ? "squad.liveEquipmentSynced" : "status.gameDisconnectedDisabled")}</div>
    {runtime?.pending && !busy ? <button type="button" data-equipment-action="resume" disabled={!online} onClick={() => perform(() => runtime.resume())}>{t("squad.liveEquipmentConfirm")}</button> : null}
    {error ? <div className="equipment-result equipment-result-rejected" role="alert">{equipmentErrorText(t, error)}</div> : null}
    {notice && !busy ? <div className="equipment-result equipment-result-success" role="status">{t(notice, { number: selectedPreset + 1 })}</div> : null}
    <section className="equipment-live-presets" data-equipment-presets="true">
      <strong>{t("squad.equipmentPresets")}</strong><p>{t("squad.livePresetHelp")}</p>
      <div className="equipment-preset-list">{presets?.equipmentPresets.map((preset, index) => <button type="button" key={preset.id} data-equipment-preset={index + 1} className={selectedPreset === index ? "active" : ""} disabled={busy || runtime?.pending} onClick={() => { setSelectedPreset(index); setPresetName(preset.name); setNotice(""); }}><span>{preset.name}</span><small>Alt+{index + 1} · {t(savedPositionCount(preset) ? "squad.livePresetSaved" : "squad.livePresetEmpty")}</small></button>)}</div>
      <div className="equipment-preset-actions">
        <label>{t("squad.presetNamePrompt")}<input data-equipment-preset-name="true" value={presetName} disabled={disabled || !presets} onChange={(event) => setPresetName(event.target.value)} /></label>
        <button type="button" data-equipment-action="rename-preset" disabled={disabled || !presets || !savedPositionCount(selected) || !presetName.trim() || presetName.trim() === selected?.name} onClick={() => perform(async () => ({ snapshot, config: await runtime.renamePreset(selectedPreset + 1, presetName, selected.id) }), "squad.livePresetRenamed")}>{t("squad.livePresetRename")}</button>
        <button type="button" data-equipment-action="save-preset" disabled={disabled || !presets || !presetName.trim()} onClick={() => perform(() => runtime.savePreset(selectedPreset + 1, presetName, (number) => t("squad.presetDefaultName", { number })), "squad.livePresetCaptured")}>{t("squad.livePresetCapture")}</button>
        <button type="button" data-equipment-action="equip-preset" disabled={disabled || !savedPositionCount(selected)} onClick={() => perform(() => runtime.applyPreset(selectedPreset + 1), "squad.livePresetEquipped")}>{t("squad.livePresetEquip")}</button>
      </div>
    </section>
    <div className="equipment-preset-squads">{(snapshot?.squads || []).map((squad) => <section className="equipment-preset-squad" key={squad.squadIndex}>
      <div className="equipment-preset-squad-header"><strong>{t("squad.number", { number: squad.squadIndex })}</strong></div>
      <div className="equipment-preset-positions">{squad.positions.map((position) => {
        const heroName = name(position.heroNameKey || position.nameKey, position.heroName || t("squad.liveEquipmentUnknownHero"));
        return <article className="equipment-position-card" data-equipment-position={`${squad.squadIndex}:${position.position}`} data-hero-uuid={position.heroUuid} key={position.position}>
          <div className="equipment-position-header"><strong>{t("squad.position", { number: position.position })}</strong><span>{t("squad.heroFixed")}</span></div>
          <div className="equipment-position-hero">{position.iconPath ? <GameAssetImage assetPath={position.iconPath} alt={heroName} className="equipment-position-hero-icon" /> : null}<div><strong>{heroName}</strong>{position.level != null ? <span>Lv.{position.level}</span> : null}</div></div>
          <div className="equipment-position-items" style={{ gridTemplateColumns: "repeat(2,minmax(0,1fr))" }}>{equipmentSlots.map((slot) => {
            const equipUuid = String(position.equips.find((item) => item.slot === slot)?.equipUuid || ""), gear = items.get(equipUuid);
            const key = `${squad.squadIndex}:${position.position}:${slot}`;
            return <label key={slot} data-equipment-slot={key} draggable={Boolean(gear) && !disabled} className={`preset-equipment-slot quality-${gear?.quality || 0} ${dropTarget === key ? "drop-target" : ""}`}
              onDragStart={(event) => { if (gear) startDrag(event, gear); else event.preventDefault(); }}
              onDragEnd={() => { setDragged(null); setDropTarget(""); }}
              onDragOver={(event) => { if (!dragged || disabled) return; event.preventDefault(); event.stopPropagation(); if (dragged.slot === slot) { setDropTarget(key); } else { setDropTarget(""); setError({ source: "validation", key: "squad.sameSlotRequired" }); } }}
              onDragLeave={() => setDropTarget("")}
              onDrop={(event) => { event.preventDefault(); event.stopPropagation(); setDropTarget(""); if (!dragged || disabled) return; if (dragged.slot !== slot) { setError({ source: "validation", key: "squad.sameSlotRequired" }); return; } change(position, slot, dragged.equipUuid); }}>
              <strong>{t(`squad.equipmentSlot${slot}`)}</strong>
              {gear?.iconPath ? <GameAssetImage assetPath={gear.iconPath} alt={gearName(gear)} className="equipment-icon" /> : null}
              <span>{gearName(gear)}</span>
              {gear ? <small data-equipment-level={gear.equipUuid}>{gearLevel(gear)}</small> : null}
              <select aria-label={key} value={equipUuid} disabled={disabled} onChange={(event) => change(position, slot, event.target.value)}>
                <option value="">{t("squad.emptyEquipment")}</option>
                {[...items.values()].filter((entry) => entry.slot === slot).map((entry) => <option value={entry.equipUuid} key={entry.equipUuid}>{gearName(entry)} · {gearLevel(entry)}</option>)}
              </select>
            </label>;
          })}</div>
        </article>;
      })}</div>
    </section>)}</div>
    {snapshot ? <section className="equipment-preset-squad"><strong>{t("squad.liveEquipmentInventory")}</strong><div className="equipment-live-inventory">{snapshot.items.filter((item) => !item.heroUuid).map((gear) => <div key={gear.equipUuid} data-inventory-equip={gear.equipUuid} className="preset-equipment-slot" draggable={!disabled} onDragStart={(event) => startDrag(event, gear)} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>
      {gear.iconPath ? <GameAssetImage assetPath={gear.iconPath} alt={gearName(gear)} className="equipment-icon" /> : null}<span>{gearName(gear)}</span><small data-equipment-level={gear.equipUuid}>{gearLevel(gear)}</small><small>{t(`squad.equipmentSlot${gear.slot}`)}</small>
    </div>)}</div></section> : null}
  </div>;
}
