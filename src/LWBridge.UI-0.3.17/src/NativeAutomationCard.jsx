import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { NativeCardHeader, NativeCardSettings, NativeCardStatus, useRememberedExpansion } from "./NativeCardShell.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { AUTOMATION_TASKS, createAutomationAdapter, validAutomationDraft } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { subscribeAutomationStatus } from "./automationStatusHub.js";
import { MISSION_KINDS, NativeMissionFields, toggleMissionMaster } from "./NativeMissionFields.jsx";
import { AutomationIcon } from "./AutomationPage.jsx";

const INTERVAL_KINDS = ["allianceGift", "strongholdResource", "allianceCenterResource"];

// Dispatch Assist settings live inside the original Secret Task card but are a separate native service.
function NativeDispatchAssist({ profileId, online, bridge, runtime }) {
  const kind = "dispatchAssist";
  const { t } = useI18n();
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, kind, online, draft => validAutomationDraft(kind, draft)), `automation-native:${profileId}:${kind}`);
  const [ready, setReady] = useState(false), [error, setError] = useState("");
  useEffect(() => { setReady(false);void config.store.refresh().then(() => setReady(true)).catch(e => setError(e.message)); }, [config.store, online, profileId]);
  return <div className="automation-dispatch-assist" data-native-automation={kind} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <NativeMissionFields kind={kind} config={config} disabled={!ready || !online || config.saving} runtime={runtime} />
    {runtime?.confirmed > 0 ? <div className="automation-status-row"><span>{t("automation.confirmedCount")}</span><strong>{runtime.confirmed}</strong></div> : null}
    {runtime?.before?.reason ? <p className="automation-card-note">{runtime.before.reason}</p> : null}
    {error ? <div role="alert" className="automation-error">{error}</div> : null}
  </div>;
}

