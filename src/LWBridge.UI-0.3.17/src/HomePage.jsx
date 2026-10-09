import { useI18n } from "./i18n.jsx";
import { ToggleRow } from "./sharedPageUI.jsx";

const RECOVERY_ACTIVE_STATES = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);

function previewHomeState(name) {
  const base = {
    rootResolved: true,
    gameRootStatus: { valid: true, root: "C:\\Games\\Last War-Survival Game" },
    proxyStatus: { gameRunning: false, repairRequired: false },
    online: false,
    gameRecoveryStatus: { state: "idle" },
    autoLaunchGame: false,
    autoReconnect: false,
    busy: "",
    proxyBusy: false,
    gameLaunchBusy: false,
    gameRootError: "",
    gameActionError: "",
    production: false,
  };
  switch (name) {
    case "home-checking": return { ...base, rootResolved: false, gameRootStatus: null, proxyStatus: null };
    case "home-missing": return { ...base, gameRootStatus: { valid: false, root: "" } };
    case "home-launching": return { ...base, gameLaunchBusy: true };
    case "home-proxy-busy-stopped": return { ...base, proxyBusy: true };
    case "home-proxy-busy-running": return { ...base, proxyBusy: true, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-proxy-busy-repair": return { ...base, proxyBusy: true, proxyStatus: { gameRunning: true, repairRequired: true } };
    case "home-busy-overlap": return { ...base, proxyBusy: true, gameLaunchBusy: true, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-root-busy-missing": return { ...base, busy: "gameRoot", gameRootStatus: { valid: false, root: "" } };
    case "home-root-busy-valid": return { ...base, busy: "gameRoot" };
    case "home-running-disconnected": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-connected": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, online: true, autoLaunchGame: true, autoReconnect: true };
    case "home-repair": return { ...base, proxyStatus: { gameRunning: true, repairRequired: true } };
    case "home-recovery-waiting": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "waiting" } };
    case "home-recovery-updating": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "updating" } };
    case "home-recovery-repairing": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "repairing" } };
    case "home-recovery-launching": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "launching" } };
    case "home-recovery-verifying": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "verifying" } };
    case "home-recovery-maintenance": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "maintenance" } };
    case "home-recovery-failed": return { ...base, gameRecoveryStatus: { state: "failed", error: "GAME_RECOVERY_FAILED" } };
    case "home-error-unknown": return { ...base, gameActionError: "HOME_ERROR001_UNKNOWN_QA" };
    case "home-error-embedded": return { ...base, gameActionError: "QA fixture: GAME_XLUA_ABI_UNSUPPORTED while preparing startup" };
    case "home-recovery-error-unknown": return { ...base, gameRecoveryStatus: { state: "failed", error: "HOME_ERROR001_UNKNOWN_QA" } };
    case "home-errors-both": return { ...base, gameRootStatus: { valid: false, root: "" }, gameRootError: "INVALID_GAME_ROOT", gameActionError: "GAME_XLUA_ABI_UNSUPPORTED" };
    case "home-error-root": return { ...base, gameRootStatus: { valid: false, root: "" }, gameRootError: "INVALID_GAME_ROOT" };
    case "home-error-action-missing-root": return { ...base, gameRootStatus: { valid: false, root: "" }, gameActionError: "GAME_XLUA_ABI_UNSUPPORTED" };
    default: return null;
  }
}

function translatedError(t, value) {
  const codes = [];
  if (value && typeof value === "object" && "code" in value && typeof value.code === "string") codes.push(value.code);
  const message = value instanceof Error ? value.message : String(value ?? "");
  codes.push(...message.match(/\b[A-Z][A-Z0-9_]{2,}\b/g) || []);
  for (const code of [...new Set(codes)].reverse()) {
    for (const namespace of ["error", "auth.error", "update.error"]) {
      const key = `${namespace}.${code}`;
      const translated = t(key);
      if (translated !== key) return translated;
    }
  }
  return t("common.actionFailed");
}

