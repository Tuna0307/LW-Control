import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { usePreviewConfigAdapter, PreviewConfigError } from "./previewConfigHook.jsx";
import { automationDraftError } from "./previewAutomationContracts.js";
import { AutomationConfigPreview } from "./AutomationPage.jsx";
import { CHAT_KINDS, settleChatDelayPairs, chatDraft, createChatAdapter } from "./chatAutomationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { NativeCardHeader, NativeCardSettings, NativeCardStatus, useRememberedExpansion } from "./NativeCardShell.jsx";
import { GameEmojiPicker } from "./GameEmojiPicker.jsx";

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
  const [expanded, toggleExpanded] = useRememberedExpansion(`automation-settings:${profileId}:${kind}`);
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
  const row = runtime?.services?.[kind];
  const validation = automationDraftError(title, config.draft);
  const disabled = !online || !hydrated || busy || config.saving;
  const claimsUnavailable = row?.capabilities?.claims === false;
  const onToggle = () => {
    const turningOn = !config.draft.enabled;
    config.store.edit(draft => settleChatDelayPairs({ ...draft, enabled: !draft.enabled }), false);
    void config.store.flush().catch(e => {
      // A switch that did not save must not look on.
      if (turningOn) config.store.edit(draft => ({ ...draft, enabled: false }), false);
      setError(e.message === "CONFIG_DRAFT_INVALID" ? t(automationDraftError(title, config.store.getSnapshot().draft) || "configSave.failed") : e.message);
    });
  };
  const retry = configError ? <>{configError}<button type="button" className="automation-error-retry" onClick={() => {
    void config.store.refresh().then(() => { setHydrated(true); setConfigError(""); }).catch(e => setConfigError(e.message));
  }}>{t("common.retry")}</button></> : null;
  const state = row?.state === "waiting_ack" ? "waiting" : row?.state;
  return <article className={`automation-card${config.draft.enabled ? " is-enabled" : ""}`} data-native-chat={kind}>
    <NativeCardHeader title={english(title)} description={english(description)} checked={config.draft.enabled} disabled={disabled || claimsUnavailable} onToggle={onToggle} />
    <NativeCardStatus t={t} online={online} enabled={config.draft.enabled === true} state={state}
      rows={[[t("automation.claimed"), row?.claimed > 0 ? row.claimed : null]]}
      note={[row?.offHours ? t("automation.offHours", { time: row.activeFrom ?? "" }) : "", runtime?.listener ? t("automation.chatObserved", { count: runtime.listener.observedMessages ?? 0 }) : "", row?.warning ?? ""].filter(Boolean).join(" · ")}
      errors={[error, retry, validation ? t(validation) : null]} />
    <PreviewConfigError config={config} t={t} />
    <NativeCardSettings t={t} collapsible expanded={expanded} onToggle={toggleExpanded} disabled={disabled} onBlur={() => config.store.flush().catch(e => setError(e.message))}>
      <AutomationConfigPreview title={title} enabled={!disabled} previewState="" config={config} assistConfig={config} availableSquads={availableSquads} onLocalError={setError} />
      {config.draft.replyEnabled && !String(config.draft.replies ?? "").split(/\r?\n/).some(line => line.trim()) ? <p className="automation-card-note">{t("automation.replyRequired")}</p> : null}
      {config.draft.replyEnabled && online ? <GameEmojiPicker bridge={bridge} profileId={profileId} disabled={disabled} onPick={tag => {
        config.store.edit(draft => ({ ...draft, replies: `${String(draft.replies ?? "").replace(/\s+$/, "")}${String(draft.replies ?? "").trim() ? " " : ""}${tag}` }), false);
        void config.store.flush().catch(e => setError(e.message));
      }} /> : null}
    </NativeCardSettings>
  </article>;
}
