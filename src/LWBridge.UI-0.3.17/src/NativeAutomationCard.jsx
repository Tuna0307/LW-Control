import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { Switch } from "./sharedPageUI.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { AUTOMATION_TASKS, createAutomationAdapter, validAutomationDraft } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

export function NativeAutomationCard({ title, description, profileId, online, bridge = backendBridge }) {
  const { english, t } = useI18n();
  const kind = AUTOMATION_TASKS[title];
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, kind, online, draft => validAutomationDraft(kind, draft)), `automation-native:${profileId}:${kind}`);
  const [ready, setReady] = useState(false), [error, setError] = useState(""), [runtime, setRuntime] = useState(null), [busy, setBusy] = useState(false);
  useEffect(() => {
    let closed = false, polling = false;
    setReady(false);setRuntime(null);
    const read = async () => {
      if (!online || polling) return;
      polling = true;
      try { assertBridgeProfileOwner(bridge, profileId); const result = await bridge.invokeProfileScoped("feature018_automation_status"); if (!closed) setRuntime(result.services?.[kind] ?? null); }
      catch (e) { if (!closed) setError(e.message); }
      finally { polling = false; }
    };
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    void read();
    const timer = setInterval(read, 5000);
    return () => { closed = true; clearInterval(timer); };
  }, [config.store, online, profileId, kind]);
  const disabled = !ready || !online || busy || config.saving;
  const toggle = async () => {
    const enabled = !config.draft.enabled;
    setBusy(true);setError("");
    try {
      if (!enabled) { assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_stop", { kind }); }
      config.store.edit(draft => ({ ...draft, enabled }), false);
      await config.store.flush();
    } catch (e) { setError(e.message === "CONFIG_DRAFT_INVALID" ? t("configSave.failed") : e.message); }
    finally { setBusy(false); }
  };
  const run = async () => {
    setBusy(true);setError("");
    try { await config.store.flush();assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_run", { kind }); }
    catch (e) { setError(e.message); }
    finally { setBusy(false); }
  };
  const state = !online ? t("status.disconnected") : runtime?.state === "nothing_available" ? t("automation.nothingAvailable") : runtime?.state === "success" ? t("common.success") : runtime?.state === "disabled" || !config.draft.enabled && !runtime ? t("common.disabled") : runtime?.state === "unconfirmed" ? t("automation.resultUnconfirmed") : t("common.waiting");
  return <article className="automation-card" data-native-automation={kind} data-automation-state={runtime?.state ?? "unobserved"}>
    <div className="automation-card-header"><div className="automation-card-title-group"><h3>{english(title)}</h3><p>{english(description)}</p></div>
      <button type="button" role="switch" aria-label={english(title)} aria-checked={config.draft.enabled} disabled={disabled || kind === "officialPosition" && !config.draft.enabled && !Number(config.draft.positionId)} onClick={toggle}><Switch checked={config.draft.enabled} /></button></div>
    <p role="status">{state}</p>
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {kind === "allianceGift" ? <div className="automation-status-grid">{["normalClaimed", "advancedClaimed"].filter(key => runtime?.[key] != null).map(key => <div className="automation-status-row" key={key}><span>{t(`automation.${key}`)}</span><strong>{runtime[key]}</strong></div>)}</div> : null}
    {kind === "allianceDonate" ? <div className="automation-status-grid">{["remaining", "donated", "scienceId"].filter(key => runtime?.[key] != null).map(key => <div className="automation-status-row" key={key}><span>{t(key === "scienceId" ? "automation.currentTech" : `automation.${key}`)}</span><strong>{runtime[key]}</strong></div>)}</div> : null}
    {runtime?.error ? <p role="alert" className="error-text">{runtime.error}</p> : null}
    {runtime?.before?.reason ? <p className="muted">{runtime.before.reason}</p> : null}
    {error ? <p role="alert" className="error-text">{error}<button type="button" onClick={() => { void config.store.refresh().then(() => { setReady(true);setError(""); }).catch(e => setError(e.message)); }}>{t("common.retry")}</button></p> : null}
    <PreviewConfigError config={config} t={t} />
    {kind === "allianceGift" ? <div className="automation-actions"><label>{t("automation.intervalMinutes")}<input type="number" min="1" max="1440" step="1" value={config.draft.intervalMinutes ?? 120} disabled={disabled} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, intervalMinutes: event.target.value }))} onBlur={() => { void config.store.flush().catch(e => setError(e.message)); }} /></label></div> : null}
    {kind === "allianceDonate" ? <div className="automation-actions"><label>{t("automation.donateThreshold")}<input type="number" min="1" max="30" step="1" value={config.draft.threshold ?? 15} disabled={disabled} aria-invalid={!validAutomationDraft(kind, config.draft)} onChange={event => config.store.edit(draft => ({ ...draft, threshold: event.target.value }))} onBlur={() => { void config.store.flush().catch(e => setError(e.message)); }} /></label></div> : null}
    {kind === "officialPosition" ? <div className="automation-actions"><label>{t("automation.targetPosition")}<select value={config.draft.positionId ?? 0} disabled={disabled} onChange={event => {
      config.store.edit(draft => ({ ...draft, positionId: Number(event.target.value) }), false);void config.store.flush().catch(e => setError(e.message));
    }}>{[[0, "none"], [10002, "vicePresident"], [10003, "strategyMinister"], [10004, "defenseMinister"], [10005, "constructionMinister"], [10006, "scienceMinister"], [10007, "internalAffairsMinister"]].map(([id, name]) => <option key={id} value={id}>{t(`automation.position.${name}`)}</option>)}</select></label></div> : null}
    {["allianceHelp", "allianceGift", "strongholdResource", "allianceCenterResource", "allianceGather", "dispatch"].includes(kind) ? <div className="automation-config-actions"><button type="button" className="automation-run-action" disabled={disabled} onClick={run}>{t(busy ? "automation.runningEllipsis" : "common.runNow")}</button></div> : null}
  </article>;
}
