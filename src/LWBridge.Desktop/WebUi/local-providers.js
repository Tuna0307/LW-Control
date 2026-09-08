// Local login-free context. Fixture mode stays browser-local; normal desktop
// mode persists operational preferences through the native backend.
function Xt({children}) {
    const live = window.LWBridgePreview.mode === 'live';
    const [autoLaunchGame, setAutoLaunch] = j.useState(() => live
        ? window.__LWBridgeBootstrap?.autoLaunchGame !== false
        : localStorage.getItem('lwbridge.autoLaunchGame') !== 'false');
    const [gameLaunchBusy, setGameLaunchBusy] = j.useState(false);
    const [profileLaunchErrors, setProfileLaunchErrors] = j.useState([]);
    j.useEffect(() => {
        if (!live || !autoLaunchGame) return;
        setGameLaunchBusy(true);
        window.LWBridgePreview.invoke('profile_instances_reconcile', {autoLaunchAll: true})
            .catch(error => setProfileLaunchErrors([String(error?.message || error)]))
            .finally(() => setGameLaunchBusy(false));
    }, []);
    const value = {
        state: {phase: 'local', accessRole: 'normal', expiresAt: null},
        busy: false, gameLaunchBusy, profileLaunchErrors,
        autoLaunchGame,
        setAutoLaunchGame: value => {
            const enabled = !!value;
            const previous = autoLaunchGame;
            setAutoLaunch(enabled);
            if (live) {
                window.LWBridgePreview.invoke('local_config_set', {autoLaunchGame: enabled})
                    .catch(error => {
                        setAutoLaunch(previous);
                        setProfileLaunchErrors([String(error?.message || error)]);
                    });
            } else {
                localStorage.setItem('lwbridge.autoLaunchGame', String(enabled));
            }
        },
        clearActionError() {}, clearProfileLaunchErrors() { setProfileLaunchErrors([]); }
    };
    return M.jsx(Jt.Provider, {value, children});
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
