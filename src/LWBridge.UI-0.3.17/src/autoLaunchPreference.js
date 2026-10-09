export const AUTO_LAUNCH_GAME_STORAGE_KEY = "lwbridge.autoLaunchGame";

export function readAutoLaunchGamePreference(storage = window.localStorage) {
  return storage.getItem(AUTO_LAUNCH_GAME_STORAGE_KEY) !== "false";
}

export function writeAutoLaunchGamePreference(storage = window.localStorage, value) {
  storage.setItem(AUTO_LAUNCH_GAME_STORAGE_KEY, String(value));
}
