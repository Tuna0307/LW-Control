"""Isolated Lua 5.3 tests of the actual FEATURE018 bridge request admission.

Memory-backed mailbox and fake game objects only. No installed scripts, live
game, account action, bridge connection or persisted runtime files are used.
"""
from __future__ import annotations

import json
_REPO = __import__('pathlib').Path(__file__).resolve().parents[2]
_SCRIPTS = _REPO / 'src' / 'LWBridge.GameScripts'
_RESEARCH = _REPO / 'tools' / 'research'
__import__('sys').path.append(str(_SCRIPTS))
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(_REPO / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime  # noqa: E402


def make_bridge():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        clock = 1000
        request_text, response_text = '', ''
        calls = 0
        owner_gate_calls = 0
        current_uid = '3133546747002212'
        current_server = 2212
        overview_uid = current_uid
        overview_server = current_server
        current_session = 'session'
        native_server_source = nil
        mutate_uid_on_source = false
        allow_lease = true
        fail_after_send = false
        switch_session_after_send = false
        switch_uid_after_send = false
        lose_lease_after_send = false
        os.time = function() return clock end
        io.open = function(path, mode)
            if path:find('feature018-request.txt', 1, true) and mode == 'rb' then
                return {
                    read = function(_, length) return request_text:sub(1, length) end,
                    close = function() end,
                }
            end
            if path:find('feature018-response.json', 1, true) and mode == 'wb' then
                return {
                    write = function(_, value) response_text = value end,
                    close = function() end,
                }
            end
            return nil
        end
        LuaEntry = {Player = {
            GetSourceServerId = function()
                if mutate_uid_on_source then
                    mutate_uid_on_source = false
                    current_uid = '3133546747009999'
                end
                return native_server_source or current_server
            end,
            GetSelfServerId = function() return current_server end,
            GetCurServerId = function() return current_server end,
        }}
        setmetatable(LuaEntry.Player, {
            __index = function(_, key) if key == 'uid' then return current_uid end end,
        })
        LWBridgeOverviewBridge = {
            IsAuthorizedFeature018Request = function(values, native_server)
                owner_gate_calls = owner_gate_calls + 1
                return allow_lease and values.authorizedUid == overview_uid and
                    values.launchSessionId == current_session and
                    tostring(native_server) == tostring(overview_server)
            end,
        }
    """)
    module = lua.execute((_SCRIPTS / "feature018_game_bridge.lua").read_text(encoding="utf-8"))
    lua.globals().bridge = module
    assert module.RegisterHandler("nativeTest", lua.eval("""
        function(values)
            calls = calls + 1
            if fail_after_send then current_server = current_server + 1 end
            if switch_session_after_send then current_session = 'new_owner_session' end
            if switch_uid_after_send then current_uid = '3133546747007777' end
            if lose_lease_after_send then allow_lease = false end
            return {state='confirmed', data={ownerUid=current_uid, server=current_server}}
        end
    """)) is True
    return lua, module


def issue(lua, module, request_id: str, *, uid: str | None = None,
          session: str = "session", issued_at: int | None = None,
          command: str = "nativeTest", game_pid: str = "1234",
          extra: str = "") -> bool:
    uid = uid if uid is not None else str(lua.globals().current_uid)
    issued_at = issued_at if issued_at is not None else int(lua.globals().clock) * 1000
    lua.globals().request_text = "\n".join((
        "schema=1", f"requestId={request_id}", "profileId=profile",
        f"launchSessionId={session}", "challenge=challenge",
        f"gamePid={game_pid}", f"command={command}", f"issuedAt={issued_at}",
        f"authorizedUid={uid}", extra,
    ))
    return bool(module.Pump())


def test_bridge_account_and_replay() -> None:
    lua, module = make_bridge()
    v = lua.globals()
    assert issue(lua, module, "A")
    first = json.loads(v.response_text)
    assert first["state"] == "confirmed" and first["data"]["server"] == 2212
    assert v.calls == 1
    assert v.owner_gate_calls == 2, "new request must authorize before and after its handler"
    assert not issue(lua, module, "A") and v.calls == 1
    assert v.owner_gate_calls == 2, "exact consumed envelope must avoid repeated native authorization work"
    assert issue(lua, module, "B") and v.calls == 2
    prior = v.response_text
    assert not issue(lua, module, "A") and v.calls == 2
    assert v.response_text == prior, "stale A was processed after B"
    assert not issue(lua, module, "A", game_pid="0001234") and v.calls == 2

    # Another ordinary logged-in account works, provided Overview and LuaEntry agree.
    v.current_uid = "3133546747002289"
    v.overview_uid = v.current_uid
    v.current_server = 2289
    v.overview_server = 2289
    assert issue(lua, module, "C") and v.calls == 3
    assert json.loads(v.response_text)["data"]["server"] == 2289
    assert not issue(lua, module, "D", uid="3133546747002212")
    assert not issue(lua, module, "D", uid="")
    assert v.calls == 3

    # An overview/native server split is never allowed into a producer.
    v.overview_server = 2212
    assert not issue(lua, module, "D") and v.calls == 3
    v.overview_server = 2289
    v.native_server_source = 2212
    assert not issue(lua, module, "D") and v.calls == 3
    v.native_server_source = None
    v.mutate_uid_on_source = True
    assert not issue(lua, module, "D") and v.calls == 3, "getter-side UID switch admitted"
    v.current_uid = "3133546747002289"
    v.allow_lease = False
    assert not issue(lua, module, "D") and v.calls == 3
    v.allow_lease = True
    assert issue(lua, module, "D") and v.calls == 4

    v.fail_after_send = True
    assert issue(lua, module, "E") and v.calls == 5
    failed = json.loads(v.response_text)
    assert failed["state"] == "failed" and failed["errorCode"] == "GAME_ACCOUNT_CHANGED"
    assert "data" not in failed or failed["data"] is None
    v.fail_after_send = False
    v.overview_server = v.current_server
    assert issue(lua, module, "F") and v.calls == 6
    assert json.loads(v.response_text)["data"]["server"] == 2290

    v.switch_uid_after_send = True
    assert issue(lua, module, "G") and v.calls == 7
    assert json.loads(v.response_text)["errorCode"] == "GAME_ACCOUNT_CHANGED"
    v.switch_uid_after_send = False
    v.current_uid = v.overview_uid
    v.switch_session_after_send = True
    assert issue(lua, module, "H") and v.calls == 8
    assert json.loads(v.response_text)["errorCode"] == "GAME_ACCOUNT_CHANGED"
    v.switch_session_after_send = False
    v.current_session = "session"

    # The exact request is consumed even if the host lease disappears while
    # the handler runs. Restoring that lease must not replay uncertain work.
    v.lose_lease_after_send = True
    assert issue(lua, module, "I") and v.calls == 9
    assert json.loads(v.response_text)["errorCode"] == "GAME_ACCOUNT_CHANGED"
    v.lose_lease_after_send = False
    v.allow_lease = True
    stale_response = v.response_text
    assert not issue(lua, module, "I") and v.calls == 9
    assert v.response_text == stale_response
    assert issue(lua, module, "J") and v.calls == 10
    assert json.loads(v.response_text)["state"] == "confirmed"

    # A host session rotation invalidates the prior session request.
    v.current_session = "next_session"
    assert not issue(lua, module, "new", session="session")
    assert issue(lua, module, "A", session="next_session") and v.calls == 11
    assert not issue(lua, module, "A", session="next_session")
    assert not issue(lua, module, "old", session="next_session", issued_at=1)
    assert v.calls == 11
    assert issue(lua, module, "notFound", session="next_session", command="missing")
    missing = json.loads(v.response_text)
    assert missing["state"] == "unsupported" and missing["errorCode"] == "GAME_METHOD_UNAVAILABLE"
    assert v.calls == 11


def test_overview_gate() -> None:
    source = (_SCRIPTS / "current_overview_bridge.lua").read_text(encoding="utf-8")
    start = source.index("local function feature018_current_account()")
    end = source.index("-- Registration is deliberately lazy.", start)
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        M = {}
        clock = 1000
        os.time = function() return clock end
        active = {profileId='profile', sessionId='session',
                  challenge='challenge', gamePid=1234}
        control = {profileId='profile', sessionId='session',
                   challenge='challenge', gamePid=1234}
        game = {observed=true, loggedIn=true, connected=true, connecting=false,
                gameUid='3133546747002212', serverId=2212}
        fresh = true
        observe_game_connection = function() return game end
        lease_is_fresh = function() return fresh end
        read_control = function() return control end
    """)
    bridge = lua.execute(source[start:end] + "\nreturn M")
    v = lua.globals()

    def allowed(uid: str = "3133546747002212", server: int | None = 2212) -> bool:
        req = lua.table_from(dict(profileId="profile", launchSessionId="session",
                                  challenge="challenge", gamePid="1234", authorizedUid=uid))
        return bridge.IsAuthorizedFeature018Request(req, server) is True

    assert allowed()
    assert not allowed("3133546747002289")
    assert not allowed(server=2289)
    v.game["gameUid"] = "3133546747002289"
    v.game["serverId"] = 2289
    assert allowed("3133546747002289", 2289)
    assert not allowed("3133546747002289", 2212)
    assert not allowed()
    v.game["connecting"] = True
    assert not allowed("3133546747002289", 2289)
    v.game["connecting"] = False
    v.game["loggedIn"] = False
    assert not allowed("3133546747002289", 2289)
    v.game["loggedIn"] = True
    v.control["challenge"] = "rotated"
    assert not allowed("3133546747002289", 2289), "rotated host control admitted stale owner"
    v.control["challenge"] = "challenge"
    v.fresh = False
    assert not allowed("3133546747002289", 2289)
    v.fresh = True
    assert allowed("3133546747002289", 2289)
    assert bridge.Feature018OwnerToken().endswith(":3133546747002289:2289")
    v.control["sessionId"] = "other"
    assert bridge.Feature018OwnerToken() is None, "stale control could own native wrappers"