export function NativeAutomationCard({ title, description, profileId, online, bridge = backendBridge }) {
  const { english, t } = useI18n();
  const kind = AUTOMATION_TASKS[title];
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, kind, online, draft => validAutomationDraft(kind, draft)), `automation-native:${profileId}:${kind}`);
  const [ready, setReady] = useState(false), [error, setError] = useState(""), [statusError, setStatusError] = useState(""), [services, setServices] = useState(null), [busy, setBusy] = useState(false), [dragged, setDragged] = useState(null), [dragOver, setDragOver] = useState(null);
  const runtime = services?.[kind] ?? null;
  // Original 0.3.17: the multi-option cards collapse their settings (closed by default, remembered).
  const collapsible = ["dispatch", "allianceGather"].includes(kind);
  const [expanded, toggleExpanded] = useRememberedExpansion(`automation-settings:${profileId}:${kind}`);
  useEffect(() => {
    let closed = false;
    setReady(false);setServices(null);
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    const unsubscribe = online ? subscribeAutomationStatus(bridge, profileId, services => { if (!closed) { setServices(services);setStatusError(""); } }, e => { if (!closed) setStatusError(e.message); }) : () => {};
    return () => { closed = true;unsubscribe(); };
  }, [config.store, online, profileId, kind]);
  const disabled = !ready || !online || busy || config.saving;
  const toggle = async () => {
    const enabled = !config.draft.enabled;
    setBusy(true);setError("");
    try {
      if (!enabled) { assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_stop", { kind }); }
      config.store.edit(draft => MISSION_KINDS.includes(kind) ? toggleMissionMaster(kind, draft, enabled) : ({ ...draft, enabled }), false);
      await config.store.flush();
    } catch (e) { setError(e.message === "CONFIG_DRAFT_INVALID" ? t("configSave.failed") : e.message); }
    finally { setBusy(false); }
  };
  const flush = () => { void config.store.flush().catch(e => setError(e.message)); };
  const numberField = (key, label, min, max, fallback) => <div className="automation-actions"><label>{t(label)}<input type="number" min={min} max={max} step="1" value={config.draft[key] ?? fallback} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, [key]: event.target.value }))} onBlur={flush} /></label></div>;
  const counts = { treatment: ["wounded", "treating", "soldiersQueued", "helpsRequested", "collected"], allianceGift: ["normalClaimed", "advancedClaimed"], allianceDonate: ["remaining", "donated", "scienceId"] }[kind] ?? [];
  const rows = [...counts.map(key => [t(key === "scienceId" ? "automation.currentTech" : `automation.${key}`), runtime?.[key] ?? (runtime?.after ?? runtime?.before)?.[key]]),
    [t("automation.confirmedCount"), runtime?.confirmed > 0 ? runtime.confirmed : null]];
  const note = runtime?.before?.reason && runtime?.state !== "disabled" ? runtime.before.reason : null;
  const errors = [runtime?.error, statusError, error && <>{error}<button type="button" className="automation-error-retry" onClick={() => { void config.store.refresh().then(() => { setReady(true);setError(""); }).catch(e => setError(e.message)); }}>{t("common.retry")}</button></>];
  const allianceGather = kind === "allianceGather" ? (() => {
    const order = Array.isArray(config.draft.allianceGatherSquads) ? config.draft.allianceGatherSquads : [1, 2];
    const listed = [...order, ...[1, 2, 3, 4].filter(slot => !order.includes(slot))];
    const save = next => {
      if (config.draft.enabled && next.length === 0) { setError(t("automation.allianceGatherSquadRequired"));return; }
      setError("");config.store.edit(draft => ({ ...draft, allianceGatherSquads: next }), false);flush();
    };
    return <>
      <p className="muted">{t("automation.allianceGatherSquadPriority")}</p>
      <div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.allianceGatherSquadPriority")}>{listed.map(slot => {
        const position = order.indexOf(slot);
        const selected = position >= 0;
        // Recovered 0.3.17: selected squads are reordered by dragging (drag handle), not buttons.
        return <div className={`automation-squad-priority-item${selected ? " selected" : ""}${dragged === slot ? " dragging" : ""}${dragOver === slot ? " drag-over" : ""}`} key={slot} draggable={selected && !disabled}
          onDragStart={event => { if (!selected) return; if (event.dataTransfer) { event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(slot)); } setDragged(slot); }}
          onDragOver={event => { if (selected && dragged !== null && dragged !== slot) { event.preventDefault(); if (event.dataTransfer) event.dataTransfer.dropEffect = "move"; setDragOver(slot); } }}
          onDrop={event => { event.preventDefault(); const source = order.indexOf(dragged);setDragged(null);setDragOver(null); if (source < 0 || position < 0 || source === position) return; const next = [...order];next.splice(position, 0, next.splice(source, 1)[0]);save(next); }}
          onDragEnd={() => { setDragged(null);setDragOver(null); }}>
          <label><input type="checkbox" checked={selected} disabled={disabled} onChange={event => save(event.target.checked ? [...order, slot] : order.filter(value => value !== slot))} /><span>{t("automation.squad", { index: slot })}</span></label>
          {selected ? <span className="automation-squad-drag-handle" aria-hidden="true"><AutomationIcon name="drag" /></span> : null}
        </div>;
      })}</div>
    </>;
  })() : null;
  if (kind === "allianceGather") rows.unshift([t("automation.allianceGatherAvailable"), runtime?.before?.available ?? (runtime ? 0 : null)]);
  const settings = kind === "treatment" ? numberField("amountPerArmy", "automation.treatmentAmountPerArmy", 1, 1000000, 1)
    : INTERVAL_KINDS.includes(kind) ? numberField("intervalMinutes", "automation.intervalMinutes", 1, 1440, kind === "allianceGift" ? 120 : 60)
    : kind === "allianceDonate" ? numberField("threshold", "automation.donateThreshold", 1, 30, 15)
    : kind === "officialPosition" ? <div className="automation-actions"><label>{t("automation.targetPosition")}<select value={config.draft.positionId ?? 0} onChange={event => {
      config.store.edit(draft => ({ ...draft, positionId: Number(event.target.value) }), false);flush();
    }}>{[[0, "none"], [10002, "vicePresident"], [10003, "strategyMinister"], [10004, "defenseMinister"], [10005, "constructionMinister"], [10006, "scienceMinister"], [10007, "internalAffairsMinister"]].map(([id, name]) => <option key={id} value={id}>{t(`automation.position.${name}`)}</option>)}</select></label></div>
    : MISSION_KINDS.includes(kind) || kind === "allianceGather" ? <>
      {MISSION_KINDS.includes(kind) ? <NativeMissionFields kind={kind} config={config} disabled={disabled} runtime={runtime} /> : null}
      {allianceGather}
      {kind === "dispatch" ? <NativeDispatchAssist profileId={profileId} online={online} bridge={bridge} runtime={services?.dispatchAssist ?? null} /> : null}
    </> : null;
  return <article className={`automation-card${config.draft.enabled ? " is-enabled" : ""}`} data-native-automation={kind} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <NativeCardHeader title={english(title)} description={english(description)} checked={config.draft.enabled} onToggle={toggle}
      disabled={disabled || kind === "officialPosition" && !config.draft.enabled && !Number(config.draft.positionId)} />
    <NativeCardStatus t={t} online={online} enabled={config.draft.enabled === true} state={runtime?.before?.done ? "success" : runtime?.state} rows={rows} note={note} errors={errors} />
    <PreviewConfigError config={config} t={t} />
    {settings ? <NativeCardSettings t={t} collapsible={collapsible} expanded={expanded} onToggle={toggleExpanded} disabled={disabled}>{settings}</NativeCardSettings> : null}
  </article>;
}
