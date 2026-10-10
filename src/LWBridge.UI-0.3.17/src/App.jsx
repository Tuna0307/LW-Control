import { Activity, Fragment, Suspense, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, useTransition } from "react";
import { flushSync } from "react-dom";
import { toggleShellTheme } from "./shellTheme.js";
import { TopVersion, ShellConfigSaveErrors } from "./ShellPresentation.jsx";
import { initialShellUpdateStatus, initialShellProfiles, previewShellFlagStores, initialProfileFocus, saveProfileFocus } from "./shellState.js";
import { ProfileSidebar } from "./ProfileSidebar.jsx";
import { AppExitDialog, AppExitPrompt } from "./AppExitDialog.jsx";
import { ProfileSwitchState } from "./ProfileSwitchState.jsx";
import { GameAssetImageProvider } from "./GameAssetImage.jsx";
import offlineDot from "./assets/dot-offline.png";
import onlineDot from "./assets/dot-online.png";
import { backendBridge } from "./backendBridge.js";
import { LANGUAGES, useI18n } from "./i18n.jsx";
import { DEFAULT_SCAN_STATE, connectionState, createMapApi, unwrapProfileEvent } from "./mapBackend.js";
import {
  AUTO_SCAN_DEFAULT_TYPES,
  applyAutoScanConfigEdit,
  applyAutoScanConfigIntent,
  loadAutoScanConfig,
  normalizeAutoScanConfig,
  saveAutoScanConfig,
} from "./mapAutoConfig.js";
import { NavIcon } from "./NavIcon.jsx";
import { PageForRoute, preloadRoute } from "./Pages.jsx";
import { initialRouteKey, routes } from "./routes.js";
import { readAutoLaunchGamePreference, writeAutoLaunchGamePreference } from "./autoLaunchPreference.js";
import { createAutomationFlagAdapter, createProfileConfigDraftRegistry } from "./profileConfigDraft.js";
import { createAutoScanNativeCoordinator } from "./autoScanNativeCoordinator.js";

const THEME_KEY = "lwbridge.theme";
const LEGACY_SERVER_HISTORY_KEY = "lastwar.serverJumpHistory";
const AUTO_WEEKEND_SHIELD_FLAG_KEY = "flag:autoWeekendShield";
const AUTO_ATTACK_SHIELD_FLAG_KEY = "flag:autoAttackShield";
const AUTO_RECONNECT_FLAG_KEY = "flag:autoForceUpdateReload";
const AUTO_CLOSE_POPUP_FLAG_KEY = "flag:autoClosePopup";
// Original 0.3.17 Home lifecycle invokes have no frontend request deadline.
// The native owner and transport close/profile-generation guards still cancel retired work.
const HOME_LIFECYCLE_TIMEOUT_MS = null;
const mapApi = createMapApi(backendBridge);
// Existing read-only local image contract; no new native producer is introduced.
// Browser previews never invoke it or lend their synthetic images to this reader.
const nativeAssetReader = backendBridge.available ? (request) => backendBridge.invoke("game_asset_image", request) : null;

export function normalizeProfileSnapshot(snapshot, fallback = { selectedProfileId: "", profiles: [], maxProfiles: 1 }) {
  const profiles = Array.isArray(snapshot?.profiles) ? snapshot.profiles : fallback.profiles || [];
  const selectedProfileId = typeof snapshot?.selectedProfileId === "string"
    ? snapshot.selectedProfileId
    : fallback.selectedProfileId || "";
  const suppliedCapacity = Number(snapshot?.maxProfiles);
  const fallbackCapacity = Number(fallback?.maxProfiles);
  const maxProfiles = Number.isInteger(suppliedCapacity) && suppliedCapacity > 0
    ? Math.max(suppliedCapacity, profiles.length)
    : Math.max(Number.isInteger(fallbackCapacity) && fallbackCapacity > 0 ? fallbackCapacity : 1, profiles.length);
  return { selectedProfileId, profiles, maxProfiles };
}

export function profileOwnerKey(owner) {
  return `${owner?.generation ?? -1}:${owner?.profileId || ""}`;
}

function legacyServerHistory() {
  try {
    const parsed = JSON.parse(localStorage.getItem(LEGACY_SERVER_HISTORY_KEY) || "[]");
    return Array.isArray(parsed)
      ? parsed.map(Number).filter((value, index, values) => Number.isInteger(value) && value >= 1 && value <= 99999 && values.indexOf(value) === index).slice(0, 5)
      : [];
  } catch {
    return [];
  }
}

function initialRoute() {
  const params = new URLSearchParams(window.location.search);
  const previewPage = params.get("previewPage");
  const requested = params.get("view") || previewPage;
  return routes.some((route) => route.key === requested) ? requested : initialRouteKey;
}

