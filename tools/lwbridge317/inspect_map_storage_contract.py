#!/usr/bin/env python3
"""Recover exact-static LWBridge 0.3.17 Map SQLite/storage contract strings.

This helper is deliberately read-only and hash-locked to the immutable 0.3.17
reference executable. It extracts the contiguous Map schema DDL and records
selected persistence/query/path markers with exact raw/RVA locations. Presence
of a SQL/path string is static evidence only; control-flow ownership is traced
separately by the native-function inspectors.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile


REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"

SCHEMA_START = b"CREATE TABLE IF NOT EXISTS map_records ("
SCHEMA_END = b"CREATE INDEX IF NOT EXISTS idx_treasure_claim_states_expire\n                  ON treasure_claim_states(expire_time);"

MARKERS = (
    "map-data",
    "map-data.db",
    "map database is unavailable",
    "open map database",
    "MAP_DATABASE_ERROR",
    "PRAGMA journal_mode = WAL",
    "PRAGMA synchronous = NORMAL",
    "PRAGMA foreign_keys = ON",
    "PRAGMA busy_timeout = 5000",
    "SELECT value FROM metadata WHERE key = 'schema_version'",
    "legacy_bridge_runtime_import_v1",
    "ATTACH DATABASE ?1 AS legacy_bridge_runtime",
    "DETACH DATABASE legacy_bridge_runtime",
    "serverJumpHistory",
    "map_records",
    "scan_runs",
    "scan_blocks",
    "scan_records",
    "player_marks",
    "app_settings",
    "treasure_claim_states",
    "dispatch_plunder_jobs",
    "truck_plunder_jobs",
    "truck_plunder_history",
    "dispatch_assist_jobs",
    "ON CONFLICT(kind,server_id,record_key)",
    "PRAGMA wal_checkpoint(TRUNCATE)",
)


class InspectError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def all_offsets(data: bytes, needle: bytes) -> list[int]:
    result: list[int] = []
    start = 0
    while True:
        hit = data.find(needle, start)
        if hit < 0:
            return result
        result.append(hit)
        start = hit + 1


def raw_to_rva(pe: pefile.PE, raw: int) -> int | None:
    for section in pe.sections:
        start = int(section.PointerToRawData)
        end = start + int(section.SizeOfRawData)
        if start <= raw < end:
            return int(section.VirtualAddress) + raw - start
    if raw < int(pe.OPTIONAL_HEADER.SizeOfHeaders):
        return raw
    return None


def locate(pe: pefile.PE, data: bytes, marker: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    image_base = int(pe.OPTIONAL_HEADER.ImageBase)
    for raw in all_offsets(data, marker.encode("utf-8")):
        rva = raw_to_rva(pe, raw)
        rows.append(
            {
                "raw": f"0x{raw:X}",
                "rva": None if rva is None else f"0x{rva:X}",
                "va": None if rva is None else f"0x{image_base + rva:X}",
            }
        )
    return rows


def extract_schema(data: bytes) -> tuple[int, str]:
    start = data.find(SCHEMA_START)
    if start < 0:
        raise InspectError("Map schema start marker is absent")
    end = data.find(SCHEMA_END, start)
    if end < 0:
        raise InspectError("Map schema end marker is absent")
    end += len(SCHEMA_END)
    raw = data[start:end]
    return start, raw.decode("utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    data = args.binary.read_bytes()
    digest = sha256(data)
    if digest != REFERENCE_SHA256:
        raise InspectError(f"unexpected LWBridge 0.3.17 SHA-256: {digest}")
    pe = pefile.PE(data=data, fast_load=False)

    schema_raw, schema_sql = extract_schema(data)
    schema_rva = raw_to_rva(pe, schema_raw)
    marker_rows = {marker: locate(pe, data, marker) for marker in MARKERS}

    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-001-STORAGE-STATIC",
        "evidenceState": "EXACT_BYTES_STATIC_CONTRACT",
        "source": {
            "path": str(args.binary),
            "sha256": digest,
            "imageBase": f"0x{int(pe.OPTIONAL_HEADER.ImageBase):X}",
        },
        "schemaSql": {
            "raw": f"0x{schema_raw:X}",
            "rva": None if schema_rva is None else f"0x{schema_rva:X}",
            "sha256": hashlib.sha256(schema_sql.encode("utf-8")).hexdigest(),
            "sql": schema_sql,
        },
        "markers": marker_rows,
        "limits": [
            "Exact schema/path/SQL string presence does not alone establish the command or transaction that uses it.",
            "Schema migration/version transitions, record normalization, staging publication, clear ownership, and query control flow require native tracing.",
            "Current-client adapter metadata must not be added to this recovered reference schema unless separately evidenced.",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
