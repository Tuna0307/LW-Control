import { useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { backendBridge } from "./backendBridge.js";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { createEquipmentRuntime, equipmentPlanChanges, equipmentPositionSnapshot, equipmentPresetCoversSnapshot, equipmentSlots, initializeEquipmentPresets, normalizeEquipmentConfig, setEquipmentSlot, swapNativeEquipmentSquads, swapNativeEquipmentTarget } from "./equipmentRuntime.js";

const clone = (value) => structuredClone(value);
const itemCount = (preset) => preset?.squads.reduce((count, squad) => count + squad.positions.reduce((sum, position) => sum + position.equips.length, 0), 0) || 0;
const positionCount = (preset) => preset?.squads.reduce((count, squad) => count + squad.positions.length, 0) || 0;
export function NativeEquipmentContent({ profileId = "", online = true, refreshCount = 0, bridge = backendBridge }) {
  const { t, language } = useI18n();
  const [snapshot, setSnapshot] = useState(null), [config, setConfig] = useState(null);
  const [selectedIndex, setSelectedIndex] = useState(0), [busy, setBusy] = useState("");
  const [error, setError] = useState(""), [notice, setNotice] = useState(""), [localized, setLocalized] = useState({ context: "", values: {} });
  const [dragged, setDragged] = useState(null), [dropTarget, setDropTarget] = useState("");
  const [revision, setRevision] = useState(0);
  const runtime = useMemo(() => {
    if (!profileId || !bridge.available) return null;
    try { return createEquipmentRuntime(bridge, profileId); } catch { return null; }
  }, [bridge, profileId]);
  const generation = useRef(0), activeRuntime = useRef(runtime);
  activeRuntime.current = runtime;
  const textContext = `${profileId}|${language}`;
  const texts = localized.context === textContext ? localized.values : {};
  const normalized = (value) => normalizeEquipmentConfig(value, (number) => t("squad.presetDefaultName", { number }));
  useEffect(() => {
    const current = ++generation.current;
    let retired = false;
    setSnapshot(null); setConfig(null); setError(""); setNotice(""); setLocalized({ context: "", values: {} });
    if (!runtime) return undefined;
    setBusy("read");
    (async () => {
      try {
        const saved = await runtime.readConfig();
        const fresh = await runtime.snapshot();
        if (retired || generation.current !== current) return;
        setSnapshot(fresh); setConfig(initializeEquipmentPresets(normalized(saved), fresh));
      } catch (failure) { if (!retired && generation.current === current) setError(String(failure.message || failure)); }
      finally { if (!retired && generation.current === current) setBusy(""); }
    })();
    return () => { retired = true; generation.current += 1; };
  // Translation changes update names without discarding unsaved equipment edits.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  // A transport reconnect need not change profileId or bridge identity. Read
  // again on its connection transition, keeping this runtime's pending-send
  // fence so reconnection can never repeat an uncertain equipment mutation.
  }, [runtime, online, refreshCount, revision]);
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
  const selected = config?.equipmentPresets[selectedIndex];
  const changes = useMemo(() => new Set(equipmentPlanChanges(selected, snapshot)), [selected, snapshot]);
  const capturedCurrentSquads = useMemo(() => equipmentPresetCoversSnapshot(selected, snapshot), [selected, snapshot]);
  const disabled = Boolean(busy || !online || !runtime || !snapshot || !config);
  const operation = async (kind, action) => {
    if (disabled) return;
    const captured = runtime;
    setBusy(kind); setError(""); setNotice("");
    try {
      const result = await action();
      captured.assertOwner();
      if (activeRuntime.current !== captured) return;
      if (result?.snapshot) setSnapshot(result.snapshot);
      if (result?.config) setConfig(normalized(result.config));
      const applied = kind === "apply" || kind === "confirm";
      const matches = result?.snapshot && equipmentPresetCoversSnapshot(selected, result.snapshot) && !equipmentPlanChanges(selected, result.snapshot).length;
      setNotice(t(applied ? (matches ? "squad.equipmentApplied" : "squad.equipmentStillDifferent") : kind === "restore" ? "squad.initialEquipmentRestored" : kind === "capture" ? "squad.presetUpdated" : "squad.equipmentPlanSaved"));
    } catch (failure) { if (activeRuntime.current === captured) setError(String(failure.message || failure)); }
    finally { if (activeRuntime.current === captured) setBusy(""); }
  };
  const capture = () => operation("capture", async () => {
    const fresh = await runtime.snapshot(), next = clone(config);
    next.equipmentPresets[selectedIndex].squads = equipmentPositionSnapshot(fresh);
    next.equipmentPresets[selectedIndex].initializedFromCurrent = true;
    return { config: await runtime.save(next, fresh), snapshot: fresh };
  });
  const save = () => operation("save", async () => ({ config: await runtime.save(config, snapshot) }));
  const apply = () => operation("apply", async () => {
    // Refresh the native owner snapshot immediately before Apply. The game can
    // move an item between heroes after the tab was rendered; a stale rendered
    // snapshot must never be the baseline for a mutation.
    const fresh = await runtime.snapshot();
    setSnapshot(fresh);
    return runtime.apply(selectedIndex + 1, config, fresh);
  });
  const dragTarget = (target) => {
    if (!dragged || !capturedCurrentSquads || disabled || runtime?.pending) return;
    const result = swapNativeEquipmentTarget(config, selectedIndex, dragged, target);
    if (result.error) { setNotice(t(result.error)); return; }
    if (result.handled) { setConfig(result.config); setNotice(""); }
  };
  const startDrag = (event, target) => {
    event.stopPropagation();
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = "move";
      event.dataTransfer.setData("text/plain", JSON.stringify(target));
    }
    setError(""); setNotice(""); setDragged(target);
  };
  const dragSquad = (targetSquadIndex) => {
    if (dragged?.kind !== "squad" || !capturedCurrentSquads || disabled || runtime?.pending) return;
    const result = swapNativeEquipmentSquads(config, selectedIndex, dragged.squadIndex, targetSquadIndex, snapshot);
    if (result.handled) { setConfig(result.config); setNotice(""); }
  };
  const editName = (value) => setConfig((current) => {
    const next = clone(current); next.equipmentPresets[selectedIndex].name = value; return next;
  });
  return <div className="equipment-preset-layout" data-native-equipment="true" data-equipment-busy={busy || undefined}>
    {changes.size ? <div className="equipment-plan-banner" data-equipment-plan-banner="true" role="status"><strong>{t("squad.equipmentPlanOnly", { number: selectedIndex + 1 })}</strong><span data-equipment-change-count={changes.size}>{t("squad.equipmentUnappliedCount", { count: changes.size })}</span></div> : null}
    {error ? <div className="equipment-result equipment-result-rejected" style={{ gridColumn: "1 / -1" }} role="alert">{error}</div> : null}
    {!runtime || !online ? <div className="map-empty" style={{ gridColumn: "1 / -1" }}>{t("status.gameDisconnectedDisabled")}</div> : null}
    <aside className="equipment-preset-rail">
      <strong>{t("squad.equipmentPresets")}</strong>
      <div className="equipment-preset-list">{(config?.equipmentPresets || []).map((preset, index) => <button type="button" key={preset.id} className={index === selectedIndex ? "active" : ""} disabled={Boolean(busy)} onClick={() => setSelectedIndex(index)}><span title={preset.name}>{preset.name}</span><small>Alt+{index + 1} · {t("squad.presetSummary", { positions: positionCount(preset), equips: itemCount(preset) })}</small></button>)}</div>
    </aside>
    <main className="equipment-preset-main">
      <div className="equipment-preset-toolbar">
        <div><strong>{selected?.name || t("squad.equipmentPresets")}</strong><span>{t("squad.presetSummary", { positions: positionCount(selected), equips: itemCount(selected) })}</span></div>
        <div className="equipment-preset-actions">
          <button type="button" disabled={Boolean(busy) || !runtime} onClick={() => setRevision((value) => value + 1)}>{t("common.refresh")}</button>
          <button type="button" data-equipment-action="capture" disabled={disabled || runtime?.pending} onClick={capture}>{t("squad.overwritePreset")}</button>
          <button type="button" data-equipment-action="save" disabled={disabled || !selected?.name.trim() || runtime?.pending} onClick={save}>{t("squad.saveEquipmentConfig")}</button>
          <button type="button" className="primary" data-equipment-action="apply" disabled={disabled || !selected?.name.trim() || !positionCount(selected) || runtime?.pending} onClick={apply}>{t(busy === "apply" ? "squad.equipmentApplying" : "squad.saveAndApplyEquipmentConfig")}</button>
          <button type="button" data-equipment-action="restore" disabled={disabled || !config?.initialEquipmentConfig || runtime?.pending} onClick={() => operation("restore", () => runtime.restore())}>{t("squad.restoreInitialEquipment")}</button>
          {runtime?.pending ? <button type="button" disabled={disabled} data-equipment-action="resume" onClick={() => operation("confirm", () => runtime.resume())}>{t("common.retry")}</button> : null}
        </div>
      </div>
      {selected ? <label className="equipment-preset-actions">{t("squad.presetNamePrompt")}<input aria-label={t("squad.presetNamePrompt")} value={selected.name} disabled={disabled || runtime?.pending} onChange={(event) => editName(event.target.value)} /></label> : null}
      {selected && !capturedCurrentSquads ? <p className="equipment-preset-hint">{t("squad.equipmentCaptureHint")}</p> : null}
      <div className="equipment-preset-squads">{(snapshot?.squads || []).map((squad) => {
        const squadDropKey = `squad-${squad.squadIndex}`;
        return <section className={`equipment-preset-squad ${dropTarget === squadDropKey ? "drop-target" : ""}`} key={squad.squadIndex}
          onDragOver={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); setDropTarget(squadDropKey); } }}
          onDragLeave={() => setDropTarget((current) => current === squadDropKey ? "" : current)}
          onDrop={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); setDropTarget(""); dragSquad(squad.squadIndex); } }}>
        <div className="equipment-preset-squad-header"><strong>{t("squad.number", { number: squad.squadIndex })}</strong>
          <div className="equipment-squad-drag-handle" data-equipment-drag={`squad:${squad.squadIndex}`} draggable={capturedCurrentSquads && !disabled && !runtime?.pending}
            onDragStart={(event) => startDrag(event, { kind: "squad", squadIndex: squad.squadIndex })}
            onDragEnd={() => { setDragged(null); setDropTarget(""); }}>{t("squad.dragSquadLoadout")}</div>
        </div>
        <div className="equipment-preset-positions">{squad.positions.map((position) => {
          const saved = selected?.squads.find((entry) => entry.squadIndex === squad.squadIndex)?.positions.find((entry) => entry.position === position.position);
          const heroName = name(position.heroNameKey || position.nameKey, position.heroName || `#${position.heroItemId || position.heroUuid}`);
          const loadoutDropKey = `loadout-${squad.squadIndex}:${position.position}`;
          return <article className={`equipment-position-card ${dropTarget === loadoutDropKey ? "drop-target" : ""}`} key={position.position} data-equipment-position={`${squad.squadIndex}:${position.position}`}
            onDragOver={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); setDropTarget(loadoutDropKey); } }}
            onDragLeave={() => setDropTarget((current) => current === loadoutDropKey ? "" : current)}
            onDrop={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); setDropTarget(""); dragTarget({ kind: "loadout", squadIndex: squad.squadIndex, position: position.position }); } }}>
            <div className="equipment-position-header"><strong>{t("squad.position", { number: position.position })}</strong><span>{t("squad.heroFixed")}</span></div>
            <div className="equipment-position-hero">{position.iconPath ? <GameAssetImage assetPath={position.iconPath} alt={heroName} className="equipment-position-hero-icon" /> : null}<div><strong title={heroName}>{heroName}</strong>{position.level != null ? <span>Lv.{position.level}</span> : null}</div></div>
            <div className="equipment-loadout-handle" data-equipment-drag={`loadout:${squad.squadIndex}:${position.position}`} draggable={capturedCurrentSquads && !disabled && !runtime?.pending}
              onDragStart={(event) => startDrag(event, { kind: "loadout", squadIndex: squad.squadIndex, position: position.position })}
              onDragEnd={() => { setDragged(null); setDropTarget(""); }}><span>{t("squad.dragLoadout")}</span></div>
            <div className="equipment-position-items" style={{ gridTemplateColumns: "repeat(2,minmax(0,1fr))" }}>{equipmentSlots.map((slot) => {
              const item = saved?.equips.find((entry) => entry.slot === slot), gear = items.get(String(item?.equipUuid));
              const current = position.equips.find((entry) => entry.slot === slot), currentGear = items.get(String(current?.equipUuid));
              const gearName = (value) => value ? name(value.nameKey, value.name || `#${value.itemId || value.equipUuid}`) : t("squad.emptyEquipment");
              const equipDropKey = `equip-${squad.squadIndex}:${position.position}:${slot}`;
              const changed = changes.has(`${squad.squadIndex}:${position.position}:${slot}`);
              return <label className={`preset-equipment-slot quality-${gear?.quality || 0} ${changed ? "equipment-slot-changed" : ""} ${dropTarget === equipDropKey ? "drop-target" : ""}`} key={slot} title={`${t(`squad.equipmentSlot${slot}`)} · ${t("squad.currentEquipmentPreset", { name: gearName(currentGear) })}`}
                data-equipment-slot={`${squad.squadIndex}:${position.position}:${slot}`}
                data-equipment-changed={changed || undefined}
                draggable={capturedCurrentSquads && !!item && !disabled && !runtime?.pending}
                onDragStart={(event) => { if (!item) { event.preventDefault(); return; } startDrag(event, { kind: "equip", squadIndex: squad.squadIndex, position: position.position, slot }); }}
                onDragOver={(event) => { if (dragged?.kind === "equip") { event.preventDefault(); event.stopPropagation(); setDropTarget(dragged.slot === slot ? equipDropKey : `invalid-${equipDropKey}`); if (dragged.slot !== slot) setNotice(t("squad.sameSlotRequired")); } }}
                onDragLeave={(event) => { if (dragged?.kind === "equip") { event.stopPropagation(); setDropTarget(""); } }}
                onDrop={(event) => { if (dragged?.kind === "equip") { event.preventDefault(); event.stopPropagation(); setDropTarget(""); dragTarget({ kind: "equip", squadIndex: squad.squadIndex, position: position.position, slot }); } }}
                onDragEnd={() => { setDragged(null); setDropTarget(""); }}>
                <strong style={{ maxWidth: "100%", overflowWrap: "anywhere", fontSize: "var(--font-footnote)" }}>{t(`squad.equipmentSlot${slot}`)}</strong>
                {gear?.iconPath ? <GameAssetImage assetPath={gear.iconPath} alt={gearName(gear)} className="equipment-icon" /> : null}
                <span className="equipment-plan-slot-name">{t("squad.equipmentPlanSlot", { name: gearName(gear) })}</span>
                <select style={{ width: "100%", minWidth: 0, maxWidth: "100%" }} title={`${gearName(gear)}${item ? ` · ${item.equipUuid}` : ""}`} aria-label={`${squad.squadIndex}:${position.position}:${slot}`} value={String(item?.equipUuid || "")} disabled={disabled || runtime?.pending} onChange={(event) => setConfig(setEquipmentSlot(config, selectedIndex, squad.squadIndex, position.position, slot, event.target.value, snapshot))}>
                  <option value="">{t("squad.emptyEquipment")}</option>
                  {[...items.values()].filter((entry) => entry.slot === slot).map((entry) => <option value={String(entry.equipUuid)} key={entry.equipUuid}>{gearName(entry)}{entry.level != null ? ` · Lv.${entry.level}` : ""} · {entry.equipUuid}</option>)}
                </select>
                {currentGear?.iconPath ? <GameAssetImage assetPath={currentGear.iconPath} alt={gearName(currentGear)} className="equipment-icon" /> : null}
                <span title={gearName(currentGear)}>{t("squad.currentEquipmentSlot", { name: gearName(currentGear) })}</span>
              </label>;
            })}</div>
          </article>;
        })}</div>
      </section>;})}</div>
      {!snapshot?.squads.length && !busy ? <div className="map-empty">{t("squad.empty")}</div> : null}
      {busy ? <div className="equipment-apply-progress" role="status">{t(busy === "apply" || busy === "restore" || busy === "confirm" ? "squad.equipmentApplying" : "common.processing")}</div> : null}
      {notice ? <div className="equipment-result equipment-result-success" role="status">{notice}</div> : null}
    </main>
  </div>;
}