export function HomePage({
  previewState = "",
  homeState,
  onAutoLaunchGameChange,
  onAutoReconnectChange,
  onGameRootSelect,
  onStartGame,
  onStopGame,
  onUpdateAndRestart,
}) {
  const { t } = useI18n();
  const preview = previewHomeState(previewState);
  const state = preview || homeState || {};
  const rootResolved = state.rootResolved === true;
  const rootValid = state.gameRootStatus?.valid === true;
  const gameRunning = state.proxyStatus?.gameRunning === true;
  const repairRequired = state.proxyStatus?.repairRequired === true;
  const recoveryState = state.gameRecoveryStatus?.state || "idle";
  const recovering = RECOVERY_ACTIVE_STATES.has(recoveryState);
  const launching = state.gameLaunchBusy === true;
  const proxyBusy = state.proxyBusy === true;
  const rootBusy = state.busy === "gameRoot";
  const showRootPicker = rootResolved && !rootValid;
  const repairOnClose = rootResolved && rootValid && gameRunning && repairRequired && !recovering;
  const lifecycleProviderAvailable = state.production === true
    && typeof onStartGame === "function"
    && typeof onStopGame === "function"
    && typeof onUpdateAndRestart === "function";
  const canStart = lifecycleProviderAvailable && rootResolved && rootValid && !gameRunning && !recovering && !proxyBusy && !launching;
  const canStop = lifecycleProviderAvailable && rootResolved && rootValid && (gameRunning || recovering) && !proxyBusy && !launching;

  let status = t("setup.checking");
  if (rootResolved) {
    if (showRootPicker) status = t("setup.gameRootMissing");
    else if (launching) status = t("setup.launchingGame");
    else if (proxyBusy) status = t("common.processing");
    else if (recovering) status = t(`recovery.state.${recoveryState}`);
    else if (repairRequired) status = t("setup.repairRequired");
    else if (gameRunning) status = t(state.online ? "status.gameRunning" : "setup.bridgeDisconnected");
    else status = t("setup.gameStopped");
  }

  return (
    <section className="panel quick-actions-panel" data-preview-fixture={preview ? previewState : undefined}>
      <div className="panel-title">
        <h2>{t("setup.title")}</h2>
        <span className={gameRunning && state.online ? "status-ok" : "muted"}>{status}</span>
      </div>
      {rootResolved ? showRootPicker ? (
        <div className="game-root-missing">
          <span>{state.gameRootError ? translatedError(t, state.gameRootError) : t("setup.gameRootMissing")}</span>
          <button className="primary" type="button" disabled={rootBusy || !state.production} onClick={onGameRootSelect}>
            {t(rootBusy ? "common.processing" : "setup.gameRootSelect")}
          </button>
        </div>
      ) : (
        <div className="game-controls">
          {!repairOnClose ? (
            <button className="primary" type="button" disabled={!canStart} onClick={onStartGame}>{t(launching ? "setup.launchingGame" : proxyBusy ? "common.processing" : "top.launchGame")}</button>
          ) : null}
          <button type="button" disabled={!canStop} onClick={repairOnClose ? onUpdateAndRestart : onStopGame}>
            {t(proxyBusy && gameRunning ? "common.processing" : repairOnClose ? "setup.updateAndLaunch" : "setup.closeGameAction")}
          </button>
          {repairOnClose ? <span className="muted">{t("setup.updateCloseGame")}</span> : null}
        </div>
      ) : null}
      {state.gameActionError ? <span className="game-root-error">{translatedError(t, state.gameActionError)}</span> : null}
      <ToggleRow
        label={t("auth.autoLaunchGame")}
        checked={state.autoLaunchGame === true}
        disabled={state.autoLaunchGame == null || !state.production}
        onChange={onAutoLaunchGameChange}
      />
      <ToggleRow
        label={t("automation.autoReconnect.title")}
        checked={state.autoReconnect === true}
        disabled={state.autoReconnect == null || !state.production}
        onChange={onAutoReconnectChange}
      />
      {recoveryState === "failed" && state.gameRecoveryStatus?.error ? (
        <span className="game-root-error">{t("recovery.failedDetail", { error: translatedError(t, state.gameRecoveryStatus.error) })}</span>
      ) : null}
    </section>
  );
}
