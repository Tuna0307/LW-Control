import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { Switch } from "./sharedPageUI.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { AUTOMATION_TASKS, createAutomationAdapter } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

export function NativeAutomationCard({ title, description, profileId, online, bridge = backendBridge }) {
  const { english, t } = useI18n();
  const kind = AUTOMATION_TASKS[title];
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, kind, online, () => true), `automation-native:${profileId}:${kind}`);
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
  const run = async () => {
    setBusy(true);setError("");
    try { await config.store.flush();assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_run", { kind }); }
    catch (e) { setError(e.message); }
    finally { setBusy(false); }
  };
  const state = !online ? t("status.disconnected") : runtime?.state === "nothing_available" ? t("automation.nothingAvailable") : runtime?.state === "success" ? t("common.success") : runtime?.state === "disabled" || !config.draft.enabled && !runtime ? t("common.disabled") : runtime?.state === "unconfirmed" ? t("automation.resultUnconfirmed") : t("common.waiting");
  return <article className="automation-card" data-native-automation={kind} data-automation-state={runtime?.state ?? "unobserved"}>
    <div className="automation-card-header"><div className="automation-card-title-group"><h3>{english(title)}</h3><p>{english(description)}</p></div>
      <button type="button" role="switch" aria-label={english(title)} aria-checked={config.draft.enabled} disabled={disabled} onClick={() => {
        config.store.edit(draft => ({ ...draft, enabled: !draft.enabled }), false);void config.store.flush().catch(e => setError(e.message));
      }}><Switch checked={config.draft.enabled} /></button></div>
    <p role="status">{state}</p>
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {runtime?.error ? <p role="alert" className="error-text">{runtime.error}</p> : null}
    {error ? <p role="alert" className="error-text">{error}<button type="button" onClick={() => { void config.store.refresh().then(() => { setReady(true);setError(""); }).catch(e => setError(e.message)); }}>{t("common.retry")}</button></p> : null}
    <PreviewConfigError config={config} t={t} />
    <div className="automation-config-actions"><button type="button" className="automation-run-action" disabled={disabled} onClick={run}>{t(busy ? "automation.runningEllipsis" : "common.runNow")}</button></div>
  </article>;
}
