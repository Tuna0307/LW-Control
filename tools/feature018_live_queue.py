"""Submit one operation to an already running isolated FEATURE018 proof.

No process startup, installed-script mutation, account selection or retries.
The packaged app independently checks UID/server/session before execution.
"""
import argparse
import json
import os
import time
import uuid
from pathlib import Path


def submit(directory, operation, timeout=45):
    directory = Path(directory).resolve(strict=True)
    if not directory.name.startswith("native-ui-"):
        raise ValueError("Expected an existing task-owned native UI receipt directory")
    admission = json.loads((directory / "feature018-account-admission.json").read_text("utf-8-sig"))
    if admission["uid"] != "3133546747002289" or admission["serverId"] != 2289:
        raise ValueError("Wrong game account admission")
    if (directory / "owned-exit.json").exists():
        raise ValueError("Session already exited")
    def same_owner(current):
        game = current.get("game", {})
        if any(game.get(key) != admission.get(key) for key in
               ("uid", "serverId", "profileId", "sessionId", "gamePid")):
            raise RuntimeError("Staged proof changed its admitted account/session; no submission or retry")

    operation = dict(operation, id=uuid.uuid4().hex)
    target = directory / "feature018-live-request.json"
    lock = directory / "feature018-live-submit.lock"
    try:
        descriptor = os.open(lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL)
    except FileExistsError as error:
        raise RuntimeError("Another queue submitter may be active; existing queue untouched") from error
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump({"processId": os.getpid(), "operationId": operation["id"]}, stream)
        result_path = directory / "native-ui-map.json"
        current = json.loads(result_path.read_text("utf-8-sig"))
        same_owner(current)
        if current.get("state") != "running" or (directory / "owned-exit.json").exists():
            raise RuntimeError("Staged proof is not running; existing queue untouched")
        if target.exists():
            previous = json.loads(target.read_text("utf-8-sig"))
            if not any(x.get("id") == previous.get("id") for x in current.get("operations", [])):
                raise RuntimeError("Previous one-shot request has no receipt; existing queue untouched, never resend")
        temporary = directory / ("feature018-live-request-" + operation["id"] + ".tmp")
        temporary.write_text(json.dumps(operation), encoding="utf-8")
        temporary.replace(target)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                current = json.loads(result_path.read_text("utf-8-sig"))
            except (OSError, json.JSONDecodeError):
                time.sleep(0.25)
                continue
            same_owner(current)
            found = next((x for x in current.get("operations", []) if x.get("id") == operation["id"]), None)
            if found is not None:
                return found
            if current.get("state") != "running" or (directory / "owned-exit.json").exists():
                raise RuntimeError("Owned session stopped without a receipt for " + operation["id"] + "; never resend")
            time.sleep(0.25)
        raise TimeoutError("No receipt for one-shot request " + operation["id"] + "; never resend automatically")
    finally:
        lock.unlink()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("operation", help="JSON operation; command, key, tab, capture, equipmentConfig or finish")
    args = parser.parse_args()
    print(json.dumps(submit(args.directory, json.loads(args.operation)), ensure_ascii=False))
