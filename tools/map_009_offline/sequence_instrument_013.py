"""Private serial region experiment with one final restoration; no product patch."""
from __future__ import annotations

RESTORE_VARS = (
    "bulk_aoi_native_camera", "bulk_aoi_native_touch_camera", "bulk_aoi_native_original_pos",
    "bulk_aoi_native_original_fov", "bulk_aoi_native_unity_camera", "bulk_aoi_native_original_aspect",
    "bulk_aoi_native_original_zoom", "bulk_aoi_original_start_view_request",
    "bulk_aoi_original_block_count", "bulk_aoi_block_count_touched",
)


def instrument_sequence(body: bytes) -> bytes:
    text = body.decode().replace("\r\n", "\n")

    def once(old: str, new: str) -> None:
        nonlocal text
        if text.count(old) != 1:
            raise ValueError("sequence experiment anchor drift: " + old[:70])
        text = text.replace(old, new, 1)

    once('    request.requestMode = tostring(values.requestMode or "native")\n',
         '''    request.requestMode = tostring(values.requestMode or "native")
    request.map013Sequence = values.map013Sequence
    if request.map013Sequence ~= nil and request.map013Sequence ~= "keep" and request.map013Sequence ~= "final" then
        request.error="map013_sequence_invalid";return request
    end
''')
    save = ',\n'.join(f'            {n}={n}' for n in RESTORE_VARS)
    restore = '\n'.join(f'    {n}=g.{n}' for n in RESTORE_VARS)
    # Defined after the real restore helper: the actual normal restorer is used.
    helpers = '''
function M._map013SequenceAdmit(r,manager,preX,preY)
    local g=M._map013Group
    if g and (g.profileId~=r.profileId or g.sessionId~=r.launchSessionId or g.gamePid~=r.gamePid or g.serverId~=r.serverId) then return false end
    if not r.map013Sequence then return g==nil end
    if r.requestMode~="coverage" then return false end
    if r.map013Sequence=="final" and not g then return false end
    if not g then
        M._map013Group={profileId=r.profileId,sessionId=r.launchSessionId,gamePid=r.gamePid,serverId=r.serverId,
            manager=manager,homeX=preX,homeY=preY,deadline=os.time()+12,
SAVE
        }
    else g.deadline=os.time()+12 end
    return true
end
function M._map013UseOriginalHome()
    local g=M._map013Group
    if not g then return end
RESTORE
    bulk_aoi_original_manager_response_flag=false
    bulk_aoi_original_world_response_flag=false
    bulk_aoi_response_flags_reset=true
    bulk_aoi_skip_restore_update=false
end
function M._map013SequenceGuard()
    local g=M._map013Group
    if g and bulk_aoi_request==nil and os.time()>=g.deadline then
        M._map013UseOriginalHome()
        local restored=restore_bulk_aoi_state(g.manager)
        M._map013Group=nil
        write_json(root..[[\\map013-sequence-timeout.json]],
            {state="failed",error="sequence_idle_timeout",restorationAttemptSucceeded=restored})
    end
end
'''.replace('SAVE', save).replace('RESTORE', restore)
    once('local function write_bulk_aoi_result(request, state, error_text, details)\n',
         helpers + '\nlocal function write_bulk_aoi_result(request, state, error_text, details)\n')
    once('    M._map013Retire(request)\n    local restored = restore_bulk_aoi_state(point_manager)',
         '    M._map013Retire(request)\n    M._map013UseOriginalHome()\n    local restored = restore_bulk_aoi_state(point_manager)\n    M._map013Group=nil')
    once('        if not M._map013Begin(request,server_lod) then',
         '''        if not M._map013SequenceAdmit(request,point_manager,pre_tile_x,pre_tile_y) then
            fail_bulk_aoi(request,"map013_sequence_owner_or_mode_invalid",nil,point_manager);return true
        end
        if not M._map013Begin(request,server_lod) then''')
    once('        local request_method = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"',
         '''        if request.map013Sequence then
            if not select(1,call(camera_manager,"RefreshCameraAnchor")) or
                not select(1,call(point_manager,"StartViewRequest")) then
                fail_bulk_aoi(request,"sequence_projection_refresh_failed",nil,point_manager);return true
            end
        end
        local request_method = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"''')
    once('    if request_completed then\n        M._map013Retire(bulk_aoi_request)',
         '''    if request_completed and bulk_aoi_request.map013Sequence=="keep" then
        M._map013Retire(bulk_aoi_request)
        details.map013PositionRetained=true
        details.cameraTileStable=false
        details.totalProbeElapsedSeconds=runtime_clock()-bulk_aoi_started_at
        write_bulk_aoi_result(bulk_aoi_request,"proven",nil,details)
        bulk_aoi_request=nil;bulk_aoi_started_at=nil
        return true
    end
    if request_completed then
        if bulk_aoi_request.map013Sequence=="final" and M._map013Group then
            details.preTileX=M._map013Group.homeX;details.preTileY=M._map013Group.homeY
            M._map013UseOriginalHome()
        end
        M._map013Retire(bulk_aoi_request)''')
    once('    local lookup = bulk_selected_lookup(details.requestedIndices)\n',
         '''    if bulk_aoi_request.map013Sequence then
        local reply_state=M._map013Match(bulk_aoi_request)
        local f=bulk_manager_flags(point_manager)
        if reply_state~="matched" or f.isRecvViewPoints~=true or world_response_flag(world)~=true or
            reflected_value(point_manager,"_splitLastAOIRequest")==true then
            if runtime_clock()-bulk_aoi_started_at>=10 then
                fail_bulk_aoi(bulk_aoi_request,"sequence_native_reply_timeout",details,point_manager)
            end
            return true
        end
        local current=reflected_value(point_manager,"_curViewIndex")
        local actual=current and select(1,collection_int_values(current,512)) or nil
        if actual==nil or not collection_contains_int(current,details.targetAoiIndex) then
            details.map013Requeues=(details.map013Requeues or 0)+1
            if details.map013Requeues>3 then
                fail_bulk_aoi(bulk_aoi_request,"sequence_native_footprint_stale",details,point_manager);return true
            end
            M._map013Retire(bulk_aoi_request)
            if not M._map013Begin(bulk_aoi_request,details.serverLod) then
                fail_bulk_aoi(bulk_aoi_request,"sequence_rearm_failed",details,point_manager);return true
            end
            if not reflected_set_value(point_manager,"isRecvViewPoints",false) or
                not reflected_set_value(world,"hasReceiveViewPointsReply",false) then
                fail_bulk_aoi(bulk_aoi_request,"sequence_reply_reset_failed",details,point_manager);return true
            end
            local camera=safe_get(world,"Camera") or reflected_value(world,"<Camera>k__BackingField")
            local refreshed=select(1,call(camera,"RefreshCameraAnchor"))
            local started=select(1,call(point_manager,"StartViewRequest"))
            local sent=select(1,call(point_manager,"UpdateViewRequest",true))
            if not refreshed or not started or not sent then
                fail_bulk_aoi(bulk_aoi_request,"sequence_native_refresh_failed",details,point_manager)
            end
            return true
        end
        details.requestedIndices=actual;details.nativeCurrentSetCount=#actual
    end
    local lookup = bulk_selected_lookup(details.requestedIndices)
''')
    once('            details.positionRestoredAfterResponse = true\n',
         '            details.positionRestoredAfterResponse = true\n            M._map013Group=nil\n')
    once('        map013NativeReplyState = details.map013NativeReplyState,\n',
         '        map013NativeReplyState = details.map013NativeReplyState,\n        map013PositionRetained = details.map013PositionRetained,\n        map013Requeues = details.map013Requeues,\n')
    once('M.Register()\nreturn M\n', '''M._map013OriginalPump=M.Pump
M.Pump=function(...)
    local values={pcall(M._map013OriginalPump,...)}
    local ok=table.remove(values,1)
    M._map013SequenceGuard()
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
M.Register()
return M
''')
    return text.encode()
