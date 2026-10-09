import { emptyUpdateStatus, previewUpdateStatus } from "./previewRemainingPagesContracts.js";
import { createConfigDraft } from "./previewConfig.js";

// Original ui/di, index-BVfnK1wp.js: only the literal stored false disables focus.
export function initialProfileFocus(storage) {
  return storage.getItem("lwbridge.focusGameOnProfileSelect") !== "false";
}
export function saveProfileFocus(storage, value) {
  storage.setItem("lwbridge.focusGameOnProfileSelect", String(value));
}

export function initialShellUpdateStatus(mode, previewState) {
  return mode === "preview" && previewState.startsWith("settings-update-")
    ? previewUpdateStatus(previewState) : emptyUpdateStatus();
}

// Own local runtime metadata, never original commercial entitlement. Native
// bootstrap currently contains one profile. Positive multi-profile UI examples
// are explicit browser-only fixtures; they never mark a game/transport online.
export function initialShellProfiles(mode, previewState, bootstrap = {}) {
  if (mode === "preview" && previewState.startsWith("shell-profiles")) {
    return {
      selectedProfileId: "preview-local-1", maxProfiles: 3,
      profiles: [
        { id: "preview-local-1", displayName: "Local 1", serverId: 321, note: "", enabled: true },
        { id: "preview-local-2", displayName: "Local 2", serverId: 322, note: "", enabled: true },
      ],
    };
  }
  return { selectedProfileId: bootstrap.profiles?.selectedProfileId || "",
    profiles: bootstrap.profiles?.profiles || [],
    maxProfiles: bootstrap.localRuntime?.profileCapacity || 1 };
}

export function previewShellFlagStores(mode, previewState) {
  if (mode !== "preview" || previewState !== "shell-save-errors") return [];
  return Array.from({ length: 4 }, () => {
    let confirmed = { enabled: false };
    let rejectFirst = true;
    const store = createConfigDraft(confirmed, {
      read: async () => ({ ...confirmed }),
      write: async (draft) => {
        if (rejectFirst) { rejectFirst = false; throw new Error("PREVIEW_CONFIG_SAVE_FAILED"); }
        confirmed = { ...draft }; return confirmed;
      },
    });
    store.edit({ enabled: true }, false);
    store.flush().catch(() => {});
    return store;
  });
}
