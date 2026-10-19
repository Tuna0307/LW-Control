"""One-shot background R22 rapid native-target attack proof; never physical input."""
from pathlib import Path
import argparse
import json
import time

from feature018_live_queue import submit


PROOF_UID = "4799432499002212"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    root = parser.parse_args().directory.resolve(strict=True)
    target = root / "r22-rapid-attack-proof.json"
    if target.exists():
        raise FileExistsError("Preserve this one-shot receipt; never replay automatically")

    evidence = {"state": "running", "operations": [], "orders": []}

    def save():
        target.write_text(json.dumps(evidence, indent=2), encoding="utf-8")

    def op(command, payload, kind="command", timeout=35):
        if command == "feature018_hotkey_action":
            payload = dict(payload, authorizedUid=PROOF_UID)
        result = submit(root, {"kind": kind, "command": command, "payload": payload}, timeout=timeout)
        evidence["operations"].append(result)
        save()
        if result.get("code") == "GAME_ACTION_UNCONFIRMED":
            return {"state": "unconfirmed", "error": result.get("error"),
                    "details": result.get("details", {})}
        if result.get("state") != "returned":
            return {"state": "failed", "error": result.get("error"),
                    "code": result.get("code"), "details": result.get("details")}
        return result["result"]

    def inventory():
        return op("feature018_attack_prepare", {"mode": "inventory"})

    def query(slot):
        return op("feature018_hotkey_action", {"action": "attack", "slot": slot, "queryOnly": True})

    def settle(slot, limit=30):
        last = None
        for _ in range(limit):
            last = query(slot)
            if last.get("state") == "confirmed" or last.get("serverAcknowledgementCorrelated") or \
                    last.get("serverAppliedChangePostState"):
                return last
            if last.get("state") == "failed":
                return last
            time.sleep(.12)
        return last or {"state": "failed", "error": "No native observation returned"}

    def current_owned_march(snapshot, slot):
        squad = snapshot.get("squads", {}).get(str(slot), {})
        return squad.get("marchUuid")

    try:
        # Same proven native monster area used by R21. Targets come only from
        # the game's fresh ordinary-monster inventory; no cursor/desktop input
        # or caller-authored target point is used.
        op("map_coordinate_jump", {"x": 199, "y": 572, "serverId": 2212}, "mapCommand")
        snapshot = None
        candidates = []
        for _ in range(40):
            snapshot = inventory()
            candidates = [c for c in snapshot.get("candidates", [])
                          if 3 <= c.get("level", -1) <= 9]
            if len({c.get("uuid") for c in candidates}) >= 3:
                break
            time.sleep(.2)
        else:
            raise RuntimeError("Three distinct eligible ordinary native monsters unavailable")

        # Prefer an unlocked idle squad, but retain the production redirect path
        # if an exact owned march is already out. The proof never attacks a
        # player, so the guildless-player boundary is not exercised or relaxed.
        unlocked = [int(k) for k, value in snapshot.get("squads", {}).items()
                    if value.get("unlocked")]
        if not unlocked:
            raise RuntimeError("No unlocked native squad available")
        slot = next((s for s in unlocked if not current_owned_march(snapshot, s)), unlocked[0])
        candidates = sorted(candidates, key=lambda c: (c.get("level", 999), str(c.get("uuid"))))
        unique = []
        seen = set()
        for candidate in candidates:
            uuid = str(candidate.get("uuid"))
            if uuid and uuid not in seen:
                seen.add(uuid)
                unique.append(candidate)
            if len(unique) == 3:
                break
        if len(unique) != 3:
            raise RuntimeError("Native inventory did not expose three distinct target UUIDs")
        evidence["slot"] = slot
        evidence["targets"] = unique
        save()

        for index, candidate in enumerate(unique, 1):
            target_uuid = str(candidate["uuid"])
            opened = op("feature018_attack_prepare", {"mode": "open", "targetUuid": target_uuid})
            # Keep presses close together; only the production one-send fence may
            # delay a later order. We never resend an ambiguous operation ID.
            started = time.monotonic()
            result = op("feature018_hotkey_action", {"action": "attack", "slot": slot})
            elapsed_ms = round((time.monotonic() - started) * 1000)
            order = {"index": index, "targetUuid": target_uuid, "open": opened,
                     "initial": result, "elapsedMs": elapsed_ms}

            # A fresh order may explicitly refuse while a prior one-send packet
            # remains unconfirmed. That is a valid R22 outcome; never retry it.
            error_text = str(result.get("error") or "")
            if result.get("state") in ("failed", "unconfirmed"):
                detail_text = json.dumps(result.get("details", {}), sort_keys=True)
                specific = ("awaiting confirmation" in error_text or
                            "observe retained transaction" in error_text or
                            "already heading" in error_text.lower() or
                            (result.get("state") == "unconfirmed" and bool(result.get("details"))))
                if not specific:
                    raise RuntimeError("Rapid order %d failed without a specific safe reason: %s" %
                                       (index, error_text + " / " + detail_text))
                order["classification"] = "specific-safe-refusal"
            else:
                order["classification"] = "accepted"
                if result.get("requestDispatched") or result.get("awaitingGameConfirmation"):
                    order["settled"] = settle(slot)
            evidence["orders"].append(order)
            save()
            time.sleep(.08)

        # One final read-only inventory + bounded cleanup recall if an exact
        # owned march exists. Recall is a separate one-shot native command.
        final_inventory = inventory()
        evidence["afterOrders"] = final_inventory
        march = current_owned_march(final_inventory, slot)
        if march:
            recall = op("feature018_hotkey_action", {"action": "recall", "slot": slot})
            evidence["cleanupRecall"] = recall
            if recall.get("requestDispatched") or recall.get("awaitingGameConfirmation"):
                for _ in range(30):
                    follow = op("feature018_hotkey_action",
                                {"action": "recall", "slot": slot, "queryOnly": True})
                    evidence["cleanupRecallFinal"] = follow
                    save()
                    if follow.get("state") == "confirmed" or follow.get("nativePostStateObserved"):
                        break
                    if follow.get("state") == "failed":
                        break
                    time.sleep(.15)

        if len(evidence["orders"]) != 3 or len({x["targetUuid"] for x in evidence["orders"]}) != 3:
            raise RuntimeError("R22 proof did not preserve three distinct fresh target intents")
        evidence["state"] = "proven"
        save()
        print("R22_RAPID_THREE_NATIVE_TARGET_ORDERS_PROVEN", slot)
    except BaseException as error:
        evidence["state"] = "failed"
        evidence["error"] = str(error)
        save()
        raise


if __name__ == "__main__":
    main()
