import { useEffect, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { usePreviewConfigAdapter, PreviewConfigError } from "./previewConfigHook.jsx";
import { automationDraftError } from "./previewAutomationContracts.js";
import { AutomationConfigPreview } from "./AutomationPage.jsx";
import { CHAT_KINDS, settleChatDelayPairs, chatDraft, createChatAdapter } from "./chatAutomationRuntime.js";
import { subscribeChatStatus } from "./chatStatusHub.js";
import { NativeCardHeader, NativeCardSettings, NativeCardStatus, useRememberedExpansion } from "./NativeCardShell.jsx";
import { GameEmojiPicker } from "./GameEmojiPicker.jsx";
import { ToggleRow } from "./sharedPageUI.jsx";

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
    let cancelled = false;
    setRuntime(null); setError("");
    setHydrated(false); setConfigError("");
    void config.store.refresh().then(() => {
      if (!cancelled) setHydrated(true);
    }).catch(e => { if (!cancelled) setConfigError(e.message); });
    let unsubscribe = () => {};
    if (online) {
      try {
        unsubscribe = subscribeChatStatus(bridge, profileId,
          status => { if (!cancelled) { setRuntime(status); setError(""); } },
          e => { if (!cancelled) setError(e.message); });
      } catch (e) { setError(e.message); }
    }
    return () => { cancelled = true; unsubscribe(); };
  }, [bridge, profileId, online, kind, config.store]);
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
  const updateGold = (key, value) => {
    config.store.edit(draft => ({ ...draft, [key]: value }), false);
    void config.store.flush().catch(e => setError(e.message));
  };
  return <article className={`automation-card${config.draft.enabled ? " is-enabled" : ""}`} data-native-chat={kind}>
    <NativeCardHeader title={english(title)} description={kind === "fireworks" ? t("chatRewards.fireworksHelp") : english(description)} checked={config.draft.enabled} disabled={disabled || claimsUnavailable} onToggle={onToggle} />
    <NativeCardStatus t={t} online={online} enabled={config.draft.enabled === true} state={state}
      rows={[[t("chatRewards.newRewards"), row?.claimed ?? null],
        ...[["alreadyClaimed", "alreadyClaimed"], ["exhausted", "exhausted"], ["expired", "expired"],
          ["refused", "refused"], ["unconfirmed", "unconfirmed"]].map(([field, key]) =>
          [t(`chatRewards.${key}`), row?.[field] > 0 ? row[field] : null]),
        ...(kind === "goldEgg" && row?.crossServer?.enabled ?
          [[t("chatRewards.remoteRooms"), row.crossServer.eligibleRooms ?? 0]] : [])]}
      note={[row?.offHours ? t("automation.offHours", { time: row.activeFrom ?? "" }) : "", runtime?.listener ? t("automation.chatObserved", { count: runtime.listener.observedMessages ?? 0 }) : "", row?.warning ?? ""].filter(Boolean).join(" · ")}
      errors={[error, retry, validation ? t(validation) : null]} />
    <PreviewConfigError config={config} t={t} />
    <NativeCardSettings t={t} collapsible expanded={expanded} onToggle={toggleExpanded} disabled={disabled} onBlur={() => config.store.flush().catch(e => setError(e.message))}>
      {kind === "goldEgg" ? <section className="automation-settings-section">
        {config.draft.legacyCombined ? <><p className="hint">{t("chatRewards.legacy")}</p><button type="button" data-chat-separate-gold disabled={disabled} onClick={() => updateGold("legacyCombined", false)}>{t("chatRewards.separate")}</button></> : null}
        <ToggleRow label={t("chatRewards.crossServer")} checked={config.draft.crossServerEnabled} disabled={disabled || config.draft.legacyCombined} onChange={value => updateGold("crossServerEnabled", value)} />
        <p className="hint">{t("chatRewards.crossHint")}</p>
        <ToggleRow label={t("chatRewards.ignoreBirthday")} checked={config.draft.ignoreBirthday} disabled={disabled || config.draft.legacyCombined} onChange={value => updateGold("ignoreBirthday", value)} />
        <p className="hint">{t("chatRewards.eventUnavailable")}</p>
      </section> : null}
      <AutomationConfigPreview title={title} enabled={!disabled && !(kind === "goldEgg" && config.draft.legacyCombined)} previewState="" config={config} assistConfig={config} availableSquads={availableSquads} onLocalError={setError} />
      {config.draft.replyEnabled && !String(config.draft.replies ?? "").split(/\r?\n/).some(line => line.trim()) ? <p className="automation-card-note">{t("automation.replyRequired")}</p> : null}
      {config.draft.replyEnabled && online ? <GameEmojiPicker bridge={bridge} profileId={profileId} disabled={disabled} onPick={tag => {
        config.store.edit(draft => ({ ...draft, replies: `${String(draft.replies ?? "").replace(/\s+$/, "")}${String(draft.replies ?? "").trim() ? " " : ""}${tag}` }), false);
        void config.store.flush().catch(e => setError(e.message));
      }} /> : null}
    </NativeCardSettings>
  </article>;
}
