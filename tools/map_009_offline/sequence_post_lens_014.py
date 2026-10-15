"""Private sequence candidate: refresh native AOI *after* changed lens.

The rejected sequence previously refreshed anchors/StartViewRequest only
before applying coverage FOV/aspect on its next region. This source-locked
diagnostic adds a post-lens anchor/subscription refresh; it never ships
in production unless genuinely fresh LOD0 peers and exact final restoration
are subsequently demonstrated.
"""


def instrument_post_lens_refresh(body: bytes) -> bytes:
    text = body.decode("utf-8").replace("\r\n", "\n")
    before = '        local manager_reply_before = manager_response_flag(point_manager)\n'
    if text.count(before) != 1 or 'function M._map013SequenceAdmit' not in text:
        raise ValueError("post-lens sequence source anchor/ownership drift")
    inject = '''        if request.map013Sequence then
            -- The second region is already on a retained translated camera.
            -- Rebuild native anchor and subscribe only after FOV120/aspect
            -- modifications, rather than reusing the pre-lens 9-AOI view.
            local refreshed = select(1, call(camera_manager, "RefreshCameraAnchor"))
            local subscribed = select(1, call(point_manager, "StartViewRequest"))
            if not refreshed or not subscribed then
                fail_bulk_aoi(request, "map014_post_lens_subscription_failed", nil, point_manager)
                return true
            end
        end
'''
    return text.replace(before, inject + before, 1).encode("utf-8")
