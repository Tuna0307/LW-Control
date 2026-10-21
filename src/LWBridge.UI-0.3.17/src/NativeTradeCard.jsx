import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { Switch } from "./sharedPageUI.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { createAutomationAdapter, validAutomationDraft } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { NativeTradeFields } from "./NativeTradeFields.jsx";

// R30 native Trade Station. The goods/currency catalog is a read-only native
// season-template observation; it is only taken while purchasing is off.
const KIND = "tradeStation";

export function NativeTradeCard({ profileId, online, bridge = backendBridge }) {
  const { t } = useI18n();
  const config = usePreviewConfigAdapter({ enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] },
    createAutomationAdapter(bridge, profileId, KIND, online, draft => validAutomationDraft(KIND, draft)), `automation-native:${profileId}:${KIND}`);
  const [ready, setReady] = useState(false), [error, setError] = useState(""), [runtime, setRuntime] = useState(null), [observation, setObservation] = useState(null), [busy, setBusy] = useState(false);
  useEffect(() => {
    let closed = false, polling = false;
    setReady(false);setRuntime(null);
    const read = async () => {
      if (!online || polling) return;
      polling = true;
      try { assertBridgeProfileOwner(bridge, profileId);const result = await bridge.invokeProfileScoped("feature018_automation_status");if (!closed) setRuntime(result.services?.[KIND] ?? null); }
      catch (e) { if (!closed) setError(e.message); }
      finally { polling = false; }
    };
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    void read();
    const timer = setInterval(read, 5000);
    return () => { closed = true;clearInterval(timer); };
  }, [config.store, online, profileId]);
  useEffect(() => {
    // Read the season catalog once per connection while purchasing is off.
    if (!online || !ready || config.draft.enabled) return undefined;
    let closed = false;
    const observe = async () => {
      for (let attempt = 0; attempt < 40 && !closed; attempt += 1) {
        try {
          assertBridgeProfileOwner(bridge, profileId);
          const result = await bridge.invokeProfileScoped("feature018_automation_observe", { kind: KIND });
          if (closed) return;
          if (result?.catalog || result?.seasonal) setObservation(result);
          if (result?.readOnly && !result?.waiting) return;
        } catch (e) { if (!closed) setError(e.message);return; }
        await new Promise(resolve => setTimeout(resolve, 500));
      }
    };
    void observe();
    return () => { closed = true; };
  }, [online, ready, profileId]);
  const draft = config.draft;
  const disabled = !ready || !online || busy || config.saving;
  const toggle = async () => {
    const enabled = !draft.enabled;
    if (enabled && !(draft.selectedItemIds ?? []).length) { setError(t("automation.tradeStation.goodsToBuy"));return; }
    setBusy(true);setError("");
    try {
      if (!enabled) { assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_stop", { kind: KIND }); }
      config.store.edit(current => ({ ...current, enabled }), false);
      await config.store.flush();
    } catch (e) { setError(e.message === "CONFIG_DRAFT_INVALID" ? t("configSave.failed") : e.message); }
    finally { setBusy(false); }
  };
  const state = !online ? t("status.disconnected") : runtime?.state === "nothing_available" ? t("automation.nothingAvailable") : runtime?.state === "success" ? t("common.success") : !draft.enabled ? t("common.disabled") : runtime?.state === "unconfirmed" ? t("automation.resultUnconfirmed") : t("common.waiting");
  return <article className={`automation-card trade-station-card${draft.enabled ? " is-enabled" : ""}`} data-native-automation={KIND} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <div className="automation-card-header">
      <div className="automation-card-title-group"><h3>{t("automation.tradeStation.title")}</h3><p>{t("automation.tradeStation.description")}</p></div>
      <button type="button" role="switch" className="automation-header-switch" aria-label={t("automation.tradeStation.title")} aria-checked={draft.enabled === true} disabled={disabled} onClick={() => { void toggle(); }}><Switch checked={draft.enabled === true} /></button>
    </div>
    <p role="status">{state}</p>
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {runtime?.error ? <p role="alert" className="error-text">{runtime.error}</p> : null}
    {runtime?.before?.reason ? <p className="muted">{runtime.before.reason}</p> : null}
    {error ? <p role="alert" className="error-text">{error}</p> : null}
    <PreviewConfigError config={config} t={t} />
    <NativeTradeFields config={config} runtime={runtime} observation={observation} disabled={disabled || draft.enabled} t={t} />
  </article>;
}