function initialTheme() {
  try {
    if (backendBridge.mode === "preview") {
      const requested = new URLSearchParams(window.location.search).get("previewTheme");
      if (requested === "dark" || requested === "light") return requested;
    }
    return localStorage.getItem(THEME_KEY) === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

function initialAutoLaunchGame() {
  return readAutoLaunchGamePreference(localStorage);
}

function initialAutoScanConfig(profileId, previewState) {
  if (previewState === "map-auto-scheduled" || previewState === "map-auto-running") {
    return normalizeAutoScanConfig({
      enabled: true,
      intervalMinutes: 60,
      serverIds: [321, 322],
      selectedTypes: AUTO_SCAN_DEFAULT_TYPES,
      scanMode: "fast",
      returnToOriginalServer: true,
      nextRunAt: 1_893_456_000_000,
    });
  }
  if (backendBridge.mode === "preview") return loadAutoScanConfig(profileId, window.localStorage);
  return normalizeAutoScanConfig(null);
}

function RetainedPages({ activeRoute, visitedRoutes, selectedProfileId, pageProps, pagePropsByRoute }) {
  return (
    <Fragment key={selectedProfileId}>
      {routes.map((route) => visitedRoutes.has(route.key) ? (
        <Activity key={route.key} mode={route.key === activeRoute ? "visible" : "hidden"}>
          <PageForRoute routeKey={route.key} {...pageProps} {...pagePropsByRoute?.[route.key]} />
        </Activity>
      ) : null)}
    </Fragment>
  );
}

export function App({ shellFlagStates = null, subscribeCloseRequests = null, confirmExit = null, profileSwitchLoading = false } = {}) {
  const { language, setLanguage, t } = useI18n();
  const previewState = backendBridge.mode === "preview" ? new URLSearchParams(window.location.search).get("previewState") || "" : "";
  const profilePreview = backendBridge.mode === "preview" && previewState.startsWith("shell-profiles");
  const [shellProfiles, setShellProfiles] = useState(() => initialShellProfiles(backendBridge.mode, previewState, window.__LWBridgeBootstrap));
  const selectedProfileId = shellProfiles.selectedProfileId || backendBridge.profileId;
  const [previewFlagStates] = useState(() => previewShellFlagStores(backendBridge.mode, previewState));
  const showProfiles = shellProfiles.maxProfiles > 1 || shellProfiles.profiles.length > 1;
  const [focusGameOnProfileSelect, setFocusGameOnProfileSelect] = useState(() => initialProfileFocus(localStorage));
  const updateProfileFocus = (value) => { saveProfileFocus(localStorage, value); setFocusGameOnProfileSelect(value); };
  const previewProfileCache = useRef(new Set(profilePreview && shellProfiles.selectedProfileId ? [shellProfiles.selectedProfileId] : []));
  const previewProfileLoadGeneration = useRef(0);
  const [previewProfileLoading, setPreviewProfileLoading] = useState(false);
  const switchLoading = profileSwitchLoading || previewProfileLoading || (backendBridge.mode === "preview" && previewState === "shell-profiles-loading");
  const nextPreviewProfileId = useRef(3);
  const [previewExitCount, setPreviewExitCount] = useState(() => backendBridge.mode === "preview" && previewState.startsWith("shell-exit") ? 2 : null);
  const profilePreviewCallbacks = profilePreview ? {
    onSelect: async (id) => {
      if (!id || id === shellProfiles.selectedProfileId) return;
      const loadGeneration = previewProfileLoadGeneration.current + 1;
      previewProfileLoadGeneration.current = loadGeneration;
      const cached = previewProfileCache.current.has(id);
      setPreviewProfileLoading(!cached);
      setShellProfiles((current) => ({ ...current, selectedProfileId: id }));
      if (!cached) {
        await new Promise((resolve) => window.requestAnimationFrame(() => window.requestAnimationFrame(resolve)));
        previewProfileCache.current.add(id);
        if (previewProfileLoadGeneration.current === loadGeneration) setPreviewProfileLoading(false);
      }
    },
    onCreate: async () => { const id = `preview-local-${nextPreviewProfileId.current++}`; setShellProfiles((current) => {
      return { ...current, profiles: [...current.profiles, { id, displayName: `Local ${current.profiles.length + 1}`, note: "", serverId: 0, enabled: true }] };
    }); },
    onRemove: async (id) => { setShellProfiles((current) => {
      const profiles = current.profiles.filter((profile) => profile.id !== id);
      return { ...current, profiles, selectedProfileId: current.selectedProfileId === id ? profiles[0]?.id || "" : current.selectedProfileId };
    }); },
    onReorder: async (ids) => { setShellProfiles((current) => ({ ...current,
      profiles: ids.map((id) => current.profiles.find((profile) => profile.id === id)) })); },
    onUpdateNote: async (id, note) => { setShellProfiles((current) => ({ ...current,
      profiles: current.profiles.map((profile) => profile.id === id ? { ...profile, note } : profile) })); },
  } : {};
  const [activeRoute, setActiveRoute] = useState(initialRoute);
  const [visitedRoutes, setVisitedRoutes] = useState(() => new Set([initialRoute()]));
  const [automationCategory, setAutomationCategory] = useState("daily");
  const [mapTab, setMapTab] = useState("city");
  const [squadTab, setSquadTab] = useState("afk");
  const [, startRouteTransition] = useTransition();
  const [theme, setTheme] = useState(initialTheme);
  const [shellUpdateStatus, setShellUpdateStatus] = useState(() => initialShellUpdateStatus(backendBridge.mode, previewState));
  const [serverJumpOpen, setServerJumpOpen] = useState(false);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [autoWeekendShieldIncoming, setAutoWeekendShieldIncoming] = useState(true);
  const [autoAttackShieldIncoming, setAutoAttackShieldIncoming] = useState(true);
  const [autoReconnectIncoming, setAutoReconnectIncoming] = useState(false);
  const [autoClosePopupIncoming, setAutoClosePopupIncoming] = useState(false);
  const [proxyStatus, setProxyStatus] = useState(null);
  const [gameRootStatus, setGameRootStatus] = useState(null);
  const [gameRecoveryStatus, setGameRecoveryStatus] = useState(null);
  const [autoLaunchGame, setAutoLaunchGame] = useState(initialAutoLaunchGame);
  const [homeBusy, setHomeBusy] = useState("");
  const [proxyBusy, setProxyBusy] = useState(false);
  const [gameLaunchBusy, setGameLaunchBusy] = useState(false);
  const [gameRootError, setGameRootError] = useState("");
  const [gameActionError, setGameActionError] = useState("");
  const [currentServerId, setCurrentServerId] = useState(0);
  const [mapRuntime, setMapRuntime] = useState(() => ({ ...DEFAULT_SCAN_STATE }));
  const [mapSummary, setMapSummary] = useState(null);
  const [serverTarget, setServerTarget] = useState("");
  const [serverJumpBusy, setServerJumpBusy] = useState(0);
  const [serverJumpError, setServerJumpError] = useState("");
  const [serverHistory, setServerHistory] = useState([]);
  const [connectionError, setConnectionError] = useState("");
  const [statusPairReady, setStatusPairReady] = useState(backendBridge.mode === "preview");
  const [nativeProfileBusy, setNativeProfileBusy] = useState(false);
  const [nativeProfileMutationBusy, setNativeProfileMutationBusy] = useState(false);
  const nativeProfileMutationInFlightRef = useRef(false);
  const [nativeProfileError, setNativeProfileError] = useState("");
  const [autoScanConfig, setAutoScanConfig] = useState(() => initialAutoScanConfig(selectedProfileId, previewState));
  const [autoScanRunning, setAutoScanRunning] = useState(() => previewState === "map-auto-running");
  const [autoScanRuntimeError, setAutoScanRuntimeError] = useState("");
  const [autoScanActionError, setAutoScanActionError] = useState("");
  const [autoScanSaveError, setAutoScanSaveError] = useState("");
  const mapReadingRef = useRef(false);
  const mapRuntimeRef = useRef({ ...DEFAULT_SCAN_STATE });
  const mapSummaryGeneration = useRef(0);
  const autoScanConfigRef = useRef(autoScanConfig);
  const autoScanCoordinatorRef = useRef(null);
  const serverJumpRef = useRef(null);
  const serverHistoryRef = useRef([]);
  const autoLaunchNativeCommittedByOwnerRef = useRef(new Map());
  const autoLaunchGlobalCommittedRef = useRef(autoLaunchGame);
  const autoLaunchGlobalOwnerRef = useRef(null);
  const autoLaunchSaveRevisionRef = useRef(0);
  const autoLaunchSaveChainRef = useRef(Promise.resolve());
  const autoLaunchNativeCommitEpochRef = useRef(0);
  const autoLaunchConfigPollGenerationRef = useRef(0);
  const statusReadRevisionRef = useRef(0);
  const reconnectStatusGeneration = useRef(0);
  const selectedProfileIdRef = useRef(selectedProfileId);
  const shellProfilesRef = useRef(shellProfiles);
  shellProfilesRef.current = shellProfiles;
  const selectedProfileGenerationRef = useRef(0);
  const selectedProfileOwnerRef = useRef({ profileId: selectedProfileId, generation: 0 });
  if (selectedProfileIdRef.current !== selectedProfileId) {
    selectedProfileGenerationRef.current += 1;
    selectedProfileIdRef.current = selectedProfileId;
  }
  const selectedProfileGeneration = selectedProfileGenerationRef.current;
  selectedProfileOwnerRef.current = {
    profileId: selectedProfileId,
    generation: selectedProfileGeneration,
  };
  const lifecycleInFlightProfilesRef = useRef(new Set());
  const sidebarLifecycleInFlightRef = useRef(new Set());
  // The original Home and the retained native sidebar are separate controls,
  // but each exact profile owns ONE native lifecycle. A global reconcile/
  // restart holds every owner until its result returns.
  const globalNativeLifecycleInFlightRef = useRef(false);
  const startupReconcileStartedRef = useRef(false);
  const startupAutoLaunchGameRef = useRef(autoLaunchGame);
  const nativeProfileRequestRef = useRef(0);
  const nativeProfileSelectionRevisionRef = useRef(0);
  const nativeReorderRevisionRef = useRef(0);
  const nativeReorderAcknowledgedRevisionRef = useRef(0);
  const nativeReorderWriteChainRef = useRef(Promise.resolve());
  const nativeNoteRevisionsRef = useRef(new Map());
  const nativeNoteAcknowledgedRevisionsRef = useRef(new Map());
  const nativeNoteWriteChainsRef = useRef(new Map());
  const isCurrentProfileOwner = useCallback((owner) => (
    owner?.profileId === selectedProfileOwnerRef.current.profileId
    && owner?.generation === selectedProfileOwnerRef.current.generation
  ), []);
  const advanceProfileOwner = useCallback((profileId) => {
    if (selectedProfileIdRef.current !== profileId) {
      selectedProfileGenerationRef.current += 1;
      selectedProfileIdRef.current = profileId;
    }
    const owner = { profileId, generation: selectedProfileGenerationRef.current };
    selectedProfileOwnerRef.current = owner;
    return owner;
  }, []);
  const profileDraftGeneration = backendBridge.mode === "native" ? selectedProfileGeneration : 0;
  const profileConfigDrafts = useMemo(() => createProfileConfigDraftRegistry(), [profileDraftGeneration]);
  useEffect(() => () => profileConfigDrafts.dispose(), [profileConfigDrafts]);
  const autoWeekendShieldStore = profileConfigDrafts.get(
    selectedProfileId,
    AUTO_WEEKEND_SHIELD_FLAG_KEY,
    autoWeekendShieldIncoming,
    createAutomationFlagAdapter(
      backendBridge,
      selectedProfileId,
      "autoWeekendShield",
      "auto_weekend_shield",
      true,
      "auto_shield",
    ),
  );
  const autoAttackShieldStore = profileConfigDrafts.get(
    selectedProfileId,
    AUTO_ATTACK_SHIELD_FLAG_KEY,
    autoAttackShieldIncoming,
    createAutomationFlagAdapter(
      backendBridge,
      selectedProfileId,
      "autoAttackShield",
      "auto_attack_shield",
      true,
      "auto_shield",
    ),
  );
  const autoReconnectStore = profileConfigDrafts.get(
    selectedProfileId,
    AUTO_RECONNECT_FLAG_KEY,
    autoReconnectIncoming,
    createAutomationFlagAdapter(
      backendBridge,
      selectedProfileId,
      "autoForceUpdateReload",
      "auto_force_update_reload",
      false,
    ),
  );
  const autoClosePopupStore = profileConfigDrafts.get(
    selectedProfileId,
    AUTO_CLOSE_POPUP_FLAG_KEY,
    autoClosePopupIncoming,
    createAutomationFlagAdapter(
      backendBridge,
      selectedProfileId,
      "autoClosePopup",
      "auto_close_popup",
      false,
    ),
  );
  const autoReconnectSnapshot = useSyncExternalStore(
    autoReconnectStore.subscribe,
    autoReconnectStore.getSnapshot,
    autoReconnectStore.getSnapshot,
  );

  const adoptNativeProfileSnapshot = useCallback((
    snapshot, expectedSelectedProfileId = "", preserveCurrentProfiles = false,
  ) => {
    let next = normalizeProfileSnapshot(snapshot, shellProfilesRef.current);
    if (expectedSelectedProfileId && next.selectedProfileId !== expectedSelectedProfileId) {
      const error = new Error("Native profile selection acknowledgement did not match the requested profile.");
      error.code = "PROFILE_SELECTION_MISMATCH";
      throw error;
    }
    if (preserveCurrentProfiles) {
      // The native B owner was selected, but its snapshot may have been
      // captured BEFORE an independent durable A/B note or reorder. Adopt
      // B selection without rolling the later profile metadata back.
      const current = shellProfilesRef.current;
      const authoritativeIds = new Set(next.profiles.map((profile) => profile.id));
      if (current.profiles.length === next.profiles.length &&
          current.profiles.every((profile) => authoritativeIds.has(profile.id)) &&
          current.profiles.some((profile) => profile.id === next.selectedProfileId)) {
        next = { ...next, profiles: current.profiles };
      }
    }
    if (next.selectedProfileId && next.selectedProfileId !== backendBridge.profileId) {
      backendBridge.setSelectedProfile(next.selectedProfileId);
      advanceProfileOwner(next.selectedProfileId);
      setStatusPairReady(false);
      setConnectionError("");
    }
    setShellProfiles(next);
    return next;
  }, [advanceProfileOwner]);

  const selectNativeProfile = useCallback(async (profileId, focusGame = true) => {
    if (!backendBridge.available || backendBridge.mode !== "native" || !profileId) return undefined;
    if (profileId === selectedProfileIdRef.current) return shellProfilesRef.current;
    const request = nativeProfileRequestRef.current + 1;
    nativeProfileRequestRef.current = request;
    // Registry note/reorder may legitimately complete while this selected
    // native owner is pending. They must not retire its acknowledgement or
    // permanently hold the selection busy flag; only a newer Select can.
    const selectionRevision = ++nativeProfileSelectionRevisionRef.current;
    setNativeProfileBusy(true);
    setNativeProfileError("");
    try {
      const snapshot = await backendBridge.invoke("profile_select", { profileId, focusGame: focusGame === true });
      if (nativeProfileSelectionRevisionRef.current !== selectionRevision) return snapshot;
      return adoptNativeProfileSnapshot(
        snapshot, profileId, nativeProfileRequestRef.current !== request,
      );
    } catch (error) {
      if (nativeProfileSelectionRevisionRef.current === selectionRevision)
        setNativeProfileError(error?.code || error?.message || String(error));
      return undefined;
    } finally {
      if (nativeProfileSelectionRevisionRef.current === selectionRevision) setNativeProfileBusy(false);
    }
  }, [adoptNativeProfileSnapshot]);

  // F-07 OWN_DESIGN: real native local-profile CRUD, not preview Add/Delete.
  // Keep roster mutations serial and let the registry enforce capacity,
  // primary/selected invariants and stopped-owner deletion safety.
  const mutateNativeProfiles = useCallback(async (command, payload = {}) => {
    if (!backendBridge.available || backendBridge.mode !== "native" || nativeProfileMutationInFlightRef.current)
      return undefined;
    nativeProfileMutationInFlightRef.current = true;
    setNativeProfileMutationBusy(true);
    setNativeProfileError("");
    try {
      const snapshot = await backendBridge.invoke(command, payload);
      nativeProfileRequestRef.current += 1;
      return adoptNativeProfileSnapshot(snapshot);
    } catch (error) {
      setNativeProfileError(error?.code || error?.message || String(error));
      throw error;
    } finally {
      nativeProfileMutationInFlightRef.current = false;
      setNativeProfileMutationBusy(false);
    }
  }, [adoptNativeProfileSnapshot]);
  const createNativeProfile = useCallback(() => mutateNativeProfiles("profile_create"), [mutateNativeProfiles]);
  const removeNativeProfile = useCallback((profileId) => mutateNativeProfiles(
    "profile_delete", { profileId },
  ), [mutateNativeProfiles]);

  // F-07 OWN_DESIGN: enable/disable the local native profile for future
  // selection, manual Start and auto-launch. Only project the acknowledged
  // target flag: a pending A toggle must not undo a newer B selection, note
  // or reorder. Existing running owners remain independently stoppable.
  const setNativeProfileEnabled = useCallback(async (profileId, enabled) => {
    if (!backendBridge.available || backendBridge.mode !== "native") return undefined;
    if (nativeProfileMutationInFlightRef.current) {
      const error = new Error("A profile mutation is already in progress.");
      error.code = "GAME_OPERATION_IN_PROGRESS";
      throw error;
    }
    nativeProfileMutationInFlightRef.current = true;
    setNativeProfileMutationBusy(true);
    setNativeProfileError("");
    try {
      const result = await backendBridge.invoke("profile_enabled_set", { profileId, enabled });
      const acknowledged = normalizeProfileSnapshot(result, shellProfilesRef.current)
        .profiles.find((profile) => profile.id === profileId);
      if (!acknowledged || acknowledged.enabled !== enabled)
        throw new Error("PROFILE_ENABLED_ACK_MISMATCH");
      nativeProfileRequestRef.current += 1;
      setShellProfiles((current) => ({
        ...current,
        profiles: current.profiles.map((profile) => profile.id === profileId
          ? { ...profile, enabled: acknowledged.enabled } : profile),
      }));
      return result;
    } catch (error) {
      setNativeProfileError(error?.code || error?.message || String(error));
      throw error;
    } finally {
      nativeProfileMutationInFlightRef.current = false;
      setNativeProfileMutationBusy(false);
    }
  }, []);

  const reorderNativeProfiles = useCallback(async (profileIds) => {
    if (!backendBridge.available || backendBridge.mode !== "native") return undefined;
    const request = nativeProfileRequestRef.current + 1;
    nativeProfileRequestRef.current = request;
    const reorderRevision = ++nativeReorderRevisionRef.current;
    setNativeProfileError("");
    // Profile display order is ONE shared registry field. Repeated JSX
    // drags are independently admitted while an earlier native request is
    // pending; execute reorder writes in submit order or a late older B,A
    // write can overwrite newer durable A,B while the React revision
    // fence correctly hides its stale acknowledgement. This queues only
    // reorders, never independent A/B notes, selects, or lifecycle work.
    const previous = nativeReorderWriteChainRef.current;
    const write = previous.catch(() => undefined).then(
      () => backendBridge.invoke("profile_reorder", { profileIds }),
    );
    nativeReorderWriteChainRef.current = write;
    try {
      const snapshot = await write;
      // R15 serializes writes to the shared native order field. A later
      // *requested* reorder may fail, so always project each successfully
      // committed ordered result, even when newer metadata/select requests
      // exist. Only exact order is adopted: never replace the selected owner,
      // notes or a different registry roster with a stale full snapshot.
      if (nativeReorderAcknowledgedRevisionRef.current < reorderRevision) {
        const authoritative = normalizeProfileSnapshot(snapshot, shellProfilesRef.current);
        nativeReorderAcknowledgedRevisionRef.current = reorderRevision;
        setShellProfiles((current) => {
          const visibleById = new Map(current.profiles.map((profile) => [profile.id, profile]));
          const ordered = authoritative.profiles.map((profile) => visibleById.get(profile.id));
          if (ordered.length !== current.profiles.length || ordered.some((profile) => !profile))
            return current;
          return { ...current, profiles: ordered };
        });
      }
      return snapshot;
    } catch (error) {
      if (nativeProfileRequestRef.current === request &&
          nativeReorderRevisionRef.current === reorderRevision)
        setNativeProfileError(error?.code || error?.message || String(error));
      return undefined;
    }
  }, []);

  const updateNativeProfileNote = useCallback(async (profileId, note) => {
    if (!backendBridge.available || backendBridge.mode !== "native") return undefined;
    const request = nativeProfileRequestRef.current + 1;
    nativeProfileRequestRef.current = request;
    const noteRevision = (nativeNoteRevisionsRef.current.get(profileId) || 0) + 1;
    nativeNoteRevisionsRef.current.set(profileId, noteRevision);
    setNativeProfileError("");
    // Two rapid submits for the SAME retained account must commit in the
    // user's order. Otherwise late native execution of first note X can
    // overwrite second note Y in SQLite while JSX displays Y. Do not use a
    // global queue: A and B metadata writes remain independent.
    const previous = nativeNoteWriteChainsRef.current.get(profileId) || Promise.resolve();
    const write = previous.catch(() => undefined).then(
      () => backendBridge.invoke("profile_note_set", { profileId, note }),
    );
    nativeNoteWriteChainsRef.current.set(profileId, write);
    try {
      const snapshot = await write;
      // The per-ID native write chain acknowledges commits in submit order.
      // A newer *requested* note may fail: suppressing this successful X
      // because pending Y has a higher revision leaves SQLite X but JSX old.
      // Project each ordered SUCCESS for this exact owner, never the entire
      // snapshot (whose selected owner/order may already be stale).
      if ((nativeNoteAcknowledgedRevisionsRef.current.get(profileId) || 0) < noteRevision) {
        const authoritative = normalizeProfileSnapshot(snapshot, shellProfilesRef.current);
        const saved = authoritative.profiles.find((profile) => profile.id === profileId);
        if (saved) {
          nativeNoteAcknowledgedRevisionsRef.current.set(profileId, noteRevision);
          setShellProfiles((current) => ({
            ...current,
            profiles: current.profiles.map((profile) => (
              profile.id === profileId ? { ...profile, note: saved.note } : profile
            )),
          }));
        }
      }
      return snapshot;
    } catch (error) {
      if (nativeProfileRequestRef.current === request &&
          nativeNoteRevisionsRef.current.get(profileId) === noteRevision)
        setNativeProfileError(error?.code || error?.message || String(error));
      throw error;
    } finally {
      if (nativeNoteWriteChainsRef.current.get(profileId) === write)
        nativeNoteWriteChainsRef.current.delete(profileId);
    }
  }, []);

  // Sidebar actions address retained native owners directly. They must survive
  // A/B/A view selection without reassigning or retiring either running owner.
  const readNativeInstance = useCallback((profileId) => backendBridge.invoke(
    "profile_instance_status", { profileId },
  ), []);
  const runNativeProfileAction = useCallback(async (profileId, command, instanceId) => {
    if (!backendBridge.available || !profileId) return null;
    if (globalNativeLifecycleInFlightRef.current || sidebarLifecycleInFlightRef.current.has(profileId)) {
      const error = new Error("A game lifecycle operation is already in progress.");
      error.code = "GAME_OPERATION_IN_PROGRESS";
      throw error;
    }
    sidebarLifecycleInFlightRef.current.add(profileId);
    try {
      const payload = { profileId };
      if (command === "profile_instance_start") payload.closeUnmanaged = true;
      else if (typeof instanceId === "string") payload.instanceId = instanceId;
      await backendBridge.invoke(command, payload, HOME_LIFECYCLE_TIMEOUT_MS);
      return await readNativeInstance(profileId);
    } finally {
      sidebarLifecycleInFlightRef.current.delete(profileId);
    }
  }, [readNativeInstance]);
  const startNativeProfile = useCallback((profileId) => runNativeProfileAction(
    profileId, "profile_instance_start",
  ), [runNativeProfileAction]);
  const stopNativeProfile = useCallback((profileId, instanceId) => runNativeProfileAction(
    profileId, "profile_instance_stop", instanceId,
  ), [runNativeProfileAction]);
  const restartAllNativeProfiles = useCallback(async () => {
    if (globalNativeLifecycleInFlightRef.current || sidebarLifecycleInFlightRef.current.size) {
      const failure = new Error("A game lifecycle operation is already in progress.");
      failure.code = "GAME_OPERATION_IN_PROGRESS";
      throw failure;
    }
    globalNativeLifecycleInFlightRef.current = true;
    try {
      const result = await backendBridge.invoke("profile_instances_update_and_restart", {}, HOME_LIFECYCLE_TIMEOUT_MS);
      if (result?.errors?.length) {
        const failure = new Error(result.errors[0]?.message || result.errors[0]?.error || "GAME_CONNECTION_UPDATE_FAILED");
        failure.code = result.errors[0]?.error || "GAME_CONNECTION_UPDATE_FAILED";
        throw failure;
      }
      return result;
    } finally {
      globalNativeLifecycleInFlightRef.current = false;
    }
  }, []);

  useEffect(() => {
    if (!backendBridge.available || backendBridge.mode !== "native") return undefined;
    let closed = false;
    const request = nativeProfileRequestRef.current;
    backendBridge.invoke("profile_list", {}).then((snapshot) => {
      if (!closed && nativeProfileRequestRef.current === request) adoptNativeProfileSnapshot(snapshot);
    }).catch((error) => {
      if (!closed && nativeProfileRequestRef.current === request) setNativeProfileError(error?.code || error?.message || String(error));
    });
    return () => { closed = true; };
  }, [adoptNativeProfileSnapshot]);

  useEffect(() => {
    autoWeekendShieldStore.receive(autoWeekendShieldIncoming);
  }, [autoWeekendShieldIncoming, autoWeekendShieldStore]);

  useEffect(() => {
    autoAttackShieldStore.receive(autoAttackShieldIncoming);
  }, [autoAttackShieldIncoming, autoAttackShieldStore]);

  useEffect(() => {
    autoReconnectStore.receive(autoReconnectIncoming);
  }, [autoReconnectIncoming, autoReconnectStore]);

  useEffect(() => {
    autoClosePopupStore.receive(autoClosePopupIncoming);
  }, [autoClosePopupIncoming, autoClosePopupStore]);

  useLayoutEffect(() => {
    reconnectStatusGeneration.current += 1;
    setStatusPairReady(backendBridge.mode === "preview");
    setConnectionError("");
    if (backendBridge.mode === "native") {
      setRuntimeStatus(null);
      setProxyStatus(null);
      setGameRootStatus(null);
      setGameRecoveryStatus(null);
      setGameRootError("");
      setGameActionError("");
      setHomeBusy("");
    }
    setProxyBusy(false);
    setGameLaunchBusy(false);
    return () => { reconnectStatusGeneration.current += 1; };
  }, [selectedProfileId]);

  const acknowledgeRuntimeStatus = useCallback((
    status,
    generation = reconnectStatusGeneration.current,
    owner = selectedProfileOwnerRef.current,
  ) => {
    if (generation !== reconnectStatusGeneration.current || !isCurrentProfileOwner(owner)) return;
    setRuntimeStatus(status);
    const config = status?.config;
    const legacyShield = config?.auto_shield;
    const weekend = typeof config?.auto_weekend_shield === "boolean" ? config.auto_weekend_shield : legacyShield;
    const attack = typeof config?.auto_attack_shield === "boolean" ? config.auto_attack_shield : legacyShield;
    const reconnect = config?.auto_force_update_reload;
    const closePopup = config?.auto_close_popup;
    if (typeof weekend === "boolean") setAutoWeekendShieldIncoming(weekend);
    if (typeof attack === "boolean") setAutoAttackShieldIncoming(attack);
    if (typeof reconnect === "boolean") setAutoReconnectIncoming(reconnect);
    if (typeof closePopup === "boolean") setAutoClosePopupIncoming(closePopup);
  }, [isCurrentProfileOwner]);

  const applyAutoScanConfigSnapshot = useCallback((snapshot) => {
    const next = normalizeAutoScanConfig(snapshot?.config);
    autoScanConfigRef.current = next;
    setAutoScanConfig(next);
  }, []);

  const applyAutoScanRuntimeSnapshot = useCallback((snapshot) => {
    setAutoScanRunning(snapshot?.running === true);
    setAutoScanRuntimeError(typeof snapshot?.lastError === "string" ? snapshot.lastError : "");
  }, []);

  useLayoutEffect(() => {
    autoScanCoordinatorRef.current?.retire();
    const next = initialAutoScanConfig(selectedProfileId, previewState);
    autoScanConfigRef.current = next;
    setAutoScanConfig(next);
    setAutoScanRunning(previewState === "map-auto-running");
    setAutoScanRuntimeError("");
    setAutoScanActionError("");
    setAutoScanSaveError("");

    if (!backendBridge.available || backendBridge.mode === "preview" || !selectedProfileId) {
      autoScanCoordinatorRef.current = null;
      return undefined;
    }

    const coordinator = createAutoScanNativeCoordinator({
      saveConfig: (config) => mapApi.updateAutoScanConfig(config),
      readStatus: () => mapApi.autoScanStatus(),
      runNow: () => mapApi.runAutoScanNow(),
      mergeConfig: applyAutoScanConfigIntent,
      onConfigSnapshot: applyAutoScanConfigSnapshot,
      onRuntimeSnapshot: applyAutoScanRuntimeSnapshot,
      onWriteError: setAutoScanSaveError,
      onActionError: setAutoScanActionError,
    });
    autoScanCoordinatorRef.current = coordinator;
    return () => {
      coordinator.retire();
      if (autoScanCoordinatorRef.current === coordinator) autoScanCoordinatorRef.current = null;
    };
  }, [applyAutoScanConfigSnapshot, applyAutoScanRuntimeSnapshot, selectedProfileId]);

  const updateAutoScanConfig = useCallback((patch, operation = null) => {
    const editedAt = Date.now();
    const edit = { patch, operation, editedAt };
    const next = applyAutoScanConfigIntent(autoScanConfigRef.current, edit);
    autoScanConfigRef.current = next;
    setAutoScanConfig(next);
    if (backendBridge.mode === "preview") {
      saveAutoScanConfig(selectedProfileId, next, window.localStorage);
      return;
    }
    autoScanCoordinatorRef.current?.save(next, edit);
  }, [selectedProfileId]);

  const runAutoScanNow = useCallback(() => {
    if (backendBridge.mode === "preview") {
      updateAutoScanConfig({ ...autoScanConfigRef.current, nextRunAt: Date.now() });
      return;
    }
    autoScanCoordinatorRef.current?.run();
  }, [selectedProfileId, updateAutoScanConfig]);

  useEffect(() => {
    if (backendBridge.mode !== "preview") return;
    const requested = new URLSearchParams(window.location.search).get("previewLanguage");
    if (LANGUAGES.some(({ code }) => code === requested) && requested !== language) setLanguage(requested);
  }, [language, setLanguage]);

  const acknowledgeGameRootStatus = useCallback((next) => {
    setGameRootStatus(next);
    setGameRootError("");
  }, []);

  const refreshMapSummary = useCallback(async () => {
    if (!backendBridge.available) return null;
    const generation = mapSummaryGeneration.current + 1;
    mapSummaryGeneration.current = generation;
    const owner = { ...selectedProfileOwnerRef.current };
    const summary = await mapApi.summary();
    if (generation !== mapSummaryGeneration.current || !isCurrentProfileOwner(owner)) return summary;
    setMapSummary(summary);
    mapReadingRef.current = summary.scanState?.isReading === true;
    mapRuntimeRef.current = summary.scanState;
    setMapRuntime(summary.scanState);
    setCurrentServerId(summary.scanState?.serverId > 0 ? summary.scanState.serverId : 0);
    return summary;
  }, [isCurrentProfileOwner, selectedProfileId]);

  const selectRoute = useCallback((routeKey) => {
    if (routeKey === activeRoute) return;
    if (routeKey === "map-data") refreshMapSummary().catch(() => {});
    preloadRoute(routeKey);
    startRouteTransition(() => {
      setVisitedRoutes((current) => {
        if (current.has(routeKey)) return current;
        const next = new Set(current);
        next.add(routeKey);
        return next;
      });
      setActiveRoute(routeKey);
    });
  }, [activeRoute, refreshMapSummary, startRouteTransition]);

  const acknowledgeMapScan = useCallback((scan) => {
    if (!scan) return;
    const wasReading = mapReadingRef.current;
    mapReadingRef.current = scan.isReading === true;
    mapRuntimeRef.current = scan;
    setCurrentServerId(scan.serverId > 0 ? scan.serverId : 0);
    setMapRuntime(scan);
    setMapSummary((current) => current?.serverId === scan.serverId ? { ...current, scanState: scan } : current);
    if (wasReading && !scan.isReading) refreshMapSummary().catch(() => {});
  }, [refreshMapSummary]);

  const acknowledgeMapCounts = useCallback((serverId, counts) => {
    setMapSummary((current) => {
      if (current?.serverId === serverId) return { ...current, counts };
      if (mapRuntimeRef.current?.serverId === serverId) return { serverId, counts, scanState: mapRuntimeRef.current };
      return current;
    });
  }, []);

  const readStatusSnapshot = useCallback(async (canAcknowledge = () => true) => {
    if (!backendBridge.available) return;
    const statusReadRevision = statusReadRevisionRef.current + 1;
    statusReadRevisionRef.current = statusReadRevision;
    const owner = { ...selectedProfileOwnerRef.current };
    if (!isCurrentProfileOwner(owner)) return;
    setStatusPairReady(false);
    setConnectionError("");
    const reconnectGeneration = reconnectStatusGeneration.current;
    const autoLaunchRevision = autoLaunchSaveRevisionRef.current;
    const autoLaunchNativeCommitEpoch = autoLaunchNativeCommitEpochRef.current;
    const autoLaunchConfigPollGeneration = autoLaunchConfigPollGenerationRef.current + 1;
    autoLaunchConfigPollGenerationRef.current = autoLaunchConfigPollGeneration;
    const [statusResult, proxyResult, configResult] = await Promise.allSettled([
      mapApi.readStatus(),
      mapApi.readProxyStatus(),
      backendBridge.invoke("local_config_get", { profileId: owner.profileId }),
    ]);
    if (!canAcknowledge()
      || !isCurrentProfileOwner(owner)
      || statusReadRevisionRef.current !== statusReadRevision) return;
    if (statusResult.status === "fulfilled") {
      acknowledgeRuntimeStatus(statusResult.value, reconnectGeneration, owner);
    }
    if (proxyResult.status === "fulfilled") setProxyStatus(proxyResult.value);
    if (configResult.status === "fulfilled") {
      if (autoLaunchConfigPollGenerationRef.current === autoLaunchConfigPollGeneration
        && autoLaunchSaveRevisionRef.current === autoLaunchRevision
        && autoLaunchNativeCommitEpochRef.current === autoLaunchNativeCommitEpoch
        && typeof configResult.value?.autoLaunchGame === "boolean") {
        autoLaunchNativeCommittedByOwnerRef.current.set(
          profileOwnerKey(owner),
          configResult.value.autoLaunchGame,
        );
      }
    }
    if (statusResult.status === "rejected" || proxyResult.status === "rejected") {
      const failure = statusResult.status === "rejected" ? statusResult.reason : proxyResult.reason;
      setStatusPairReady(false);
      setConnectionError(failure?.message || String(failure));
    } else {
      setStatusPairReady(true);
      setConnectionError("");
    }
  }, [acknowledgeRuntimeStatus, isCurrentProfileOwner]);

  const refreshStatus = useCallback(() => readStatusSnapshot(), [readStatusSnapshot]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId || startupReconcileStartedRef.current) return;
    startupReconcileStartedRef.current = true;
    globalNativeLifecycleInFlightRef.current = true;
    let closed = false;
    const owner = { ...selectedProfileOwnerRef.current };
    const profileId = owner.profileId;
    const ownerKey = profileOwnerKey(owner);
    lifecycleInFlightProfilesRef.current.add(ownerKey);
    setGameLaunchBusy(true);
    setGameActionError("");
    backendBridge.invoke(
      "profile_instances_reconcile",
      { autoLaunchAll: startupAutoLaunchGameRef.current },
      HOME_LIFECYCLE_TIMEOUT_MS,
    ).then(async (result) => {
      if (closed || !isCurrentProfileOwner(owner)) return;
      const profileError = Array.isArray(result?.errors)
        ? result.errors.find((entry) => !entry?.profileId || entry.profileId === profileId)
        : null;
      if (profileError?.error || profileError?.message) {
        setGameActionError(profileError.error || profileError.message);
      }
      await readStatusSnapshot(() => !closed && isCurrentProfileOwner(owner));
    }).catch((error) => {
      if (!closed && isCurrentProfileOwner(owner)) {
        setGameActionError(error?.code || error?.message || String(error));
      }
    }).finally(() => {
      lifecycleInFlightProfilesRef.current.delete(ownerKey);
      globalNativeLifecycleInFlightRef.current = false;
      if (!closed && isCurrentProfileOwner(owner)) {
        setGameLaunchBusy(false);
      }
    });
    return () => { closed = true; };
  }, [isCurrentProfileOwner, readStatusSnapshot, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    let closed = false;
    const owner = { ...selectedProfileOwnerRef.current };
    const profileId = owner.profileId;
    const stop = backendBridge.listen("bridge://game-recovery", (event) => {
      const payload = unwrapProfileEvent(event, profileId);
      if (!closed && isCurrentProfileOwner(owner) && payload) setGameRecoveryStatus(payload);
    });
    backendBridge.invokeProfileScoped("game_recovery_status", {}).then((status) => {
      if (!closed && isCurrentProfileOwner(owner) && status) setGameRecoveryStatus(status);
    }).catch(() => {});
    return () => { closed = true; stop(); };
  }, [isCurrentProfileOwner, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return;
    let closed = false;
    const owner = { ...selectedProfileOwnerRef.current };
    backendBridge.invoke("game_root_status", {}).then((rootStatus) => {
      if (closed || !isCurrentProfileOwner(owner)) return;
      acknowledgeGameRootStatus(rootStatus);
    }).catch((error) => {
      if (!closed && isCurrentProfileOwner(owner)) setGameRootError(error?.message || String(error));
    });
    return () => { closed = true; };
  }, [acknowledgeGameRootStatus, isCurrentProfileOwner, selectedProfileId]);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    try {
      localStorage.setItem(THEME_KEY, theme);
    } catch {
      // Static preview remains usable when storage is unavailable.
    }
  }, [theme]);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    let closed = false;
    const stop = backendBridge.listen("bridge://update-status", (status) => {
      if (!closed && status) setShellUpdateStatus(status);
    });
    backendBridge.invoke("update_status", {}).then((status) => {
      if (!closed) setShellUpdateStatus(status);
    }).catch(() => {});
    return () => { closed = true; stop(); };
  }, []);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    let closed = false;
    let inFlight = false;
    const owner = { ...selectedProfileOwnerRef.current };
    const unsubscribeStatus = mapApi.listenStatus((status) => {
      if (!closed && isCurrentProfileOwner(owner)) {
        statusReadRevisionRef.current += 1;
        acknowledgeRuntimeStatus(status, reconnectStatusGeneration.current, owner);
      }
    });
    const unsubscribeScan = mapApi.listenScanStatus((scan) => {
      if (!closed && isCurrentProfileOwner(owner)) acknowledgeMapScan(scan);
    });
    const pollStatus = async () => {
      if (inFlight) return;
      inFlight = true;
      try {
        await readStatusSnapshot(() => !closed && isCurrentProfileOwner(owner));
      } finally {
        inFlight = false;
      }
    };
    pollStatus();
    const timer = window.setInterval(() => { pollStatus(); }, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
      unsubscribeStatus();
      unsubscribeScan();
    };
  }, [acknowledgeMapScan, acknowledgeRuntimeStatus, isCurrentProfileOwner, readStatusSnapshot, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || backendBridge.mode === "preview" || !selectedProfileId) return undefined;
    let closed = false;
    const owner = { ...selectedProfileOwnerRef.current };
    const acknowledge = (snapshot) => {
      if (!closed && isCurrentProfileOwner(owner)) autoScanCoordinatorRef.current?.receive(snapshot);
    };
    const unsubscribe = mapApi.listenAutoScanChanged(acknowledge);
    mapApi.autoScanStatus().then(acknowledge).catch((error) => {
      if (!closed && isCurrentProfileOwner(owner)) setAutoScanRuntimeError(error?.message || String(error));
    });
    return () => {
      closed = true;
      unsubscribe();
    };
  }, [isCurrentProfileOwner, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    mapReadingRef.current = false;
    mapRuntimeRef.current = { ...DEFAULT_SCAN_STATE };
    setMapRuntime({ ...DEFAULT_SCAN_STATE });
    setMapSummary(null);
    refreshMapSummary().catch(() => {});
    return () => { mapSummaryGeneration.current += 1; };
  }, [refreshMapSummary, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    let closed = false;
    serverHistoryRef.current = [];
    setServerHistory([]);
    setServerJumpError("");
    mapApi.importServerJumpHistory(legacyServerHistory()).then((history) => {
      if (!closed) {
        serverHistoryRef.current = history;
        setServerHistory(history);
      }
      try { localStorage.removeItem(LEGACY_SERVER_HISTORY_KEY); } catch {}
    }).catch(() => {
      if (!closed) setServerJumpError(t("common.actionFailed"));
    });
    return () => { closed = true; };
  }, [selectedProfileId]);

  useEffect(() => {
    if (!serverJumpOpen) return undefined;
    const closeOutside = (event) => {
      if (!serverJumpRef.current?.contains(event.target)) setServerJumpOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [serverJumpOpen]);

  const bridgeState = connectionState(
    runtimeStatus,
    proxyStatus,
    backendBridge.mode,
    statusPairReady,
    connectionError,
  );
  const online = bridgeState === "connected";

  useEffect(() => {
    if (!online || !backendBridge.available) return undefined;
    let closed = false;
    let polling = false;
    const poll = async () => {
      if (closed || polling) return;
      polling = true;
      try {
        await refreshMapSummary();
      } catch {
        // The reference parent logs summary failures without replacing the current Map state.
      } finally {
        polling = false;
      }
    };
    poll();
    const timer = window.setInterval(poll, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
    };
  }, [online, refreshMapSummary]);

  const updateAutoLaunch = useCallback((value) => {
    if (!backendBridge.available) return undefined;
    const owner = { ...selectedProfileOwnerRef.current };
    const ownerKey = profileOwnerKey(owner);
    const enabled = value === true;
    // A new native owner inherits the already-visible global/local intent. Its
    // native gate is not authority for that rollback baseline. Same-owner edits
    // retain the baseline so earlier success / later failure converges correctly.
    if (autoLaunchGlobalOwnerRef.current !== ownerKey) {
      autoLaunchGlobalOwnerRef.current = ownerKey;
      autoLaunchGlobalCommittedRef.current = autoLaunchGame;
    }
    const revision = autoLaunchSaveRevisionRef.current + 1;
    autoLaunchSaveRevisionRef.current = revision;
    writeAutoLaunchGamePreference(localStorage, enabled);
    setAutoLaunchGame(enabled);

    const save = autoLaunchSaveChainRef.current
      .catch(() => undefined)
      .then(() => {
        if (!isCurrentProfileOwner(owner)) return null;
        return backendBridge.invoke("local_config_set", { profileId: owner.profileId, autoLaunchGame: enabled });
      })
      .then((next) => {
        if (next === null || !isCurrentProfileOwner(owner)) return next;
        if (typeof next?.autoLaunchGame !== "boolean") {
          throw new Error("local_config_set returned an invalid autoLaunchGame value.");
        }
        autoLaunchNativeCommitEpochRef.current += 1;
        autoLaunchNativeCommittedByOwnerRef.current.set(ownerKey, next.autoLaunchGame);
        autoLaunchGlobalCommittedRef.current = next.autoLaunchGame;
        if (autoLaunchSaveRevisionRef.current === revision) {
          writeAutoLaunchGamePreference(localStorage, next.autoLaunchGame);
          setAutoLaunchGame(next.autoLaunchGame);
        }
        return next;
      })
      .catch((error) => {
        if (isCurrentProfileOwner(owner) && autoLaunchSaveRevisionRef.current === revision) {
          const rollback = autoLaunchGlobalCommittedRef.current;
          writeAutoLaunchGamePreference(localStorage, rollback);
          setAutoLaunchGame(rollback);
          setGameActionError(error?.message || String(error));
        }
        return null;
      });

    autoLaunchSaveChainRef.current = save;
    return save;
  }, [autoLaunchGame, isCurrentProfileOwner]);

  const updateAutoReconnect = useCallback(async (value) => {
    if (!backendBridge.available) return;
    autoReconnectStore.edit(value, false);
    await autoReconnectStore.flush().catch(() => {});
  }, [autoReconnectStore]);

  const selectGameRoot = useCallback(async () => {
    if (!backendBridge.available) return;
    const owner = { ...selectedProfileOwnerRef.current };
    setHomeBusy("gameRoot");
    try {
      const selection = await backendBridge.invoke("game_root_select", {});
      if (!isCurrentProfileOwner(owner)) return;
      if (selection.canceled) return;
      if (!selection.valid) {
        setGameRootError("INVALID_GAME_ROOT");
        return;
      }
      const next = await backendBridge.invoke("game_root_status", {});
      if (!isCurrentProfileOwner(owner)) return;
      acknowledgeGameRootStatus(next);
    } catch (error) {
      if (isCurrentProfileOwner(owner)) setGameRootError(error?.message || String(error));
    } finally {
      if (isCurrentProfileOwner(owner)) setHomeBusy("");
    }
  }, [acknowledgeGameRootStatus, isCurrentProfileOwner]);

  const refreshHomeProxyStatus = useCallback(async (owner) => {
    if (!isCurrentProfileOwner(owner)) return null;
    const next = await backendBridge.invokeProfileScoped("proxy_status", {});
    if (isCurrentProfileOwner(owner)) setProxyStatus(next);
    return next;
  }, [isCurrentProfileOwner]);

  const startGame = useCallback(async () => {
    if (!backendBridge.available || !selectedProfileId) return;
    const owner = { ...selectedProfileOwnerRef.current };
    const ownerKey = profileOwnerKey(owner);
    if (globalNativeLifecycleInFlightRef.current || lifecycleInFlightProfilesRef.current.has(ownerKey) ||
        sidebarLifecycleInFlightRef.current.has(owner.profileId)) return;
    lifecycleInFlightProfilesRef.current.add(ownerKey);
    sidebarLifecycleInFlightRef.current.add(owner.profileId);
    setProxyBusy(true);
    setGameActionError("");
    try {
      await backendBridge.invokeProfileScoped(
        "profile_instance_start",
        { closeUnmanaged: true },
        HOME_LIFECYCLE_TIMEOUT_MS,
      );
      await refreshHomeProxyStatus(owner);
    } catch (error) {
      if (isCurrentProfileOwner(owner)) {
        setGameActionError(error?.code || error?.message || String(error));
      }
    } finally {
      lifecycleInFlightProfilesRef.current.delete(ownerKey);
      sidebarLifecycleInFlightRef.current.delete(owner.profileId);
      if (isCurrentProfileOwner(owner)) setProxyBusy(false);
    }
  }, [isCurrentProfileOwner, refreshHomeProxyStatus, selectedProfileId]);

  const stopGame = useCallback(async () => {
    if (!backendBridge.available || !selectedProfileId) return;
    const owner = { ...selectedProfileOwnerRef.current };
    const ownerKey = profileOwnerKey(owner);
    if (globalNativeLifecycleInFlightRef.current || lifecycleInFlightProfilesRef.current.has(ownerKey) ||
        sidebarLifecycleInFlightRef.current.has(owner.profileId)) return;
    lifecycleInFlightProfilesRef.current.add(ownerKey);
    sidebarLifecycleInFlightRef.current.add(owner.profileId);
    setProxyBusy(true);
    setGameActionError("");
    try {
      // Original 0.3.17 Home Pt captures its selected profile ID before the
      // awaited status read, then Stops that same owner. A view switch must
      // retire stale UI replies, not silently abandon an already-clicked
      // Close or redirect it to the newly selected owner.
      const instance = await backendBridge.invoke(
        "profile_instance_status", { profileId: owner.profileId },
      );
      // F-04 OWN_DESIGN: recovery may be cancelling an official launcher
      // before an instanceId exists. The native Stop explicitly supports an
      // absent optional ID for this case and waits for exact-owner cleanup.
      // Skipping dispatch previously made a clickable Home Close a silent noop.
      await backendBridge.invoke(
        "profile_instance_stop",
        { profileId: owner.profileId, ...(instance?.instanceId ? { instanceId: instance.instanceId } : {}) },
        HOME_LIFECYCLE_TIMEOUT_MS,
      );
      await refreshHomeProxyStatus(owner);
    } catch (error) {
      if (isCurrentProfileOwner(owner)) {
        setGameActionError(error?.code || error?.message || String(error));
      }
    } finally {
      lifecycleInFlightProfilesRef.current.delete(ownerKey);
      sidebarLifecycleInFlightRef.current.delete(owner.profileId);
      if (isCurrentProfileOwner(owner)) setProxyBusy(false);
    }
  }, [isCurrentProfileOwner, refreshHomeProxyStatus, selectedProfileId]);

  const updateAndRestartGame = useCallback(async () => {
    if (!backendBridge.available || !selectedProfileId) return;
    const owner = { ...selectedProfileOwnerRef.current };
    const ownerKey = profileOwnerKey(owner);
    if (globalNativeLifecycleInFlightRef.current || sidebarLifecycleInFlightRef.current.size ||
        lifecycleInFlightProfilesRef.current.has(ownerKey)) return;
    lifecycleInFlightProfilesRef.current.add(ownerKey);
    globalNativeLifecycleInFlightRef.current = true;
    setProxyBusy(true);
    setGameActionError("");
    try {
      const result = await backendBridge.invoke(
        "profile_instances_update_and_restart",
        {},
        HOME_LIFECYCLE_TIMEOUT_MS,
      );
      if (!Array.isArray(result?.errors) || !Array.isArray(result?.restarted)) {
        throw new Error("GAME_REPAIR_INVALID_RESULT");
      }
      if (result.errors.length > 0) {
        if (isCurrentProfileOwner(owner))
          setGameActionError(result.errors[0]?.error || "GAME_CONNECTION_UPDATE_FAILED");
        return;
      }
      const repairStatus = await refreshHomeProxyStatus(owner);
      const selectedRestarted = result.restarted.includes(owner.profileId);
      // F-06 OWN_DESIGN: a game updated outside our journal can have a
      // damaged bridge but no saved repair session. The global legacy repair
      // command correctly cannot restore an absent journal. Rather than
      // acknowledging a silent noop, explicitly launch this captured owner
      // through the supported helper's closeUnmanaged repair/install path.
      // Native start checks the selected installation and foreign ownership.
      if (!selectedRestarted && repairStatus?.repairRequired !== true) {
        // No journal was repaired, and the status no longer exposes a repair
        // target (for example the game exited or the selected view changed).
        // An empty global result is not evidence that the advertised repair
        // actually happened. Never acknowledge this as successful.
        throw new Error("GAME_REPAIR_NOT_APPLIED");
      }
      if (repairStatus?.repairRequired === true && !selectedRestarted) {
        const started = await backendBridge.invoke(
          "profile_instance_start",
          { profileId: owner.profileId, closeUnmanaged: true },
          HOME_LIFECYCLE_TIMEOUT_MS,
        );
        if (started?.connectionState !== "connected")
          throw new Error("GAME_REPAIR_CONNECT_NOT_READY");
        await refreshHomeProxyStatus(owner);
      } else if (repairStatus?.repairRequired === true) {
        throw new Error("GAME_REPAIR_INCOMPLETE");
      }
    } catch (error) {
      if (isCurrentProfileOwner(owner)) {
        setGameActionError(error?.code || error?.message || String(error));
      }
    } finally {
      lifecycleInFlightProfilesRef.current.delete(ownerKey);
      globalNativeLifecycleInFlightRef.current = false;
      if (isCurrentProfileOwner(owner)) setProxyBusy(false);
    }
  }, [isCurrentProfileOwner, refreshHomeProxyStatus, selectedProfileId]);

  const requestServerJump = useCallback(async (serverId) => {
    const result = await mapApi.jumpServer(serverId);
    try {
      await refreshMapSummary();
    } catch {
      // The reference parent logs this refresh failure and still acknowledges the jump result.
    }
    return result;
  }, [refreshMapSummary]);

  const jumpServer = useCallback(async (serverId) => {
    if (!Number.isInteger(serverId) || serverId < 1 || serverId > 99999) {
      setServerJumpError(t("server.invalidId"));
      return;
    }
    if (!online) {
      setServerJumpError(t("status.gameDisconnected"));
      return;
    }
    if (mapRuntime.isReading) {
      setServerJumpError(t("server.stopScanFirst"));
      return;
    }
    setServerJumpBusy(serverId);
    setServerJumpError("");
    try {
      const result = await requestServerJump(serverId);
      if (result.changed) {
        const history = [result.serverId, ...serverHistoryRef.current.filter((value) => value !== result.serverId)].slice(0, 5);
        const persisted = await mapApi.setServerJumpHistory(history);
        serverHistoryRef.current = persisted;
        setServerHistory(persisted);
      }
      setServerTarget("");
      setServerJumpOpen(false);
    } catch {
      setServerJumpError(t("common.actionFailed"));
    } finally {
      setServerJumpBusy(0);
    }
  }, [mapRuntime.isReading, online, requestServerJump, t]);

  const serverJumpBlockedText = online
    ? mapRuntime.isReading ? t("server.stopScanFirst") : ""
    : t("status.gameDisconnected");
  const serverJumpBusyActive = serverJumpBusy > 0;
  const nativeProfileCallbacks = backendBridge.mode === "native" ? {
    onSelect: selectNativeProfile,
    onCreate: createNativeProfile,
    onRemove: removeNativeProfile,
    onSetEnabled: setNativeProfileEnabled,
    onReorder: reorderNativeProfiles,
    onUpdateNote: updateNativeProfileNote,
    readInstance: readNativeInstance,
    onStartProfile: startNativeProfile,
    onStopProfile: stopNativeProfile,
    onRestartAll: restartAllNativeProfiles,
  } : {};
  const profileCallbacks = profilePreview ? profilePreviewCallbacks : nativeProfileCallbacks;
  const effectiveProfileSwitchLoading = switchLoading || nativeProfileBusy || nativeProfileMutationBusy;

  const stateText = {
    connected: t("status.connected"),
    disconnected: t("status.disconnected"),
    stopped: t("setup.gameStopped"),
    preview: "Preview",
    unavailable: "Unavailable",
    checking: t("status.checking"),
  }[bridgeState] || t("status.checking");
  const pendingTasks = Number(runtimeStatus?.pending ?? 0);
  const pageProps = {
    mapApi,
    bridgeMode: backendBridge.mode,
    backendAvailable: backendBridge.available,
    online,
    currentServerId,
    scanState: mapRuntime,
    summary: mapSummary,
    onState: acknowledgeMapScan,
    onCounts: acknowledgeMapCounts,
    autoScanConfig,
    autoScanRunning,
    autoScanError: autoScanSaveError || autoScanActionError || autoScanRuntimeError,
    onAutoScanConfig: updateAutoScanConfig,
    onAutoScanRunNow: runAutoScanNow,
    previewState,
    showProfileFocus: showProfiles,
    focusGameOnProfileSelect,
    onFocusGameOnProfileSelectChange: updateProfileFocus,
    homeState: {
      rootResolved: gameRootStatus !== null,
      gameRootStatus,
      proxyStatus,
      online,
      gameRecoveryStatus,
      autoLaunchGame,
      autoReconnect: autoReconnectSnapshot.draft,
      busy: homeBusy,
      proxyBusy,
      gameLaunchBusy,
      gameRootError,
      gameActionError,
      production: backendBridge.available,
    },
    onAutoLaunchGameChange: updateAutoLaunch,
    onAutoReconnectChange: updateAutoReconnect,
    onGameRootSelect: selectGameRoot,
    onStartGame: startGame,
    onStopGame: stopGame,
    onUpdateAndRestart: updateAndRestartGame,
  };
  const pagePropsByRoute = {
    ...(showProfiles ? {
      automation: { profileId: selectedProfileId, activeCategory: automationCategory, onActiveCategoryChange: setAutomationCategory },
      "map-data": { activeTab: mapTab, onActiveTabChange: setMapTab },
    } : {}),
    march: {
      profileId: selectedProfileId,
      ...(showProfiles ? { activeTab: squadTab, onActiveTabChange: setSquadTab } : {}),
    },
  };

  return (
    <GameAssetImageProvider readImage={nativeAssetReader}><>
      <main className="app-shell" data-reference-version="0.3.17" data-ui-project="LWBridge.UI-0.3.17">
      <header className="top-bar">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            <span>LW</span>
          </div>
          <div className="brand-copy">
            <h1>lwbridge</h1>
            <TopVersion status={shellUpdateStatus} />
          </div>
        </div>

        <section className="status-strip" aria-label="Status">
          <div className={`status-card status-online${online ? " online" : ""}`}>
            <strong>
              <img src={online ? onlineDot : offlineDot} alt="" aria-hidden="true" />
              {stateText}
            </strong>
          </div>
          <div className="status-card">
            <span>{t("status.pendingTasks")}</span>
            <strong>{Number.isFinite(pendingTasks) ? pendingTasks : 0}</strong>
          </div>
        </section>

        <div className="top-actions">
          <button
            className="theme-toggle"
            type="button"
            title={t(theme === "dark" ? "theme.switchToLight" : "theme.switchToDark")}
            aria-label={t(theme === "dark" ? "theme.switchToLight" : "theme.switchToDark")}
            aria-pressed={theme === "dark"}
            onClick={() => toggleShellTheme(theme, setTheme, document, window, localStorage, flushSync)}
          >
            {theme === "dark" ? (
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <circle cx="12" cy="12" r="3.5" />
                <path d="M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.65 17.65l1.42 1.42M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.65 6.35l1.42-1.42" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M20.4 15.1A8.5 8.5 0 0 1 8.9 3.6 8.5 8.5 0 1 0 20.4 15.1Z" />
              </svg>
            )}
          </button>

          <label className="language-select">
            <span>{t("top.language")}</span>
            <select
              value={language}
              aria-label={t("top.language")}
              onChange={(event) => setLanguage(event.target.value)}
            >
              {LANGUAGES.map(({ code, name }) => <option value={code} key={code}>{name}</option>)}
            </select>
          </label>

          <div className="server-jump" ref={serverJumpRef}>
            <button
              className="top-action secondary"
              type="button"
              onClick={() => setServerJumpOpen((value) => !value)}
            >
              {currentServerId > 0 ? t("server.switchLabelWithId", { server: currentServerId }) : t("server.switchLabel")}
            </button>
            {serverJumpOpen ? (
              <section className="server-jump-popover" aria-label={t("server.dialogLabel")}>
                <div className="server-jump-heading">
                  <strong>{t("server.title")}</strong>
                  <span>{t("server.current", { server: currentServerId || "-" })}</span>
                </div>
                <div className="server-jump-form">
                  <input
                    inputMode="numeric"
                    min="1"
                    max="99999"
                    placeholder={t("server.targetPlaceholder")}
                    type="number"
                    value={serverTarget}
                    onChange={(event) => setServerTarget(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" && !serverJumpBusyActive && !serverJumpBlockedText) jumpServer(Number(serverTarget));
                    }}
                  />
                  <button className="top-action primary" type="button" disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(Number(serverTarget))}>
                    {serverJumpBusyActive ? t("server.switching", { server: serverJumpBusy }) : t("server.switchAction")}
                  </button>
                </div>
                {mapRuntime.homeServerId > 0 && currentServerId !== mapRuntime.homeServerId ? (
                  <div className="server-jump-home"><span>{t("server.home", { server: mapRuntime.homeServerId })}</span><button type="button" disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(mapRuntime.homeServerId)}>{t("server.returnHome")}</button></div>
                ) : null}
                {serverJumpBlockedText ? <p className="server-jump-error">{serverJumpBlockedText}</p> : null}
                {serverJumpError ? <p className="server-jump-error">{serverJumpError}</p> : null}
                {mapRuntime.seasonServerIds?.length ? <div className="server-jump-history"><span>{t("server.seasonServers")}</span><div>{mapRuntime.seasonServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {mapRuntime.truckMatchServerIds?.length ? <div className="server-jump-history"><span>{t("server.plunderableServers")}</span><div>{mapRuntime.truckMatchServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {serverHistory.length ? <div className="server-jump-history"><span>{t("server.recent")}</span><div>{serverHistory.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
              </section>
            ) : null}
          </div>

          <button
            className="top-action secondary"
            type="button"
            disabled={!backendBridge.available}
            onClick={refreshStatus}
          >
            {t("top.refreshStatus")}
          </button>
        </div>
      </header>

      <div className={`app-layout${showProfiles ? "" : " single-profile"}`}>
        {showProfiles ? <aside className="profile-sidebar"><ProfileSidebar
          state={shellProfiles}
          busy={nativeProfileBusy || nativeProfileMutationBusy}
          error={nativeProfileError}
          focusGameOnProfileSelect={focusGameOnProfileSelect}
          {...profileCallbacks}
        /></aside> : null}
        <nav className="side-nav" aria-label="Navigation">
          <div className="nav-heading">
            <strong>{t("nav.title")}</strong>
          </div>
          {routes.map((route) => (
            <button
              key={route.key}
              type="button"
              className={route.key === activeRoute ? "active" : ""}
              aria-current={route.key === activeRoute ? "page" : undefined}
              onMouseEnter={() => preloadRoute(route.key)}
              onFocus={() => preloadRoute(route.key)}
              onClick={() => selectRoute(route.key)}
            >
              <span className="nav-icon" aria-hidden="true">
                <NavIcon name={route.key} />
              </span>
              <span className="nav-label">{t(route.labelKey)}</span>
            </button>
          ))}
        </nav>

        <section className="main-view">
          <Suspense fallback={<div className="panel"><span className="muted">{t("common.processing")}</span></div>}>
            <div className="profile-view-context">
              <ShellConfigSaveErrors states={shellFlagStates || (previewFlagStates.length ? previewFlagStates : [autoWeekendShieldStore, autoAttackShieldStore, autoReconnectStore, autoClosePopupStore])} />
              {effectiveProfileSwitchLoading ? <ProfileSwitchState loading /> : <RetainedPages
                activeRoute={activeRoute}
                visitedRoutes={visitedRoutes}
                selectedProfileId={selectedProfileId}
                pageProps={pageProps}
                pagePropsByRoute={pagePropsByRoute}
              />}
            </div>
          </Suspense>
        </section>
      </div>
    </main>
    <AppExitPrompt subscribeCloseRequests={subscribeCloseRequests} confirmExit={confirmExit} />
    {previewExitCount !== null ? <AppExitDialog instanceCount={previewExitCount} busy={previewState === "shell-exit-busy"}
      onCancel={() => setPreviewExitCount(null)} /> : null}
    </></GameAssetImageProvider>
  );
}
