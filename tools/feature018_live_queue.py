"""Submit one operation to an already running isolated FEATURE018 proof.

No process startup, installed-script mutation, account selection or retries.
The packaged app independently checks UID/server/session before execution.
"""
import argparse
import json
import time
import uuid
from pathlib import Path


def submit(directory, operation, timeout=45):
    directory = Path(directory).resolve(strict=True)
    if not directory.name.startswith("native-ui-"):
        raise ValueError("Expected an existing task-owned native UI receipt directory")
    admission = json.loads((directory / "feature018-account-admission.json").read_text("utf-8"))
    if admission["uid"] != "3133546747002289" or admission["serverId"] != 2289:
        raise ValueError("Wrong game account admission")
    if (directory / "owned-exit.json").exists():
        raise ValueError("Session already exited")
    operation = dict(operation, id=uuid.uuid4().hex)
    target = directory / "feature018-live-request.json"
    temporary = target.with_suffix(".tmp")
    temporary.write_text(json.dumps(operation), encoding="utf-8")
    temporary.replace(target)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            current = json.loads((directory / "native-ui-map.json").read_text("utf-8"))
            found = next((x for x in current.get("operations", []) if x["id"] == operation["id"]), None)
            if found is not None:
                return found
        except (OSError, ValueError):
            pass
        time.sleep(0.25)
    raise TimeoutError("No receipt for one-shot request " + operation["id"] + "; never resend automatically")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("operation", help="JSON operation; command, key, tab, capture, equipmentConfig or finish")
    args = parser.parse_args()
    print(json.dumps(submit(args.directory, json.loads(args.operation)), ensure_ascii=False))
