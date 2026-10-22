import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { NativeCardHeader, NativeCardSettings, NativeCardStatus } from "./NativeCardShell.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { createAutomationAdapter, validAutomationDraft } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { subscribeAutomationStatus } from "./automationStatusHub.js";
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
    let closed = false;
    setReady(false);setRuntime(null);
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    const unsubscribe = online ? subscribeAutomationStatus(bridge, profileId, services => { if (!closed) setRuntime(services?.[KIND] ?? null); }, e => { if (!closed) setError(e.message); }) : () => {};
    return () => { closed = true;unsubscribe(); };
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
  return <article className={`automation-card trade-station-card${draft.enabled ? " is-enabled" : ""}`} data-native-automation={KIND} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <NativeCardHeader title={t("automation.tradeStation.title")} description={t("automation.tradeStation.description")} checked={draft.enabled === true} disabled={disabled} onToggle={() => { void toggle(); }} />
    <NativeCardStatus t={t} online={online} enabled={draft.enabled === true} state={runtime?.state}
      rows={[[t("automation.confirmedCount"), runtime?.confirmed > 0 ? runtime.confirmed : null]]}
      note={runtime?.before?.reason && runtime?.state !== "disabled" ? runtime.before.reason : null} errors={[runtime?.error, error]} />
    <PreviewConfigError config={config} t={t} />
    <NativeCardSettings t={t}>
      <NativeTradeFields config={config} runtime={runtime} observation={observation} disabled={disabled || draft.enabled} t={t} />
    </NativeCardSettings>
  </article>;
}
