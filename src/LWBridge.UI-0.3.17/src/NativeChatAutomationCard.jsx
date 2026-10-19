import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { usePreviewConfigAdapter, PreviewConfigError } from "./previewConfigHook.jsx";
import { automationDraftError } from "./previewAutomationContracts.js";
import { AutomationConfigPreview } from "./AutomationPage.jsx";
import { CHAT_KINDS, chatDraft, createChatAdapter } from "./chatAutomationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { Switch } from "./sharedPageUI.jsx";

export function NativeChatAutomationCard({ title, description, online, profileId, availableSquads, bridge = backendBridge }) {
  const { english, t } = useI18n();
  const kind = CHAT_KINDS[title];
  const config = usePreviewConfigAdapter(chatDraft,
    createChatAdapter(bridge, profileId, kind, draft => !automationDraftError(title, draft), online),
    `native-chat:${profileId}:${kind}`);
  const [runtime, setRuntime] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const [configError, setConfigError] = useState("");
  useEffect(() => {
    let cancelled = false, polling = false;
    setHydrated(false); setConfigError("");
    void config.store.refresh().then(() => {
      if (!cancelled) setHydrated(true);
    }).catch(e => { if (!cancelled) setConfigError(e.message); });
    const poll = async () => {
      if (!online || polling) return;
      polling = true;
      try {
        assertBridgeProfileOwner(bridge, profileId);
        const status = await bridge.invokeProfileScoped("feature018_chat_status");
        if (!cancelled) { setRuntime(status); setError(""); }
      } catch (e) { if (!cancelled) setError(e.message); }
      finally { polling = false; }
    };
    void poll();
    const timer = setInterval(poll, 2000);
    return () => { cancelled = true; clearInterval(timer); };
  }, [profileId, online, kind, config.store]);
  const run = async () => {
    setBusy(true); setError("");
    try {
      await config.store.flush();
      assertBridgeProfileOwner(bridge, profileId);
      await bridge.invokeProfileScoped("feature018_chat_run", { kind });
      setRuntime(await bridge.invokeProfileScoped("feature018_chat_status"));
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  };
  const row = runtime?.services?.[kind];
  const validation = automationDraftError(title, config.draft);
  const disabled = !online || !hydrated || busy || config.saving;
  const claimsUnavailable = row?.capabilities?.claims === false;
  return <article className="automation-card" data-native-chat={kind}>
    <div className="automation-card-header"><div><h3>{english(title)}</h3><p>{english(description)}</p></div>
      <button type="button" role="switch" aria-label={english(title)} aria-checked={config.draft.enabled}
        disabled={disabled || claimsUnavailable} onClick={() => {
          config.store.edit(draft => ({ ...draft, enabled: !draft.enabled }), false);
          void config.store.flush().catch(e => setError(e.message));
        }}><Switch checked={config.draft.enabled} /></button></div>
    <div role="status" className="automation-card-meta-row">{!online ? t("status.disconnected") : row?.state ?? (config.draft.enabled ? t("common.waiting") : t("common.disabled"))}</div>
    {runtime?.listener ? <p className="muted">{t("automation.chatObserved", { count: runtime.listener.observedMessages ?? 0 })}</p> : null}
    {row?.warning ? <p className="muted">{row.warning}</p> : null}
    {error ? <p role="alert" className="error-text">{error}</p> : null}
    {configError ? <p role="alert" className="error-text">{configError}<button type="button" onClick={() => {
      void config.store.refresh().then(() => { setHydrated(true); setConfigError(""); }).catch(e => setConfigError(e.message));
    }}>{t("common.retry")}</button></p> : null}
    <PreviewConfigError config={config} t={t} />
    {validation ? <p role="alert">{t(validation)}</p> : null}
    <div className="automation-config-actions"><button type="button" className="automation-run-action" disabled={disabled || claimsUnavailable || !!validation} onClick={run}>{t(busy ? "automation.runningEllipsis" : "common.runNow")}</button></div>
    <fieldset className="automation-config-body" disabled={disabled} onBlur={() => config.store.flush().catch(e => setError(e.message))}>
      <AutomationConfigPreview title={title} enabled={!disabled} previewState="" config={config} assistConfig={config} availableSquads={availableSquads} onLocalError={setError} />
    </fieldset>
  </article>;
}
