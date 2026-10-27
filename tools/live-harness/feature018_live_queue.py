"""Submit one operation to an already running isolated FEATURE018 proof.

No process startup, installed-script mutation, account selection or gameplay retries.
The packaged app independently checks UID/server/session before execution.
"""
import argparse
import json
import os
import time
import uuid
from pathlib import Path


def submit(directory, operation, timeout=45, *, expected_uid="4799432499002212", expected_server=2212, gather_only=False):
    # Proof-only authority: explicit temporary account selection never accepts
    # an arbitrary UID, and server alone can never authorize the owner's main.
    # Explicit per-run owner authorization can extend ONLY the gathering proof
    # to another exact account. The default authority remains unchanged.
    if gather_only:
        allowed = (operation.get('kind') == 'finish' or
            operation.get('kind') == 'r23Ui' and operation.get('surface') == 'r30-resourceGather' or
            operation.get('kind') == 'command' and operation.get('command') in
                ('feature018_automation_observe', 'feature018_automation_status', 'feature018_automation_stop') and
                operation.get('payload', {}).get('kind', 'resourceGather') == 'resourceGather')
        if not allowed or not str(expected_uid).isdigit() or str(expected_uid).startswith('0') or expected_server <= 0:
            raise ValueError('Owner-authorized gathering proof scope exceeded')
    if not gather_only and (expected_uid, expected_server) != ("4799432499002212", 2212):
        raise ValueError("Account is outside the owner-authorized proof pairs")
    if (str(operation.get("command", "")).startswith("mini_land_") or
            operation.get("command") == "monopoly_cell_open"):
        raise ValueError("R14 forbids all live Land Cell requests")
    if operation.get("kind") == "key" and operation.get("binding") not in (
            "Q", "W", "E", "R", "A", "S", "D", "F", "F9", "F10"):
        raise ValueError("Key is outside the R14 live assignment")
    directory = Path(directory).resolve(strict=True)
    if not directory.name.startswith("native-ui-"):
        raise ValueError("Expected an existing task-owned native UI receipt directory")
    admission = json.loads((directory / "feature018-account-admission.json").read_text("utf-8-sig"))
    if (admission.get("serverId") != expected_server or
            admission.get("uid") != expected_uid or
            not admission.get("loggedIn") or not admission.get("connected")):
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
        # Windows may briefly deny replacement while the app reads the OLD
        # request file. Retry only atomic publication of this same request ID;
        # never create another operation or replay an ambiguous game command.
        for attempt in range(5):
            try:
                temporary.replace(target)
                break
            except PermissionError:
                if not temporary.exists():
                    published = json.loads(target.read_text("utf-8-sig"))
                    if published.get("id") == operation["id"]:
                        break
                    raise
                if attempt == 4:
                    raise
                fresh = json.loads(result_path.read_text("utf-8-sig"))
                same_owner(fresh)
                if fresh.get("state") != "running" or (directory / "owned-exit.json").exists():
                    raise RuntimeError("Owner stopped before request publication; nothing resubmitted")
                time.sleep(0.1)
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
    parser.add_argument("operation", help="JSON operation; command, key, observedKeys, show, tab, capture, equipmentConfig or finish")
    args = parser.parse_args()
    print(json.dumps(submit(args.directory, json.loads(args.operation)), ensure_ascii=False))