def test_native_game_connection_observation() -> None:
    source = (_SCRIPTS / "current_overview_bridge.lua").read_text(encoding="utf-8")
    start = source.index("local function observe_game_connection()")
    end = source.index("local function pipe_mailbox_path(", start)
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        read_index = 0
        unstable = false
        native_uid = '3133546747002212'
        native_server = 2212
        safe_get = function(value, key) return value and value[key] end
        reflected_value = function() return nil end
        call = function(value, method)
            if value==nil or type(value[method])~='function' then return false,nil end
            return true,value[method](value)
        end
        GameEntry = {
            Network={Logined=true, IsConnected=true, IsConnecting=false},
            Data={Player={
                GetUid=function()
                    read_index=read_index+1
                    if unstable and read_index % 2 == 0 then
                        return '3133546747002289'
                    end
                    return native_uid
                end,
                GetCurServerId=function() return native_server end,
            }},
        }
    """)
    observe = lua.execute(source[start:end] + "\nreturn observe_game_connection")
    v = lua.globals()
    assert observe()["gameUid"] == "3133546747002212"
    v.native_uid = "3133546747002289"
    v.native_server = 2289
    assert observe()["gameUid"] == "3133546747002289"
    v.native_uid = "3133546747002212"
    v.unstable = True
    sample = observe()
    assert sample["observed"] is False, (
        f"mixed GetUid samples were published as owner: {sample['observed']}, "
        f"uid={sample['gameUid']}, reads={v.read_index}"
    )


def test_visual_metrics_native_sources() -> None:
    source = (_SCRIPTS / "current_overview_bridge.lua").read_text(encoding="utf-8")
    assert (
        'data.renderPath = (visual_metrics_show_fps or visual_metrics_show_ping)' in source
    ), "visual metrics render path must remain a string when FPS alone is enabled"
    start = source.index("local function sample_visual_fps()")
    end = source.index("local function pump_visual_metrics(", start)
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        proxy = {
          ping = 37,
          GetPing = function(self) return self.ping end,
        }
        CS = {
          GameEntry = { Network = { proxy = proxy } },
          UnityEngine = { Time = { smoothDeltaTime = 0.02, unscaledDeltaTime = 0.025 } },
        }
        safe_get = function(value, key) return value and value[key] end
        reflected_value = function(value, key)
          if key == 'm_proxy' then return value.proxy end
          if key == '_ping' then return value.ping end
          return nil
        end
        reflected_type_name = function(value)
          if value == proxy then return 'GameKit.Base.NetRawProxy' end
          return 'Other'
        end
        call = function(value, method)
          local fn = value and value[method]
          if type(fn) ~= 'function' then return false,nil end
          local ok,result = pcall(fn,value)
          return ok,result
        end
    """)
    fps, ping = lua.execute(
        source[start:end] +
        "\nreturn sample_visual_fps, sample_visual_ping_milliseconds"
    )
    assert round(fps(), 3) == 50.0, "visual FPS did not use Unity frame timing"
    ping_ms, ping_source = ping()
    assert ping_ms == 37 and ping_source == "GameKit.Base.NetRawProxy.GetPing"
    lua.execute("proxy.GetPing=nil; proxy.ping=41")
    ping_ms, ping_source = ping()
    assert ping_ms == 41 and ping_source == "GameKit.Base.NetRawProxy._ping"
    lua.execute("reflected_type_name=function() return 'Other' end")
    assert ping()[0] is None, "non-NetRawProxy transport was accepted as game ping"

    wrapper = (_SCRIPTS / "run_overview_bridge.py").read_text(encoding="utf-8")
    assert (
        'feature.RegisterHandler("feature018_visual_metrics_configure"' in wrapper
        and "bridge.ConfigureVisualMetrics(fields)" in wrapper
    ), "packaged bridge did not register the authenticated visual-metrics command"


if __name__ == "__main__":
    lua, module = make_bridge()
    module.RegisterHandler('city_layout_apply_status', lua.eval("""function()
        calls=calls+1; current_uid=nil
        return {state='confirmed',data={completed=true}}
    end"""))
    assert issue(lua, module, 'city-pause', command='city_layout_apply_status')
    assert lua.globals().response_text == '' and lua.globals().calls == 1
    assert not module.Pump() and lua.globals().calls == 1
    lua.execute("current_uid='3133546747002212'")
    assert module.Pump() and lua.globals().calls == 1
    assert json.loads(lua.globals().response_text)['data']['completed'] is True
    test_bridge_account_and_replay()
    test_overview_gate()
    test_native_game_connection_observation()
    test_visual_metrics_native_sources()
    print("FEATURE018_GAME_BRIDGE_LUA53_PASS")
