"""Opt-in source-locked read-only v24 event-manager availability counters.

Does not create, claim, attack, move, share, spawn or request an event.
Counts available currently loaded native collections; absence from one
sample is never a whole-server event absence conclusion.
"""
from __future__ import annotations


def instrument_event_source(body: bytes) -> bytes:
    text = body.decode("utf-8").replace("\r\n", "\n")

    def once(old: str, new: str) -> None:
        nonlocal text
        if text.count(old) != 1:
            raise ValueError("map014 event source anchor drift: " + old.strip()[:100])
        text = text.replace(old, new, 1)

    once('        details.doomsdayBossCount = 0\n', '''        -- These are optional observational counters only, not a synthetic
        -- Doom Walker or supplies row. Capture manager state *after* the
        -- already-supported read-only Doomsday main-info settle period.
        local dc = rawget(_G, "DataCenter")
        local boss_manager = dc and safe_get(dc, "LWDoomsdayManager") or nil
        local theater = boss_manager and safe_get(boss_manager, "theaterBosses") or nil
        local alliance = boss_manager and safe_get(boss_manager, "allianceBosses") or nil
        local native_points = reflected_value(point_manager, "_pointInfos")
        local total_loaded = native_points and collection_count(native_points) or nil
        local observed_ordinary, observed_supplies = 0, 0
        local counted = native_points and each(native_points, MAX_POINTS + 1, function(raw)
            local info = safe_get(raw, "Value") or raw
            local kind = integer_field(info, { "pointType", "PointType" })
            if kind == 21 then observed_ordinary = observed_ordinary + 1 end
            if kind == 27 then observed_supplies = observed_supplies + 1 end
            return true
        end) or nil
        details.map014EventSourceStats = {
            doomsdayManagerAvailable = boss_manager ~= nil,
            theaterCollectionType = type(theater),
            theaterCount = theater and collection_count(theater) or nil,
            allianceCollectionType = type(alliance),
            allianceCount = alliance and collection_count(alliance) or nil,
            nativePointInfosCount = total_loaded,
            nativePointInfosEnumerated = counted,
            nativeTreasureType21Loaded = observed_ordinary,
            nativeSuppliesType27Loaded = observed_supplies,
            interpretation = "current loaded source only; no global event-spawn census",
        }
        details.doomsdayBossCount = 0
''')
    once('        doomsdayBossCount = details.doomsdayBossCount or 0,\n',
         '        doomsdayBossCount = details.doomsdayBossCount or 0,\n'
         '        map014EventSourceStats = details.map014EventSourceStats,\n')
    return text.encode("utf-8")
