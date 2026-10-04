import { createConfigDraft } from "./previewConfig.js";

function profileRequiredError() {
  const error = new Error("Select a profile before changing profile-scoped automation settings.");
  error.code = "PROFILE_REQUIRED";
  return error;
}

function profileScopeMismatchError() {
  const error = new Error("The command targets a different local profile.");
  error.code = "PROFILE_SCOPE_MISMATCH";
  return error;
}

export function assertBridgeProfileOwner(bridge, profileId) {
  if (!profileId || !bridge?.profileId) throw profileRequiredError();
  if (bridge.profileId !== profileId) throw profileScopeMismatchError();
}

export function createAutomationFlagAdapter(bridge, profileId, name, statusKey, defaultValue = false) {
  return {
    read: async () => {
      assertBridgeProfileOwner(bridge, profileId);
      const status = await bridge.invokeProfileScoped("get_status", {});
      const value = status?.config?.[statusKey];
      return typeof value === "boolean" ? value : defaultValue;
    },
    write: async (enabled) => {
      assertBridgeProfileOwner(bridge, profileId);
      const result = await bridge.invokeProfileScoped("set_automation", { name, enabled });
      if (typeof result?.enabled !== "boolean") throw new Error("set_automation returned an invalid enabled value.");
      return result.enabled;
    },
  };
}

export function createProfileConfigDraftRegistry() {
  const stores = new Map();

  return {
    get(profileId, key, initial, adapter) {
      const id = JSON.stringify([profileId, key]);
      const existing = stores.get(id);
      if (existing) {
        existing.setAdapter(adapter);
        return existing;
      }
      const store = createConfigDraft(initial, adapter);
      stores.set(id, store);
      return store;
    },
    find(profileId, key) {
      return stores.get(JSON.stringify([profileId, key]));
    },
    dispose() {
      for (const store of stores.values()) store.dispose();
      stores.clear();
    },
  };
}
