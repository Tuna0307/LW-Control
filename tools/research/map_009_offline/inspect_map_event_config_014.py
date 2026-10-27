"""Hash-admitted, read-only current-game Party/Supplies config evidence."""
import argparse
import hashlib
import json
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "src" / "LWBridge.GameScripts"))
import run_live_resource_probe as probe
import compact_lua53 as lua
from read_config_literals_014 import read_literals

ARCHIVE_SHA256 = "f30ae484c4527aeec1db90f3e567d38b7718b96bbfd2364eafd29084146ec41f"


def inspect(path: Path) -> dict:
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != ARCHIVE_SHA256:
        raise ValueError("unsupported current-game config archive")
    selected = {"detect_event": [27000, 27013, 27014, 27015, 25098],
                "ice_supplies": [1]}
    tables = {}
    with zipfile.ZipFile(path) as archive:
        for name, identifiers in selected.items():
            encoded = archive.read(name)
            decoded = probe.decode_lenc(encoded)
            literal = read_literals(lua.parse_chunk(decoded))
            fields = literal["index"]
            rows = []
            for identifier in identifiers:
                data = literal["data"][identifier]
                rows.append({field: data.get(column[1]) for field, column in fields.items()
                             if field in {"id", "type", "type2", "name", "description", "desc",
                                          "season", "para", "c_para", "level", "total_limit"}})
            tables[name] = {"encodedSha256": hashlib.sha256(encoded).hexdigest(),
                            "decodedSha256": hashlib.sha256(decoded).hexdigest(), "rows": rows}
    return {"archiveSha256": digest, "literalOnlyNoGameExecution": True,
            "liveSpawnWitness": False, "tables": tables}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, default=Path.home() / "AppData/Local/FunFly/Last War-Survival Game/Game/LastWar_Data/StreamingAssets/table/table_39432_579ad1f65adc8149666b1fd19cd8e38d.data")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if "map-009-offline" not in str(args.output.resolve()).lower():
        raise ValueError("raw config evidence belongs in ignored task scratch")
    result = inspect(args.archive)
    args.output.write_text(json.dumps(result, indent=2), encoding="utf8")
    print("MAP014_CONFIG_SOURCE_OK", "Party event=27000", "Supplies type=1")


if __name__ == "__main__":
    main()
