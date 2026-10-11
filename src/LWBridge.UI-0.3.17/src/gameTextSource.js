// The native game_root_status contract is NativeGameRootStatus.Root (serialized
// as `root`). GameRootStatus.Path is a separate launch-admission contract.
export function selectedNativeGameTextRoot(gameRootStatus) {
  return gameRootStatus?.valid === true && typeof gameRootStatus.root === "string"
    ? gameRootStatus.root.trim()
    : "";
}
