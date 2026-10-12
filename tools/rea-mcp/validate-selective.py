#!/usr/bin/env python3
"""Read-only audit of MAP-013's ignored, full original-PE pseudocode receipts.

Validates original .pdata unwind ranges, the Ghidra source-bound body,
unabridged retained UTF-8 text and explicit excerpt/truncation metadata.
Does not execute any game or commercial reference runtime.
"""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import re

import pefile

ORIGINAL_SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
DEFAULT_ORIGINAL = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")


def parse_sections(report: str) -> list[dict[str, str]]:
    sections = []
    for chunk in re.split(r"(?=\nENTRY=)", report):
        rows = {}
        for line in chunk.splitlines():
            if re.match(r"^[A-Z0-9_]+(?:=[^\s]*)?$", line) and "=" in line:
                k, v = line.split("=", 1)
                rows[k] = v
            if line.startswith("SOURCE_UNWIND_START="):
                bits = line.split()
                rows.update(part.split("=", 1) for part in bits)
        if "ENTRY" in rows:
            sections.append(rows)
    return sections


def audit(original: Path, report_path: Path) -> dict[str, object]:
    with original.open("rb") as stream:
        sha = hashlib.file_digest(stream, "sha256").hexdigest()
    if sha != ORIGINAL_SHA:
        raise AssertionError(f"unexpected original SHA: {sha}")
    image = pefile.PE(str(original))
    image_base = image.OPTIONAL_HEADER.ImageBase
    source_bounds = {
        (image_base + entry.struct.BeginAddress, image_base + entry.struct.EndAddress)
        for entry in image.DIRECTORY_ENTRY_EXCEPTION
    }
    chunks = parse_sections(report_path.read_text(encoding="utf-8"))
    if not chunks:
        raise AssertionError("no function evidence entries")
    good = []
    failed = []
    for d in chunks:
        address = int(d["ENTRY"], 16)
        begin = int(d["SOURCE_UNWIND_START"], 16)
        end = int(d["SOURCE_UNWIND_END_EXCLUSIVE"], 16)
        assert address == begin and (begin, end) in source_bounds, (d["ENTRY"], "not source unwind range")
        assert int(d["SOURCE_UNWIND_BYTE_LENGTH"]) == end - begin
        assert d["SOURCE_RANGE_BODY_APPLIED"] == "true", address
        assert d["GHIDRA_BODY_MATCHES_SOURCE_RANGE"] == "true", address
        assert int(d["GHIDRA_BODY_ADDRESS_COUNT"]) == end - begin, address
        if d.get("DECOMPILE_COMPLETED") != "true":
            failed.append(hex(address))
            continue
        raw = Path(d["PSEUDOCODE_RAW_PATH"])
        assert raw.is_file(), raw
        data = raw.read_bytes()
        body = data.decode("utf-8")
        assert d["PSEUDOCODE_TRUNCATED"] == "false", address
        assert d["PSEUDOCODE_SHA256"] == hashlib.sha256(data).hexdigest(), address
        assert len(body) == int(d["PSEUDOCODE_ORIGINAL_CHARS"]) == int(d["PSEUDOCODE_RETAINED_CHARS"]), address
        assert len(data) == int(d["PSEUDOCODE_ORIGINAL_UTF8_BYTES"]) == int(d["PSEUDOCODE_RETAINED_UTF8_BYTES"]), address
        assert d["PSEUDO_EXCERPT_TRUNCATED"] in ("true", "false"), address
        good.append({"address": hex(address), "chars": len(body), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(), "sourceRange": [hex(begin), hex(end)]})
    return {"originalSha256": sha, "complete": good, "failedDecompilation": failed}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("--original", type=Path, default=DEFAULT_ORIGINAL)
    args = parser.parse_args()
    import json
    print(json.dumps(audit(args.original, args.report), indent=2))


if __name__ == "__main__":
    main()
