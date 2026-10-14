"""Read-only, hash-gated current-game march field/method inventory.

No game session, network request, patching, decompiler process or output file.
Use only the exact installed assembly admitted by inspect_managed.SHA256.
"""

import argparse
import hashlib
import json
import sys
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1] / "map_009_offline"
sys.path.insert(0, str(TOOLS))
from inspect_managed import DEFAULT, SHA256  # noqa: E402
from rgmd_il import MetadataImage  # noqa: E402


EXPECTED_FIELDS = {
    "allMarches": "0x0400253a",
    "viewBounds": "0x0400254e",
    "viewRect": "0x0400254f",
    "lastCameraViewRange": "0x04002550",
    "cameraViewRect": "0x04002551",
    "_messages": "0x04002561",
    "_messagesMergeDict": "0x04002562",
    "_lastGetBlockMsgTime": "0x04002566",
    "_lastHandleBlockMsgTime": "0x04002567",
}

EXPECTED_METHODS = {
    "OnUpdate": "0x0600326c",
    "UpdateViewRect": "0x0600326e",
    "IsInView": "0x0600326f",
    "IsLineInView": "0x06003270",
    "IsRectInView": "0x06003271",
    "IsMarchInView": "0x06003272",
    "TryExcuteMarchMessage": "0x06003276",
    "HandleMessage": "0x06003277",
    "HandleWorldMarchGet": "0x06003278",
    "HandleWorldGetRectMarchInfos": "0x0600327b",
    "WorldGetMarchInfos": "0x060032aa",
    "GetAllMarchesByCS": "0x060032c9",
}

EXPECTED_MESSAGE_METHODS = {
    "CSSetData": "0x060019a6",
    "CSHandleResponse": "0x060019a7",
}

EXPECTED_WORLD_MARCH_METHODS = {
    "GetMarchCurPosIndex": "0x06003208",
}


def extract(image):
    fields = {}
    methods = {}
    protocol = {}
    world_march = {}
    for type_index, definition in enumerate(image.tables.TypeDef.rows, 1):
        name = image.type_name("TypeDef", type_index)
        if name not in {"WorldMarchDataManager", "WorldGetRectMarchInfosMessage", "WorldMarch"}:
            continue
        if name == "WorldMarchDataManager":
            for ref in definition.FieldList:
                field = ref.row
                field_name = str(field.Name)
                if field_name in EXPECTED_FIELDS:
                    fields[field_name] = {
                        "token": f"0x{0x04000000 | ref.row_index:08x}",
                        "type": image.decode_field_signature(bytes(field.Signature.value)),
                    }
        for ref in definition.MethodList:
            method = ref.row
            method_name = str(method.Name)
            expected = (EXPECTED_METHODS if name == "WorldMarchDataManager" else
                        EXPECTED_MESSAGE_METHODS if name == "WorldGetRectMarchInfosMessage" else
                        EXPECTED_WORLD_MARCH_METHODS)
            if method_name not in expected:
                continue
            signature, arguments = image.decode_method_signature(bytes(method.Signature.value))
            target = (methods if name == "WorldMarchDataManager" else
                      protocol if name == "WorldGetRectMarchInfosMessage" else world_march)
            target[method_name] = {
                "token": f"0x{0x06000000 | ref.row_index:08x}",
                "returns": signature,
                "parameters": arguments,
            }
    for observed, expected in (
        (fields, EXPECTED_FIELDS), (methods, EXPECTED_METHODS),
        (protocol, EXPECTED_MESSAGE_METHODS), (world_march, EXPECTED_WORLD_MARCH_METHODS),
    ):
        if set(observed) != set(expected):
            raise ValueError(f"current build metadata has absent/extra march symbols: {set(expected) ^ set(observed)}")
        for name, token in expected.items():
            if observed[name]["token"] != token:
                raise ValueError(f"token changed for {name}: {observed[name]['token']} versus {token}")
    move = world_march["GetMarchCurPosIndex"]
    if move["returns"] != "int32" or move["parameters"]:
        raise ValueError("WorldMarch.GetMarchCurPosIndex signature changed")
    return {"fields": fields, "methods": methods, "protocolMethods": protocol,
            "worldMarchMethods": world_march}


def inspect(assembly: Path = DEFAULT):
    with assembly.open("rb") as handle:
        digest = hashlib.file_digest(handle, "sha256").hexdigest()
    if digest != SHA256:
        raise ValueError("installed assembly hash differs: no cross-version token claims")
    result = extract(MetadataImage.load(assembly))
    return {"assemblySha256": digest, "readOnly": True, **result}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assembly", type=Path, default=DEFAULT)
    args = parser.parse_args()
    print(json.dumps(inspect(args.assembly), indent=2, ensure_ascii=False))
