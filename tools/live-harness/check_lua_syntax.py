"""Syntax check actual source with Lua 5.3, with no game or source execution."""
from pathlib import Path
import sys

repo = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(repo / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime
lua = LuaRuntime()
load = lua.eval("load")
sources = (
    "src/LWBridge.GameScripts/current_live_resource_probe.lua",
    "src/LWBridge.GameScripts/current_overview_bridge.lua",
    "tools/research/map_009_offline/map013_wrapper_trace.lua",
)
for name in sources:
    source = (repo / name).read_text(encoding="utf-8")
    result = load(source, name, "t")
    if isinstance(result, tuple):
        raise SyntaxError(f"{name}: {result[1]}")
    if result is None:
        raise SyntaxError(name)
    print(f"MAP014_LUA53_COMPILE_OK {name} {len(source)}")
