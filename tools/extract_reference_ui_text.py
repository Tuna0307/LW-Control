"""Extract display strings only; never evaluate or run the supplied UI script."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re

QUOTED = r'"(?:[^"\\]|\\.)*"'


def extract(source: str) -> dict[str, str]:
    result: dict[str, str] = {}
    # Explicit object pairs precede incidental inline wording. First occurrence
    # wins for generic labels; scoped feature labels are recorded separately.
    for pattern in (
        rf'zh:({QUOTED}),en:({QUOTED})',
        rf'zhDescription:({QUOTED}),enDescription:({QUOTED})',
        rf'==="zh"\?({QUOTED}):({QUOTED})',
    ):
        for chinese, english in re.findall(pattern, source):
            result.setdefault(json.loads(english), json.loads(chinese))
    # The same English caption can mean different things in different features.
    # Recover the exact owning feature's labels rather than flattening that context.
    for match in re.finditer(
        rf'([a-z_]+):\{{zh:({QUOTED}),en:({QUOTED}),zhDescription:({QUOTED}),enDescription:({QUOTED})\}}', source
    ):
        feature, zh_name, en_name, zh_description, en_description = match.groups()
        result[f"feature.{feature}.name.{json.loads(en_name)}"] = json.loads(zh_name)
        result[f"feature.{feature}.description.{json.loads(en_description)}"] = json.loads(zh_description)
    # Action arrays consist of flat display objects in this reference build.
    # Restrict the grammar and let the coverage check reject a changed layout.
    for match in re.finditer(r'([a-z_]+):\[((?:\{[^{}]*\},?)+)\]', source):
        feature, actions = match.groups()
        if not actions.startswith('{mode:'):
            continue
        for chinese, english in re.findall(rf'zh:({QUOTED}),en:({QUOTED})', actions):
            result[f"feature.{feature}.action.{json.loads(english)}"] = json.loads(chinese)
    return dict(sorted(result.items()))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    data = args.source.read_bytes()
    strings = extract(data.decode("utf-8"))
    if len(strings) < 100:
        raise ValueError("Expected reference display pairs were not found.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(strings, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"source_sha256": hashlib.sha256(data).hexdigest(), "strings": len(strings)}))


if __name__ == "__main__":
    main()
