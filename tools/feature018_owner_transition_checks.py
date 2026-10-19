"""Execute the actual packaged Lua FEATURE018 session-owner transition gate.

No game, installed scripts, account action, file mailbox or network involved.
The falsifying case is a transient missing native UID sample between two real
owner ticks: losing one sample must pause dispatch without discarding an
already-sent City ACK fence; sustained loss or a different owner must retire.
"""
from __future__ import annotations

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / "artifacts/map-013-lua-site"))
sys.path.insert(0, str(ROOT))
from lupa.lua53 import LuaRuntime  # noqa: E402
from run_overview_bridge import wrapper_source  # noqa: E402


def check() -> None:
    source = wrapper_source().decode("utf-8")
    begin = source.rfind("local feature018_previous_owner = nil")
    end = source.find("local function pump_runtime()", begin)
    assert begin >= 0 and end > begin, "live packaged FEATURE018 owner seam missing"
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        owner = nil
        clock = 0
        os.time = function() return clock end
        city = {cleanup = 0, pump = 0}
        mini = {cleanup = 0}
        sheep = {cleanup = 0}
        afk = {cleanup = 0, pump = 0}
        feature = {pump = 0}
        function city.CleanupAfterLeaseLoss() city.cleanup = city.cleanup + 1 end
        function mini.CleanupAfterLeaseLoss() mini.cleanup = mini.cleanup + 1 end
        function sheep.CleanupAfterLeaseLoss() sheep.cleanup = sheep.cleanup + 1 end
        function afk.CleanupAfterLeaseLoss() afk.cleanup = afk.cleanup + 1 end
        function afk.Pump() afk.pump = afk.pump + 1 end
        function city.Pump() city.pump = city.pump + 1 end
        function sheep.Pump() end
        function feature.Pump() feature.pump = feature.pump + 1 end
        bridge = {
            Feature018OwnerToken = function() return owner end,
            SetFeature018PeriodicTick = function() return true end,
        }
    """)
    periodic = lua.execute(source[begin:end] + "\nreturn feature018_periodic\n")

    def tick(at: int, uid: str | None) -> tuple[int, int, int, int, int]:
        lua.globals().clock = at
        lua.globals().owner = uid
        periodic()
        return tuple(int(getattr(lua.globals(), name)[field])
                     for name, field in (("city", "cleanup"), ("mini", "cleanup"),
                                         ("sheep", "cleanup"), ("city", "pump"),
                                         ("feature", "pump")))

    first = tick(0, "owner-A")
    assert first == (0, 0, 0, 1, 1), first
    assert tick(1, None) == (0, 0, 0, 1, 1), "lost owner ran an action or erased ACK"
    assert tick(2, None) == (0, 0, 0, 1, 1)
    assert tick(3, "owner-A") == (0, 0, 0, 2, 2), "transient gap retired same owner"
    assert tick(4, None) == (0, 0, 0, 2, 2)
    assert tick(7, None) == (1, 1, 1, 2, 2), "sustained loss did not retire wrappers"
    assert tick(8, "owner-B") == (1, 1, 1, 3, 3)
    assert tick(9, "owner-C") == (2, 2, 2, 4, 4), "different owner inherited wrappers"
    assert tick(10, None) == (2, 2, 2, 4, 4)
    assert tick(14, None) == (3, 3, 3, 4, 4), "missing owner remained armed"

    # R18: a still-fresh exact session can have a temporarily missing account.
    # Pause City through >15s, but never dispatch until that account returns.
    lua.execute('bridge.Feature018SessionToken=function() return "same-session" end')
    before = tick(20, "owner-C")
    assert tick(21, None)[3:] == before[3:]
    after_gap = tick(40, None)
    assert after_gap[0] == before[0] and after_gap[3:] == before[3:], 'City aborted during transient account gap'
    returned = tick(41, "owner-C")
    assert returned[0] == before[0] and returned[3] == before[3]+1
    assert tick(42, "owner-D")[0] == returned[0]+1, 'real account change inherited City job'


if __name__ == "__main__":
    check()
    print("FEATURE018_OWNER_TRANSITION_NATIVE_LUA53_PASS")
