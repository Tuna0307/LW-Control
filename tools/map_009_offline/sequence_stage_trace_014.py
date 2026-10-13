"""Private camera/subscription stage observation; no shipped probe changes.

Keep the first region's complete restoration snapshot, not the snapshot of
the second retained position. Observe exact native state before/after the
forced update so logical target echoes cannot stand in for subscriptions.
"""


def instrument_subscription_stages(body: bytes, *, bounded_lens: bool = False,
                                   lens_fov: float = 40.0) -> bytes:
    text = body.decode("utf-8").replace("\r\n", "\n")

    def once(old: str, new: str) -> None:
        nonlocal text
        if text.count(old) != 1:
            raise ValueError("subscription stage source anchor drift: " + old[:60])
        text = text.replace(old, new, 1)

    if bounded_lens:
        if lens_fov not in (20.0, 40.0):
            raise ValueError("unsupported diagnostic lens")
        # Genuine v24 camera pitch is 45 degrees. The former FOV120 has
        # upper rays above the ground horizon; actual retained anchors then
        # fail to follow the translated camera. FOV40 remains below that
        # horizon. This is a private candidate, not accepted production.
        once('call(camera_manager, "SetFOV", request.coverageFov)',
             f'call(camera_manager, "SetFOV", {lens_fov:.1f})')

    once('            manager=manager,homeX=preX,homeY=preY,deadline=os.time()+12,',
         '            manager=manager,homeX=preX,homeY=preY,deadline=os.time()+12,\n'
         '            map014Restore=M._map014CameraRestore,')
    once('    bulk_aoi_original_manager_response_flag=false\n',
         '    M._map014CameraRestore=g.map014Restore\n'
         '    bulk_aoi_original_manager_response_flag=false\n')
    helper = '''
function M._map014SubscriptionStage(r,stage,manager,camera,unity)
    if not r then return end
    local values=r._map014SubscriptionStages or {}
    if #values>=8 then return end
    r._map014SubscriptionStages=values
    local current=reflected_value(manager,"_curViewIndex")
    local indices=current and select(1,collection_int_values(current,512)) or nil
    local w=runtime_world()
    local tile=w and safe_get(w,"CurTilePosClamped") or nil
    values[#values+1]={stage=stage,elapsed=runtime_clock(),
        tileX=tonumber(tile and safe_get(tile,"x")),tileY=tonumber(tile and safe_get(tile,"y")),
        currentIndices=indices,geometry=M._map014CameraGeometryCheckpoint(camera,unity),
        startViewRequest=reflected_value(manager,"startViewRequest"),
        flags=bulk_manager_flags(manager),
        splitPending=reflected_value(manager,"_splitLastAOIRequest")==true}
end
'''
    once('function M._map013SequenceAdmit(r,manager,preX,preY)\n',
         helper + '\nfunction M._map013SequenceAdmit(r,manager,preX,preY)\n')
    once('        bulk_aoi_native_original_fov = checkpoint.fov\n',
         '        M._map014SubscriptionStage(request,"before-shift",point_manager,camera_manager,unity_camera)\n'
         '        bulk_aoi_native_original_fov = checkpoint.fov\n')
    once('        local remote_tile = safe_get(world, "CurTilePosClamped")\n',
         '        M._map014SubscriptionStage(request,"after-shift",point_manager,camera_manager,unity_camera)\n'
         '        local remote_tile = safe_get(world, "CurTilePosClamped")\n')
    once('        local manager_reply_before = manager_response_flag(point_manager)\n',
         '        M._map014SubscriptionStage(request,"post-lens",point_manager,camera_manager,unity_camera)\n'
         '        local manager_reply_before = manager_response_flag(point_manager)\n')
    once('        if not invoked then\n            call(camera_manager, "RefreshCameraAnchor")\n',
         '        M._map014SubscriptionStage(request,"post-update",point_manager,camera_manager,unity_camera)\n'
         '        if not invoked then\n            call(camera_manager, "RefreshCameraAnchor")\n')
    once('    local elapsed_now = bulk_aoi_started_at and (runtime_clock() - bulk_aoi_started_at) or 0\n',
         '''    if not bulk_aoi_request._map014FirstPoll then
        bulk_aoi_request._map014FirstPoll=true
        M._map014SubscriptionStage(bulk_aoi_request,"first-poll",point_manager,
            bulk_aoi_native_camera,bulk_aoi_native_unity_camera)
    end
    local elapsed_now = bulk_aoi_started_at and (runtime_clock() - bulk_aoi_started_at) or 0
''')
    once('        map013PositionRetained = details.map013PositionRetained,\n',
         '        map013PositionRetained = details.map013PositionRetained,\n'
         '        map014SubscriptionStages = request._map014SubscriptionStages,\n')
    return text.encode("utf-8")
