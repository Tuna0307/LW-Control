#!/usr/bin/env python3
"""Recover the current Last War PointInfo identity surface from Assembly-CSharp.rdl.

This inspector is intentionally narrow. It hash-gates the current build-1078
managed artifact, reuses the repository's read-only RG/RGMD parser, and records
only directly attributable PointInfo / ResPointInfo / xLua wrapper contracts.
Modified source-call tokens inside constructors remain raw tokens rather than
being assigned guessed protobuf getter names.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

from inspect_lastwar_rdl_metadata import MetadataImage


EXPECTED_SHA256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd"
EXPECTED_SIZE = 15_640_576


class InspectError(ValueError):
    pass


def _exact_type(image: MetadataImage, full_name: str):
    matches = []
    for index, row in enumerate(image.tables.TypeDef.rows, 1):
        if image.type_name("TypeDef", index) == full_name:
            matches.append((index, row))
    if len(matches) != 1:
        raise InspectError(f"expected one exact type {full_name!r}, found {len(matches)}")
    return matches[0]


def _field(image: MetadataImage, typedef, name: str) -> dict[str, object]:
    matches = [ref for ref in typedef.FieldList if str(ref.row.Name) == name]
    if len(matches) != 1:
        raise InspectError(f"expected one field {name!r}, found {len(matches)}")
    ref = matches[0]
    return {
        "row": ref.row_index,
        "type": image.decode_field_signature(bytes(ref.row.Signature.value)),
        "name": name,
    }


def _method(
    image: MetadataImage,
    typedef,
    name: str,
    expected_args: list[str] | None = None,
) -> dict[str, object]:
    matches = []
    for ref in typedef.MethodList:
        if str(ref.row.Name) != name:
            continue
        ret, args = image.decode_method_signature(bytes(ref.row.Signature.value))
        if expected_args is not None and args != expected_args:
            continue
        matches.append((ref, ret, args))
    if len(matches) != 1:
        raise InspectError(
            f"expected one method {name!r} args={expected_args!r}, found {len(matches)}"
        )
    ref, ret, args = matches[0]
    return {
        "row": ref.row_index,
        "rva": f"0x{int(ref.row.Rva):x}",
        "returnType": ret,
        "argumentTypes": args,
        "il": image.disassemble(ref.row),
    }


def _single_field_token(method: dict[str, object]) -> str:
    tokens = []
    for line in method["il"]:
        match = re.search(r"\bldfld token\((0x[0-9A-Fa-f]+)\)", str(line))
        if match:
            tokens.append(match.group(1).upper().replace("0X", "0x"))
    if len(tokens) != 1:
        raise InspectError(f"expected one ldfld token, found {tokens!r}")
    return tokens[0]


def _stored_field_tokens(method: dict[str, object]) -> list[str]:
    tokens = []
    for line in method["il"]:
        match = re.search(r"\bstfld token\((0x[0-9A-Fa-f]+)\)", str(line))
        if match:
            tokens.append(match.group(1).upper().replace("0X", "0x"))
    return tokens


def inspect(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported Assembly-CSharp.rdl SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )
    if len(data) != EXPECTED_SIZE:
        raise InspectError(
            f"unsupported Assembly-CSharp.rdl size {len(data)}; expected {EXPECTED_SIZE}"
        )

    image = MetadataImage.load(path)
    point_type_row, point_type = _exact_type(image, "PointInfo")
    res_type_row, res_type = _exact_type(image, "ResPointInfo")
    wrap_type_row, wrap_type = _exact_type(image, "XLua.CSObjectWrap.PointInfoWrap")

    point_fields = {
        name: _field(image, point_type, name)
        for name in ("pointIndex", "mainIndex", "uuid")
    }
    wrapper_methods = {
        "pointIndex": _method(image, wrap_type, "_g_get_pointIndex", ["native int"]),
        "mainIndex": _method(image, wrap_type, "_g_get_mainIndex", ["native int"]),
        "uuid": _method(image, wrap_type, "_g_get_uuid", ["native int"]),
    }
    field_tokens = {
        name: _single_field_token(method) for name, method in wrapper_methods.items()
    }

    is_main = _method(image, point_type, "get_isMainPoint", [])
    is_main_tokens = [
        match.group(1).upper().replace("0X", "0x")
        for line in is_main["il"]
        if (match := re.search(r"\bldfld token\((0x[0-9A-Fa-f]+)\)", str(line)))
    ]
    expected_comparison = [field_tokens["mainIndex"], field_tokens["pointIndex"]]
    if is_main_tokens != expected_comparison or not any(
        " ceq" in f" {line}" for line in is_main["il"]
    ):
        raise InspectError(
            "PointInfo.get_isMainPoint no longer compares mainIndex and pointIndex as expected"
        )

    point_ctor = _method(image, point_type, ".ctor", ["Protobuf.WorldPointInfo"])
    ctor_store_tokens = _stored_field_tokens(point_ctor)
    for name, token in field_tokens.items():
        if token not in ctor_store_tokens:
            raise InspectError(f"PointInfo(WorldPointInfo) no longer stores {name} token {token}")

    res_fields = {
        "gatherMarchUuid": _field(image, res_type, "gatherMarchUuid"),
    }
    res_methods = {
        "constructor": _method(image, res_type, ".ctor", ["Protobuf.WorldPointInfo"]),
        "getResType": _method(image, res_type, "GetResType", []),
        "getResLevel": _method(image, res_type, "GetResLevel", []),
    }

    return {
        "finding": "LWB-R6-050",
        "date": "2026-09-10",
        "status": "RECOVERED current-client managed/xLua identity surface; capture-to-builder mapping UNKNOWN/BLOCKED",
        "source": {
            "path": "%LOCALAPPDATA%\\FunFly\\Last War-Survival Game\\Game\\LastWar_Data\\Assemblies\\Assembly-CSharp.rdl",
            "build": "1.0.361 / 1078",
            "sha256": digest,
            "size": len(data),
            "rgmdOffset": f"0x{image.root_offset:x}",
            "typeDefs": len(image.tables.TypeDef.rows),
            "methodDefs": len(image.tables.MethodDef.rows),
        },
        "pointInfo": {
            "typeRow": point_type_row,
            "fields": point_fields,
            "xLuaWrapperTypeRow": wrap_type_row,
            "xLuaGetters": wrapper_methods,
            "backingFieldTokens": field_tokens,
            "isMainPoint": is_main,
            "worldPointConstructor": point_ctor,
            "constructorStoredFieldTokens": ctor_store_tokens,
            "constructorSourceGetterMapping": "UNKNOWN/BLOCKED: modified call tokens are preserved raw and are not assigned protobuf getter names",
        },
        "resourcePoint": {
            "typeRow": res_type_row,
            "base": "PointInfo",
            "fields": res_fields,
            "methods": res_methods,
        },
        "conclusion": (
            "The current client exposes pointIndex, mainIndex, and uuid as distinct PointInfo fields through generated xLua getters. "
            "PointInfo.isMainPoint directly compares mainIndex with pointIndex, and PointInfo(WorldPointInfo) stores all three fields. "
            "ResPointInfo separately defines gatherMarchUuid plus resource type/level getters."
        ),
        "limits": [
            "The modified call tokens feeding PointInfo(WorldPointInfo) are not mapped to named protobuf getters by this finding.",
            "No native capture/serializer owner is recovered from these managed/xLua getters.",
            "No direct dataflow from current-client fields into the original R6-049 normalized-record builder is proven.",
            "No production ingestion, bridge readiness, real scan completion, or displayed live result is enabled.",
        ],
        "next": (
            "Use the proven current field names/types as capture-side anchors while tracing a permitted serializer/provider path into the "
            "R6-049 builder; keep unsupported source-getter and native ownership mappings fail-closed."
        ),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rdl", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.rdl)
    except (InspectError, OSError, ValueError) as exc:
        parser.error(str(exc))
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
