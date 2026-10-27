"""Exact-source private experiment; never changes default product bytes."""
from __future__ import annotations


def instrument_native_reply(body: bytes, *, camera_free_direct: bool = False) -> bytes:
    text = body.decode("utf-8").replace("\r\n", "\n")

    def once(old: str, new: str) -> None:
        nonlocal text
        if text.count(old) != 1:
            raise ValueError("native reply experiment source anchor drift: " + old[:60])
        text = text.replace(old, new, 1)

    once('local M = { VERSION = "lwbridge-live-resource-probe-2" }\n', '''local M = { VERSION = "lwbridge-live-resource-probe-2" }
function M._map013Key(r, lod)
    return table.concat({r.requestId,r.profileId,r.launchSessionId,tostring(r.gamePid),
        tostring(r.serverId),"0",tostring(r.targetTileX),tostring(r.targetTileY),tostring(lod or 0)},"|")
end
function M._map013Load()
    if M._map013Readers then return true end
    local ok,readers=pcall(function()
        local path=os.getenv("LWBRIDGE_MAP013_SAMPLER_PATH")
        assert(type(path)=="string" and path~="", "missing owned sampler")
        local a=CS.System.Reflection.Assembly.LoadFrom(path)
        local t=a:GetType("LWBridge.MapTrace.Sampler",true)
        return {begin=t:GetField("Begin",24):GetValue(nil),
            match=t:GetField("Match",24):GetValue(nil),retire=t:GetField("Retire",24):GetValue(nil),
            bindWorld=t:GetField("BindWorld",24):GetValue(nil)}
    end)
    if ok then M._map013Readers=readers end
    return ok
end
function M._map013Begin(r,lod)
    if not M._map013Load() then return false end
    r._map013Key=M._map013Key(r,lod)
    local ok,value=pcall(function()return M._map013Readers.begin(CS.GameEntry.Network,r._map013Key)end)
    if not ok or value~=true then return false end
    local bound,result=pcall(function()return M._map013Readers.bindWorld(CS.SceneManager.World,r._map013Key)end)
    return bound and result==true
end
function M._map013Match(r)
    if not M._map013Readers or not r._map013Key then return "unarmed" end
    local ok,value=pcall(function()return M._map013Readers.match(CS.GameEntry.Network,r._map013Key)end)
    return ok and tostring(value) or "unavailable"
end
function M._map013Retire(r)
    if M._map013Readers and r and r._map013Key then
        pcall(function()M._map013Readers.retire(r._map013Key)end)
    end
end
''')
    once('        local request_method = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"',
         '''        if not M._map013Begin(request,server_lod) then
            fail_bulk_aoi(request,"map013_native_reply_window_unavailable",nil,point_manager)
            return true
        end
        local request_method = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"''')
    once('    if request_completed then\n        local request = bulk_aoi_request',
         '''    details.map013NativeReplyState = M._map013Match(bulk_aoi_request)
    request_completed = details.map013NativeReplyState == "matched" and
        details.responseFlagsTransitioned == true and
        reflected_value(point_manager, "_splitLastAOIRequest") ~= true
    if request_completed then
        M._map013Retire(bulk_aoi_request)
        local request = bulk_aoi_request''')
    once('    local lookup = bulk_selected_lookup(details.requestedIndices)\n',
         '''    details.map013NativeReplyState=M._map013Match(bulk_aoi_request)
    if details.map013NativeReplyState~="matched" then
        if runtime_clock()-bulk_aoi_started_at>=10 then
            fail_bulk_aoi(bulk_aoi_request,"map013_native_reply_or_cache_timeout",details,point_manager)
        end
        return true
    end
    local lookup = bulk_selected_lookup(details.requestedIndices)
''')
    once('local function fail_bulk_aoi(request, error_text, details, point_manager)\n',
         'local function fail_bulk_aoi(request, error_text, details, point_manager)\n    M._map013Retire(request)\n')
    once('        requestMode = request.requestMode,\n',
         '        requestMode = request.requestMode,\n        map013NativeReplyState = details.map013NativeReplyState,\n')
    if camera_free_direct:
        once('''        if not select(1, call(touch_camera, "SetCameraPos", remote_pos)) or
           not select(1, call(camera_manager, "RefreshCameraAnchor")) then''',
             '''        if request.requestMode ~= "direct" and
           (not select(1, call(touch_camera, "SetCameraPos", remote_pos)) or
            not select(1, call(camera_manager, "RefreshCameraAnchor"))) then''')
    return text.encode("utf-8")
