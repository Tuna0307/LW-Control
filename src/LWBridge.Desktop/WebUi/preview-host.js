/* Adapter for the recovered frontend.
 * Normal desktop launches use the native WebView2 RPC bridge.
 * Capture/browser runs stay on the deterministic fixture transport and never
 * fall through to native process/game operations.
 */
(() => {
    'use strict';
    const query = new URLSearchParams(location.search);
    const bootstrap = window.__LWBridgeBootstrap || {};
    const nativeWebView = window.chrome?.webview;
    const liveRequested = bootstrap.mode === 'live';
    const live = liveRequested && !!nativeWebView;
    const views = ['overview', 'automation', 'map-data', 'march', 'city-layout', 'hotkeys', 'mini-games', 'advanced', 'settings'];
    const language = query.get('language');
    if (language) localStorage.setItem('lwbridge.language', language);
    const theme = query.get('theme');
    if (theme) localStorage.setItem('lwbridge.theme', theme);
    const clone = value => value == null ? value : structuredClone(value);
    const types = ['city', 'resource', 'monster', 'truck', 'railway', 'dispatch', 'ghost', 'treasure'];
    const calls = [];
    const failures = [];
    const listeners = new Map();
    const pending = new Map();

    const fixtureScanState = {
        serverId: 0, serverIdSource: 'none', scanRunId: '', isReading: false,
        phase: 'idle', selectedTypes: types, totalBlocks: 0, readBlocks: 0,
        unreadBlocks: 0, failedBlocks: 0, inflightBlocks: 0, scanMode: 'normal',
        concurrency: 8, retryCount: 2, scanRate: 0, progressPercent: 0,
        nativeCaptureReady: false, nativePendingRecords: 0, nativeDroppedRecords: 0,
        homeServerId: 0, seasonServerIds: [], truckMatchServerIds: []
    };
    const fixtureCounts = Object.fromEntries(types.map(key => [key, 0]));
    const fixtureDefaults = {
        status: {xluaOnline: false, pending: 0, config: {auto_weekend_shield: true, auto_attack_shield: true, auto_force_update_reload: false, auto_close_popup: false, tasks: {}}},
        hotkeys: Object.fromEntries(['attack', 'recall', 'shieldOverlay', 'shieldUse', 'equipment', 'randomRelocate', 'allianceRelocate', 'frontlineReinforce', 'attackMarchSpeedupItem', 'attackMarchSpeedupDiamond'].map(key => [key, false])),
        metrics: {showFps: false, showPing: false},
        equipment: {equipmentPresets: [], initialEquipmentConfig: null},
        vip: {selectedSkinId: null, autoApplyOnStart: false, favoriteSkinIds: []}
    };
    let fixtureState;
    try { fixtureState = {...clone(fixtureDefaults), ...JSON.parse(localStorage.getItem('lwbridge.preview.config') || '{}')}; }
    catch { fixtureState = clone(fixtureDefaults); }
    const saveFixture = () => localStorage.setItem('lwbridge.preview.config', JSON.stringify(fixtureState));
    const fixtureUpdate = {phase: 'idle', currentVersion: '0.3.1', latestVersion: null, releaseNotes: '', progress: null, message: null, nextManualCheckAt: null, downloadDirectory: ''};
    const fixtureReads = {
        get_status: () => fixtureState.status,
        proxy_status: () => ({gameRunning: false, repairRequired: false}),
        game_root_status: () => ({valid: query.get('fixture') !== 'missing-root', path: '', source: 'preview'}),
        game_recovery_status: () => ({state: 'idle', error: null}),
        automation_status: () => ({tasks: {}}),
        resource_automation_status: () => ({tasks: Object.fromEntries(['buildingResources', 'armedTruckReward'].map(key => [key, {enabled: false, intervalMinutes: 60}]))}),
        map_scan_status: () => fixtureScanState,
        map_summary: () => ({serverId: 0, counts: fixtureCounts, scanState: fixtureScanState}),
        map_data_options: () => ({serverId: 0, alliances: [], names: {}, dispatchLevels: [], counts: fixtureCounts, rewardItems: {}, treasureTypes: [], noAllianceCount: 0, scanProgress: null}),
        map_search: () => ({rows: [], total: 0}),
        map_plunder_jobs_list: () => ({dispatchJobs: [], truckJobs: []}),
        map_treasure_claim_status: () => ({running: false, jobs: []}),
        hotkey_config_get: () => fixtureState.hotkeys,
        visual_metrics_config_get: () => fixtureState.metrics,
        equipment_config_get: () => fixtureState.equipment,
        squad_list: () => ({squads: []}),
        monster_catalog_options: () => ({options: []}),
        dispatch_assist_state: () => ({tasks: [], jobs: []}),
        city_layout_draft_get: () => null,
        city_layout_apply_status: () => ({state: 'idle'}),
        vip18_base_config_get: () => fixtureState.vip,
        vip18_base_list: () => ({items: []}),
        lastwar_localize: () => ({}),
        update_status: () => fixtureUpdate,
        profile_instance_status: () => null
    };

    const syntheticProfiles = Array.from({length: query.get('profiles') === '2' ? 2 : 1}, (_, i) => ({
        id: `local-${i + 1}`, displayName: `Profile ${i + 1}`, roleName: '', serverId: 0,
        enabled: true, note: '', connectionState: 'offline'
    }));
    const profiles = liveRequested && bootstrap.profiles
        ? clone(bootstrap.profiles)
        : {selectedProfileId: syntheticProfiles[0].id, profiles: syntheticProfiles};

    function recordCall(command) {
        calls.push(command);
        if (calls.length > 300) calls.shift();
    }

    function dispatchEvent(event, payload) {
        for (const callback of listeners.get(event) || []) {
            try { callback(payload); }
            catch (error) { failures.push(String(error)); }
        }
    }

    if (live) {
        nativeWebView.addEventListener('message', event => {
            let message = event.data;
            if (typeof message === 'string') {
                try { message = JSON.parse(message); }
                catch { return; }
            }
            if (!message || message.sessionId !== bootstrap.sessionId) return;
            if (message.kind === 'response') {
                const entry = pending.get(message.id);
                if (!entry) return;
                pending.delete(message.id);
                clearTimeout(entry.timer);
                if (message.ok) entry.resolve(message.result);
                else {
                    const error = new Error(message.error?.message || 'Native command failed.');
                    error.code = message.error?.code || 'NATIVE_COMMAND_FAILED';
                    error.details = message.error?.details;
                    entry.reject(error);
                }
            } else if (message.kind === 'event') {
                dispatchEvent(message.event, message.payload);
            }
        });
    }

    function liveInvoke(command, payload) {
        if (!nativeWebView) {
            const error = new Error('Native WebView transport is unavailable in live mode.');
            error.code = 'NATIVE_TRANSPORT_MISSING';
            return Promise.reject(error);
        }
        const id = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
        const timeoutMs = command === 'profile_instance_start' || command === 'profile_instances_reconcile' ||
            command === 'profile_instances_update_and_restart'
            ? 360000 : 30000;
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => {
                pending.delete(id);
                nativeWebView.postMessage({kind: 'cancel', sessionId: bootstrap.sessionId, id});
                const error = new Error(`Native command timed out: ${command}`);
                error.code = 'COMMAND_TIMEOUT';
                reject(error);
            }, timeoutMs);
            pending.set(id, {resolve, reject, timer});
            nativeWebView.postMessage({kind: 'invoke', sessionId: bootstrap.sessionId, id, command, payload});
        });
    }

    async function fixtureInvoke(command, payload = {}) {
        if (Object.hasOwn(fixtureReads, command)) return clone(fixtureReads[command]());
        if (command === 'append_log' || command === 'set_window_theme') return null;
        if (command === 'server_jump_history_import' || command === 'server_jump_history_set') return clone(payload.history || []);
        const configKey = {hotkey_config_save: 'hotkeys', visual_metrics_config_save: 'metrics', equipment_config_save: 'equipment', vip18_base_config_save: 'vip'}[command];
        if (configKey) { fixtureState[configKey] = clone(payload); saveFixture(); return clone(fixtureState[configKey]); }
        if (command === 'set_automation') {
            const key = {autoWeekendShield: 'auto_weekend_shield', autoAttackShield: 'auto_attack_shield', autoForceUpdateReload: 'auto_force_update_reload', autoClosePopup: 'auto_close_popup'}[payload.name];
            if (key) { fixtureState.status.config[key] = !!payload.enabled; saveFixture(); return {enabled: !!payload.enabled}; }
        }
        if (command === 'automation_configure') {
            fixtureState.status.config.tasks[payload.task] = clone(payload.config); saveFixture(); return clone(payload.config);
        }
        failures.push(command);
        throw new Error('UI preview only — game features are not connected yet.');
    }

    window.LWBridgePreview = {
        mode: liveRequested ? 'live' : 'fixture',
        view: views.includes(query.get('view')) ? query.get('view') : 'overview',
        profiles,
        calls,
        failures,
        listen(event, callback) {
            if (!listeners.has(event)) {
                listeners.set(event, new Set());
                if (live) nativeWebView.postMessage({kind: 'listen', sessionId: bootstrap.sessionId, event});
            }
            listeners.get(event).add(callback);
            return () => {
                const set = listeners.get(event);
                set?.delete(callback);
                if (set?.size === 0) {
                    listeners.delete(event);
                    if (live) nativeWebView.postMessage({kind: 'unlisten', sessionId: bootstrap.sessionId, event});
                }
            };
        },
        async invoke(command, payload = {}) {
            recordCall(command);
            try {
                return liveRequested ? await liveInvoke(command, payload) : await fixtureInvoke(command, payload);
            } catch (error) {
                if (liveRequested) failures.push(`${command}: ${error.code || ''} ${error.message}`.trim());
                throw error;
            }
        }
    };
    addEventListener('error', event => failures.push(String(event.error || event.message)));
    addEventListener('unhandledrejection', event => failures.push(String(event.reason)));
})();
