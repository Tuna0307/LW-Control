import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { Switch } from "./sharedPageUI.jsx";
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
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {runtime?.before?.reason ? <p className="muted">{runtime.before.reason}</p> : null}
    {error ? <p role="alert" className="error-text">{error}</p> : null}
  </div>;
}

export function NativeAutomationCard({ title, description, profileId, online, bridge = backendBridge }) {
  const { english, t } = useI18n();
  const kind = AUTOMATION_TASKS[title];
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, kind, online, draft => validAutomationDraft(kind, draft)), `automation-native:${profileId}:${kind}`);
  const [ready, setReady] = useState(false), [error, setError] = useState(""), [services, setServices] = useState(null), [busy, setBusy] = useState(false), [dragged, setDragged] = useState(null), [dragOver, setDragOver] = useState(null);
  const runtime = services?.[kind] ?? null;
  // Original 0.3.17: the multi-option cards collapse their settings (closed by default, remembered).
  const collapsible = ["dispatch", "allianceGather"].includes(kind);
  const expansionKey = `automation-settings:${profileId}:${kind}`;
  const [expanded, setExpanded] = useState(() => { try { return localStorage.getItem(expansionKey) === "true"; } catch { return false; } });
  const toggleExpanded = () => { const next = !expanded;setExpanded(next);try { localStorage.setItem(expansionKey, String(next)); } catch { /* Storage may be unavailable. */ } };
  useEffect(() => {
    let closed = false;
    setReady(false);setServices(null);
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    const unsubscribe = online ? subscribeAutomationStatus(bridge, profileId, services => { if (!closed) setServices(services); }, e => { if (!closed) setError(e.message); }) : () => {};
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
  const state = !online ? t("status.disconnected") : runtime?.state === "nothing_available" ? t("automation.nothingAvailable") : runtime?.state === "success" ? t("common.success") : runtime?.state === "disabled" || !config.draft.enabled && !runtime ? t("common.disabled") : runtime?.state === "unconfirmed" ? t("automation.resultUnconfirmed") : t("common.waiting");
  return <article className="automation-card" data-native-automation={kind} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <div className="automation-card-header"><div className="automation-card-title-group"><h3>{english(title)}</h3><p>{english(description)}</p></div>
      <button type="button" role="switch" aria-label={english(title)} aria-checked={config.draft.enabled} disabled={disabled || kind === "officialPosition" && !config.draft.enabled && !Number(config.draft.positionId)} onClick={toggle}><Switch checked={config.draft.enabled} /></button></div>
    <p role="status">{state}</p>
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {kind === "treatment" ? <div className="automation-status-grid">{["wounded", "treating", "soldiersQueued", "helpsRequested", "collected"].filter(key => (runtime?.after ?? runtime?.before)?.[key] != null || runtime?.[key] != null).map(key => <div className="automation-status-row" key={key}><span>{t(`automation.${key}`)}</span><strong>{runtime?.[key] ?? (runtime?.after ?? runtime?.before)?.[key]}</strong></div>)}</div> : null}
    {kind === "allianceGift" ? <div className="automation-status-grid">{["normalClaimed", "advancedClaimed"].filter(key => runtime?.[key] != null).map(key => <div className="automation-status-row" key={key}><span>{t(`automation.${key}`)}</span><strong>{runtime[key]}</strong></div>)}</div> : null}
    {kind === "allianceDonate" ? <div className="automation-status-grid">{["remaining", "donated", "scienceId"].filter(key => runtime?.[key] != null).map(key => <div className="automation-status-row" key={key}><span>{t(key === "scienceId" ? "automation.currentTech" : `automation.${key}`)}</span><strong>{runtime[key]}</strong></div>)}</div> : null}
    {runtime?.error ? <p role="alert" className="error-text">{runtime.error}</p> : null}
    {runtime?.before?.reason ? <p className="muted">{runtime.before.reason}</p> : null}
    {error ? <p role="alert" className="error-text">{error}<button type="button" onClick={() => { void config.store.refresh().then(() => { setReady(true);setError(""); }).catch(e => setError(e.message)); }}>{t("common.retry")}</button></p> : null}
    <PreviewConfigError config={config} t={t} />
    {kind === "treatment" ? <div className="automation-actions"><label>{t("automation.treatmentAmountPerArmy")}<input type="number" min="1" max="1000000" step="1" value={config.draft.amountPerArmy ?? 1} disabled={disabled} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, amountPerArmy: event.target.value }))} onBlur={() => { void config.store.flush().catch(e => setError(e.message)); }} /></label></div> : null}
    {INTERVAL_KINDS.includes(kind) ? <div className="automation-actions"><label>{t("automation.intervalMinutes")}<input type="number" min="1" max="1440" step="1" value={config.draft.intervalMinutes ?? (kind === "allianceGift" ? 120 : 60)} disabled={disabled} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, intervalMinutes: event.target.value }))} onBlur={() => { void config.store.flush().catch(e => setError(e.message)); }} /></label></div> : null}
    {kind === "allianceDonate" ? <div className="automation-actions"><label>{t("automation.donateThreshold")}<input type="number" min="1" max="30" step="1" value={config.draft.threshold ?? 15} disabled={disabled} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, threshold: event.target.value }))} onBlur={() => { void config.store.flush().catch(e => setError(e.message)); }} /></label></div> : null}
    {kind === "officialPosition" ? <div className="automation-actions"><label>{t("automation.targetPosition")}<select value={config.draft.positionId ?? 0} disabled={disabled} onChange={event => {
      config.store.edit(draft => ({ ...draft, positionId: Number(event.target.value) }), false);void config.store.flush().catch(e => setError(e.message));
    }}>{[[0, "none"], [10002, "vicePresident"], [10003, "strategyMinister"], [10004, "defenseMinister"], [10005, "constructionMinister"], [10006, "scienceMinister"], [10007, "internalAffairsMinister"]].map(([id, name]) => <option key={id} value={id}>{t(`automation.position.${name}`)}</option>)}</select></label></div> : null}
    {collapsible ? <div className="automation-config-actions">
      {collapsible ? <button type="button" className={`automation-config-trigger${expanded ? " is-open" : ""}`} aria-expanded={expanded} onClick={toggleExpanded}><span className="automation-config-trigger-label"><span>{t("settings.title")}</span></span><svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="M4 6l4 4 4-4" /></svg></button> : null}
    </div> : null}
    {collapsible ? <div className="automation-config-body" style={expanded ? undefined : { display: "none" }}>
    {MISSION_KINDS.includes(kind) ? <NativeMissionFields kind={kind} config={config} disabled={disabled} runtime={runtime} /> : null}
    {kind === "allianceGather" ? (() => {
      const order = Array.isArray(config.draft.allianceGatherSquads) ? config.draft.allianceGatherSquads : [1, 2];
      const listed = [...order, ...[1, 2, 3, 4].filter(slot => !order.includes(slot))];
      const save = next => {
        if (config.draft.enabled && next.length === 0) { setError(t("automation.allianceGatherSquadRequired"));return; }
        setError("");config.store.edit(draft => ({ ...draft, allianceGatherSquads: next }), false);void config.store.flush().catch(e => setError(e.message));
      };
      return <div className="automation-actions"><p className="muted">{t("automation.allianceGatherSquadPriority")}</p>
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
        {runtime?.after?.dispatched || runtime?.before?.available != null ? <div className="automation-status-row"><span>{t("automation.allianceGatherAvailable")}</span><strong>{runtime?.before?.available ?? 0}</strong></div> : null}
      </div>;
    })() : null}
    {kind === "dispatch" ? <NativeDispatchAssist profileId={profileId} online={online} bridge={bridge} runtime={services?.dispatchAssist ?? null} /> : null}
    </div> : null}
  </article>;
}
