"""Opt-in isolated intermediate-Zoom diagnostic, never default product source.

Unlike ZoomMax this targets the smaller of 15% remaining zoom range and 30
native zoom units. The real response must independently prove LOD0, AOI
coverage, source identities and complete original camera restoration.
"""
from __future__ import annotations


def instrument_intermediate_zoom(source: bytes) -> bytes:
    old = b'local ok_zoom = pcall(function() camera_manager.Zoom = zoom_max end)'
    # No approximate matching or silent drift of game camera mutation call.
    if source.count(old) != 1:
        raise ValueError("intermediate zoom source anchor drift")
    new = b'''local target_zoom = math.min(
                original_zoom + (zoom_max - original_zoom) * 0.15,
                original_zoom + 30)
            if target_zoom <= original_zoom or target_zoom >= zoom_max then
                fail_bulk_aoi(request, "intermediate_zoom_unavailable", nil, point_manager)
                return true
            end
            local ok_zoom = pcall(function() camera_manager.Zoom = target_zoom end)'''
    return source.replace(old, new)
