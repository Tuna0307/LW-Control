"""Read-only inventory of original Mini Games modules in the present v26 Lua pack.

The installed package may contain an independently owned bridge. Inspect only
game modules, verifying the preserved original entry and admitted content build.
This tool never writes to or executes the installed game/package.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(TOOLS / "map_009_offline"))
import run_live_resource_probe as probe  # noqa: E402
import compact_lua53 as lua  # noqa: E402
from inspect_current import root_methods, walk, disassemble  # noqa: E402

ORIGINAL_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"


def inspect(patterns: list[str], needle: str | None, details: str | None, limit: int,
            list_methods: bool = False):
    path = probe.paths()["data"]
    with path.open("rb") as file:
        digest = hashlib.file_digest(file, "sha256").hexdigest()
    file_version, content_version, raw = probe.read_lwlf(path)
    entries = dict(raw)
    original = entries.get(probe.ORIGINAL_LUA_ENTRY, entries.get(probe.LUA_ENTRY))
    if (file_version, content_version) not in ((3, 26), (3, 27)) or original is None or \
       hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
        raise ValueError("Installed Lua package has unrecognized version/original entry")
    names = sorted(n for n in entries if any(fnmatch.fnmatchcase(n.lower(), p.lower()) for p in patterns)
                   and "LWBridge" not in n and n != probe.LUA_ENTRY)
    print(f"ADMITTED_GAME_CONTENT 3/{content_version} original-entry-sha256={ORIGINAL_ENTRY_SHA256}")
    print(f"INSPECTED_PACKAGE_SHA256 {digest} (may contain an existing third-party bridge)")
    print(f"MATCHED_MODULES {len(names)}")
    if not needle and not details and not list_methods:
        for name in names[:limit]:
            print(name)
        return
    printed = 0
    for name in names:
        decoded = probe.decode_lenc(entries[name])
        root = lua.parse_chunk(decoded)
        methods = root_methods(root)
        if list_methods:
            print(f"MODULE {name} SHA256={hashlib.sha256(entries[name]).hexdigest()}")
            print("  METHODS", ", ".join(f"{m}={p}" for m, p in methods.items()))
            printed += 1
            if printed >= limit:
                return
            continue
        if details:
            path_ = methods.get(details, details)
            found = [(p, proto) for p, proto in walk(root) if p == path_]
        else:
            found = [(p, proto) for p, proto in walk(root) if any(needle.lower() in str(c).lower() for c in proto["constants"])]
        if not found:
            continue
        print(f"MODULE {name} SHA256={hashlib.sha256(entries[name]).hexdigest()}")
        print("  METHODS", ", ".join(f"{m}={p}" for m, p in methods.items()))
        for p, proto in found[:limit]:
            constants = proto["constants"]
            items = [str(c) for c in constants if not needle or needle.lower() in str(c).lower()]
            print(f"  PROTO {p} sourceLines={proto['line']}-{proto['lastLine']} params={proto['params']} matchingConstants={items[:20]}")
            if details:
                for line in disassemble(proto):
                    print("    " + line)
            printed += 1
            if printed >= limit:
                return


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--entry", action="append", required=True, help="glob of relative .luac module name")
    parser.add_argument("--find", help="substring in decoded prototype constants")
    parser.add_argument("--method", help="exact root method name or prototype path, disassemble")
    parser.add_argument("--list-methods", action="store_true", help="show decoded top-level method locators only")
    parser.add_argument("--limit", type=int, default=30)
    args = parser.parse_args()
    inspect(args.entry, args.find, args.method, args.limit, args.list_methods)
