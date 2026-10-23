import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

export const CHAT_KINDS = { "Red Packet": "redPacket", "Fireworks / Egg": "fireworks", Treasure: "treasure" };
export function chatDraft(value = {}) {
  return { enabled: value.enabled === true, claimMin: value.claimDelaySeconds?.[0] ?? 0,
    claimMax: value.claimDelaySeconds?.[1] ?? 0, replyEnabled: value.replyEnabled === true,
    replyMin: value.replyDelaySeconds?.[0] ?? 2, replyMax: value.replyDelaySeconds?.[1] ?? 5,
    replies: (value.replies ?? []).join("\n"), treasureSearchEnabled: value.searchEnabled === true,
    treasureDispatchEnabled: value.dispatchEnabled === true,
    dispatchMin: value.dispatchDelaySeconds?.[0] ?? 2, dispatchMax: value.dispatchDelaySeconds?.[1] ?? 5,
    dispatchRetry: value.dispatchRetrySeconds ?? 30, dispatchSquads: value.dispatchSquadPriority ?? [1],
    claimSchedule: Array.isArray(value.claimSchedule) ? value.claimSchedule.map(row => ({ ...row })) : null,
    activeHoursEnabled: value.activeHours?.enabled === true, activeStart: value.activeHours?.start ?? "08:00",
    activeEnd: value.activeHours?.end ?? "02:00" };
}
// Owner 2026-10-21: "Min 10, Max 0" means 10 seconds - raise Max to Min instead of refusing to save.
export function settleChatDelayPairs(draft) {
  const next = { ...draft };
  for (const [min, max] of [["claimMin", "claimMax"], ["replyMin", "replyMax"], ["dispatchMin", "dispatchMax"]]) {
    const low = Number(next[min]), high = Number(next[max]);
    if (String(next[min] ?? "").trim() !== "" && String(next[max] ?? "").trim() !== "" && Number.isFinite(low) && Number.isFinite(high) && high < low) next[max] = next[min];
  }
  return next;
}
export function chatConfig(draft) {
  return { enabled: draft.enabled, claimDelaySeconds: [Number(draft.claimMin), Number(draft.claimMax)],
    replyEnabled: draft.replyEnabled, replyDelaySeconds: [Number(draft.replyMin), Number(draft.replyMax)],
    replies: draft.replies.split(/\r?\n/).map(line => line.trim()).filter(Boolean),
    searchEnabled: draft.treasureSearchEnabled, dispatchEnabled: draft.treasureDispatchEnabled,
    dispatchDelaySeconds: [Number(draft.dispatchMin), Number(draft.dispatchMax)],
    dispatchRetrySeconds: Number(draft.dispatchRetry), dispatchSquadPriority: draft.dispatchSquads,
    activeHours: { enabled: draft.activeHoursEnabled === true, start: draft.activeStart ?? "08:00", end: draft.activeEnd ?? "02:00" },
    ...(Array.isArray(draft.claimSchedule) ? { claimSchedule: draft.claimSchedule.map(row => ({ start: row.start, end: row.end, min: Number(row.min), max: Number(row.max) })) } : {}) };
}
export function createChatAdapter(bridge, profileId, kind, valid, online) {
  const invoke = (command, payload = {}) => {
    assertBridgeProfileOwner(bridge, profileId);
    return bridge.invokeProfileScoped(command, payload);
  };
  return { valid,
    read: async () => chatDraft(await invoke("feature018_chat_config_get", { kind })),
    write: async draft => {
      const saved = await invoke("feature018_chat_config_save", { kind, config: chatConfig(draft) });
      if (online) await invoke("feature018_chat_configure", { kind });
      return chatDraft(saved);
    },
  };
}
