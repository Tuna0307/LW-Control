// Local login-free context. Fixture mode stays browser-local; normal desktop
// mode persists operational preferences through the native backend.
function Xt({children}) {
    const live = window.LWBridgePreview.mode === 'live';
    const [autoLaunchGame, setAutoLaunch] = j.useState(() => live
        ? window.__LWBridgeBootstrap?.autoLaunchGame !== false
        : localStorage.getItem('lwbridge.autoLaunchGame') !== 'false');
    const autoLaunchCommitted = j.useRef(autoLaunchGame);
    const autoLaunchSaveRevision = j.useRef(0);
    const autoLaunchSaveChain = j.useRef(Promise.resolve());
    const [gameLaunchBusy, setGameLaunchBusy] = j.useState(false);
    const [profileLaunchErrors, setProfileLaunchErrors] = j.useState([]);
    const asProfileError = error => ({
        profileId: window.LWBridgePreview.profiles.selectedProfileId,
        error: error?.code || 'NATIVE_COMMAND_FAILED',
        message: String(error?.message || error?.code || 'NATIVE_COMMAND_FAILED')
    });
    j.useEffect(() => {
        if (!live || !autoLaunchGame) return;
        setGameLaunchBusy(true);
        window.LWBridgePreview.invoke('profile_instances_reconcile', {autoLaunchAll: true})
            .catch(error => setProfileLaunchErrors([asProfileError(error)]))
            .finally(() => setGameLaunchBusy(false));
    }, []);
    const value = {
        state: {phase: 'local', accessRole: 'normal', expiresAt: null},
        busy: false, gameLaunchBusy, profileLaunchErrors,
        autoLaunchGame,
        setAutoLaunchGame: value => {
            const enabled = !!value;
            const revision = ++autoLaunchSaveRevision.current;
            setProfileLaunchErrors([]);
            setAutoLaunch(enabled);
            if (live) {
                const save = autoLaunchSaveChain.current
                    .catch(() => void 0)
                    .then(() => window.LWBridgePreview.invoke('local_config_set', {autoLaunchGame: enabled}));
                autoLaunchSaveChain.current = save;
                save
                    .then(result => {
                        const committed = typeof result?.autoLaunchGame === 'boolean'
                            ? result.autoLaunchGame
                            : enabled;
                        autoLaunchCommitted.current = committed;
                        if (autoLaunchSaveRevision.current === revision) {
                            setAutoLaunch(committed);
                            setProfileLaunchErrors([]);
                        }
                    })
                    .catch(error => {
                        if (autoLaunchSaveRevision.current === revision) {
                            setAutoLaunch(autoLaunchCommitted.current);
                            setProfileLaunchErrors([asProfileError(error)]);
                        }
                    });
            } else {
                localStorage.setItem('lwbridge.autoLaunchGame', String(enabled));
                autoLaunchCommitted.current = enabled;
            }
        },
        clearActionError() {}, clearProfileLaunchErrors() { setProfileLaunchErrors([]); }
    };
    const visibleSaveError = profileLaunchErrors
        .map(error => error.message || error.error)
        .filter(Boolean)
        .join('\n');
    return M.jsx(Jt.Provider, {
        value,
        children: M.jsxs(M.Fragment, {children: [
            visibleSaveError ? M.jsx('div', {
                className: 'profile-error profile-save-error',
                role: 'alert',
                children: visibleSaveError
            }) : null,
            children
        ]})
    });
}

function sn({children}) {
    const [state, setState] = j.useState(window.LWBridgePreview.profiles);
    const [busy, setBusy] = j.useState(false);
    const [error, setError] = j.useState('');
    Le(state.selectedProfileId);
    const refresh = async () => {
        if (window.LWBridgePreview.mode !== 'live') return state;
        setBusy(true);
        setError('');
        try {
            const next = await window.LWBridgePreview.invoke('profile_list');
            setState(next);
            return next;
        } catch (reason) {
            setError(String(reason?.message || reason));
            throw reason;
        } finally {
            setBusy(false);
        }
    };
    j.useEffect(() => { refresh().catch(() => void 0); }, []);
    const value = {
        state, entitlement: {maxProfiles: state.profiles.length},
        selectedProfileId: state.selectedProfileId, busy, error,
        refresh,
        select: async id => setState(s => ({...s, selectedProfileId: id})),
        updateNote: async (id, note) => setState(s => ({...s, profiles: s.profiles.map(p => p.id === id ? {...p, note} : p)})),
        reorder: async ids => setState(s => ({...s, profiles: ids.map(id => s.profiles.find(p => p.id === id))})),
        create: () => window.LWBridgePreview.invoke('profile_create'),
        remove: () => window.LWBridgePreview.invoke('profile_delete')
    };
    return M.jsx(on.Provider, {value, children});
}
