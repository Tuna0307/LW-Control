"""Verify retained research evidence without executing the target or recovered bootstrap."""
import argparse
import hashlib
import json
from pathlib import Path


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, help="Optional owner-supplied matching LastWarPlugin.exe")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    index = json.loads((root / "evidence-index.json").read_text(encoding="utf-8"))
    for item in index["retained_files"]:
        relative = Path(item["path"])
        assert not relative.is_absolute() and ".." not in relative.parts, "Unsafe evidence path"
        data = (root / relative).read_bytes()
        assert len(data) == item["bytes"], f"Size mismatch: {relative}"
        assert digest(data) == item["sha256"], f"Hash mismatch: {relative}"

    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    for item in manifest["files"]:
        data = (root / item["file"]).read_bytes()
        assert len(data) == item["bytes"] and digest(data) == item["sha256"]
    for item in json.loads((root / "scripts-manifest.json").read_text(encoding="utf-8")):
        parent = (root / item["parent"]).read_bytes()
        expected = parent[item["parent_offset"]:item["parent_offset"] + item["bytes"]]
        assert (root / item["file"]).read_bytes() == expected, f"Script provenance mismatch: {item['file']}"

    bootstrap = (root / "bootstrap.txt").read_bytes()
    assert len(bootstrap) == 456 and digest(bootstrap) == index["bootstrap"]["sha256"]
    if args.exe:
        original = args.exe.read_bytes()
        assert digest(original) == index["target"]["sha256"], "Different target executable"
        for item in manifest["files"]:
            expected = original[int(item["offset"], 16):int(item["end_offset"], 16)]
            assert (root / item["file"]).read_bytes() == expected, f"EXE extraction mismatch: {item['file']}"
        start = int(index["bootstrap"]["offset"], 16)
        decoded = bytes(value ^ 0x5A for value in original[start:start + 456])
        assert decoded == bootstrap, "EXE bootstrap mismatch"
    print(f"LASTWARPLUGIN_RESEARCH_EVIDENCE_OK files={len(index['retained_files'])} original_exe={bool(args.exe)} target_executed=False")


if __name__ == "__main__":
    main()
