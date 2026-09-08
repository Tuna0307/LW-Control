#!/usr/bin/env python3
"""Read-only static inspector for the verified LWBridge bootstrap/injection assets."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

ASSETS = {
    "xlua-proxy-secure.dll": (0x987F74, 0x95800),
    "xlua-proxy-plain.dll": (0xA1D774, 0x96000),
    "lwbridge-profile-launcher.exe": (0xB72B52, 0xA7200),
    "lwbridge-multi-hook.dll": (0xC19D52, 0x65A00),
}

INTERESTING_IMPORTS = {
    "CreateProcessW",
    "VirtualAllocEx",
    "WriteProcessMemory",
    "CreateRemoteThread",
    "OpenProcess",
    "ResumeThread",
    "SuspendThread",
    "LoadLibraryA",
    "LoadLibraryW",
    "LoadLibraryExW",
    "CreateEventW",
    "OpenEventW",
    "SetEvent",
    "WaitForSingleObject",
    "CreateFileW",
}

LAUNCHER_MARKERS = (
    "stage=launch_begin",
    "stage=hook_events_created",
    "stage=hook_injected",
    "stage=hook_ready",
    "stage=game_resumed",
    "stage=proxy_injection_prepared",
    "HOOK_ALLOC_FAILED",
    "HOOK_WRITE_FAILED",
    "HOOK_THREAD_FAILED",
    "HOOK_INJECT_TIMEOUT",
    "HOOK_LOAD_FAILED",
    "HOOK_READY_TIMEOUT",
    "GAME_XLUA_ABI_UNSUPPORTED",
    "PROXY_INSTALL_AT_LAUNCH_FAILED",
    "LWBRIDGE_HOOK_READY_EVENT",
    "LWBRIDGE_HOOK_GO_EVENT",
    "LWBRIDGE_HOOK_FAILED_EVENT",
)

HOOK_MARKERS = (
    "redirected game xlua load api=LoadLibraryW requested=xlua.dll proxy=runtime",
    "redirected game xlua load api=LoadLibraryExW requested=xlua.dll proxy=runtime",
    "XLUA_REDIRECT_HOOK_FAILED",
    "ready event signaled",
    "single-instance hooks failed",
    "native mutex and guard-thread hooks installed",
)

PROXY_MARKERS = (
    "__XluaBridgePipeSend",
    "__XluaBridgeLoad",
    "__XluaBridgeEvalHook",
    "XluaBridgeNativeStart",
    "XluaBridgeMapScanTick",
    "XluaBridgeNativeUpdate",
    "XluaBridgePoll",
    '"type":"hello"',
)


class InspectError(ValueError):
    pass


def _asset_bytes(pe: pefile.PE, data: bytes, name: str) -> bytes:
    rva, size = ASSETS[name]
    try:
        offset = int(pe.get_offset_from_rva(rva))
    except pefile.PEFormatError as exc:
        raise InspectError(f"asset {name} RVA 0x{rva:X} is not file-backed") from exc
    payload = data[offset : offset + size]
    if len(payload) != size:
        raise InspectError(f"asset {name} is truncated")
    return payload


def _imports(payload: bytes) -> dict[str, list[str]]:
    pe = pefile.PE(data=payload, fast_load=False)
    rows: dict[str, list[str]] = {}
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        dll = entry.dll.decode("ascii", "replace")
        matches: list[str] = []
        for item in entry.imports:
            if item.name is None:
                continue
            name = item.name.decode("ascii", "replace")
            if name in INTERESTING_IMPORTS:
                matches.append(name)
        if matches:
            rows[dll] = matches
    return rows


def _exports(payload: bytes) -> list[dict[str, Any]]:
    pe = pefile.PE(data=payload, fast_load=False)
    rows: list[dict[str, Any]] = []
    for symbol in getattr(getattr(pe, "DIRECTORY_ENTRY_EXPORT", None), "symbols", []):
        if symbol.name is None:
            continue
        name = symbol.name.decode("ascii", "replace")
        if "XluaBridge" not in name:
            continue
        rows.append({"name": name, "rva": int(symbol.address)})
    return rows


def _markers(payload: bytes, markers: tuple[str, ...]) -> dict[str, bool]:
    return {marker: marker.encode("ascii") in payload for marker in markers}


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}")

    pe = pefile.PE(data=data, fast_load=False)
    assets: dict[str, Any] = {}
    payloads: dict[str, bytes] = {}
    for name in ASSETS:
        payload = _asset_bytes(pe, data, name)
        payloads[name] = payload
        assets[name] = {
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
            "interestingImports": _imports(payload),
            "bridgeExports": _exports(payload),
        }

    assets["lwbridge-profile-launcher.exe"]["markers"] = _markers(
        payloads["lwbridge-profile-launcher.exe"], LAUNCHER_MARKERS
    )
    assets["lwbridge-multi-hook.dll"]["markers"] = _markers(
        payloads["lwbridge-multi-hook.dll"], HOOK_MARKERS
    )
    for name in ("xlua-proxy-secure.dll", "xlua-proxy-plain.dll"):
        assets[name]["markers"] = _markers(payloads[name], PROXY_MARKERS)

    return {
        "lwbridgeSha256": digest,
        "assets": assets,
        "recoveredSequence": [
            "validate descriptor, parent, launch proof/ticket, paths, and hook integrity",
            "create hook ready/go/failed events",
            "create official launcher/game suspended",
            "allocate and write hook DLL path into target",
            "run target LoadLibraryW with CreateRemoteThread",
            "require inject-thread success and hook-ready event",
            "signal hook-go event",
            "resume official launcher/game",
            "redirect game xlua.dll load to runtime proxy",
            "require proxy named-pipe hello and bridge heartbeat",
        ],
    }


def _text_report(result: dict[str, Any]) -> str:
    lines = [f"lwbridge_sha256={result['lwbridgeSha256']}", "recovered_sequence:"]
    for index, stage in enumerate(result["recoveredSequence"], 1):
        lines.append(f"  {index}. {stage}")
    lines.append("assets:")
    for name, row in result["assets"].items():
        lines.append(f"  {name}: size={row['size']} sha256={row['sha256']}")
        for dll, imports in row["interestingImports"].items():
            lines.append(f"    imports {dll}: {', '.join(imports)}")
        if row.get("bridgeExports"):
            rendered = ", ".join(
                f"{item['name']}@0x{item['rva']:X}" for item in row["bridgeExports"]
            )
            lines.append(f"    bridge_exports: {rendered}")
        markers = row.get("markers", {})
        if markers:
            missing = [marker for marker, present in markers.items() if not present]
            lines.append(
                f"    markers={len(markers) - len(missing)}/{len(markers)}"
                + (f" missing={missing!r}" if missing else "")
            )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(_text_report(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
