"""Controlled one-shot approved 2288 shield and exact Land Cell staged receipts.

Only the caller's existing task-owned packaged app may act. Every request has a
separate native response. Never retry a sent purchase, ItemUse or land unlock.
"""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import time

from feature018_live_queue import submit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--stage", required=True, choices=("shield8", "land", "shieldUseOwned"))
    args = parser.parse_args()
    directory = args.directory.resolve(strict=True)
    output = directory / ("feature018-live-" + args.stage + ".json")
    if output.exists():
        raise FileExistsError("This one-shot live stage was already started; do not repeat")
    log = {"schemaVersion": 1, "stage": args.stage, "state": "running",
           "startedAtUtc": datetime.now(timezone.utc).isoformat(), "operations": []}

    def save():
        output.write_text(json.dumps(log, indent=2, ensure_ascii=False), encoding="utf-8")

    def call(label, operation):
        entry = {"label": label, "request": operation, "status": "prepared"}
        log["operations"].append(entry)
        save()
        try:
            receipt = submit(directory, operation, timeout=35)
        except Exception as error:
            entry["status"] = "uncertain_no_retry"
            entry["error"] = str(error)
            save()
            raise
        entry["status"] = receipt.get("state")
        entry["receipt"] = receipt
        save()
        if receipt.get("state") != "returned":
            raise RuntimeError(f"{label}: {receipt.get('code')} {receipt.get('error')}")
        return receipt.get("result", {})

    def command(label, name, payload):
        return call(label, {"kind": "command", "command": name, "payload": payload})

    try:
        if args.stage == "shield8":
            before = command("beforeShop8", "feature018_shield_shop", {"mode": "inspect", "slot": 8})
            price, balance = before.get("nativePrice"), before.get("gold")
            if (before.get("readOnly") is not True or before.get("durationHours") != 8 or
                not isinstance(price, int) or price <= 0 or
                not isinstance(balance, int) or balance < price or
                before.get("count") != 0 or not before.get("itemId")):
                raise RuntimeError("Native shield-8 price/balance/empty stock precondition changed")
            log["consentedInGameGemAmount"] = price
            save()
            bought = command("oneShield8Purchase", "feature018_shield_shop",
                             {"mode": "buy", "slot": 8, "gemOptIn": True})
            if bought.get("requestDispatched") is not True:
                raise RuntimeError("No native purchase dispatch; no second attempt")
            log["purchaseSubmittedOnce"] = True
            save()
            confirmed = None
            for ordinal in range(8):
                state = command(f"shieldPurchaseStatus{ordinal}", "feature018_shield_shop",
                                {"mode": "status", "slot": 8})
                if state.get("purchasedCount") == 1 and state.get("spentGems") == price and \
                   state.get("gold") == balance-price and state.get("count") == 1:
                    confirmed = state
                    break
                time.sleep(.35)
            if confirmed is None:
                raise RuntimeError("Shield purchase pending or unconfirmed; ItemUse withheld")
            log["purchaseConfirmedByBalanceAndInventory"] = True
            save()
            inventory = command("ownedShieldAfterPurchase", "feature018_shield_inventory_get", {})
            if not any(row.get("durationHours") == 8 for row in inventory.get("items", [])):
                raise RuntimeError("Native inventory has no 8-hour shield; ItemUse withheld")
            # A physical F6 key is attempted only through exact game HWND and
            # game-side text-focus gating. If it is delivered, never issue a
            # separate direct use, even if its ACK is still pending.
            key = call("originalF6Key", {"kind": "key", "binding": "F6"})
            pressed = key.get("delivered", {}).get("Pressed") or \
                key.get("delivered", {}).get("pressed")
            log["f6PressedOnExactWindow"] = bool(pressed)
            save()
            if not pressed:
                action = command("nativeShield8ApplyAfterUnsentF6", "feature018_hotkey_action",
                                 {"action": "shieldUse", "slot": 8})
                log["directUseDispatched"] = action.get("requestDispatched") is True
                save()
            # Query-only can never transmit another ItemUse.
            for ordinal in range(8):
                try:
                    observed = command(f"shieldUseStatus{ordinal}", "feature018_hotkey_action",
                                       {"action": "shieldUse", "slot": 8, "queryOnly": True})
                except RuntimeError:
                    break
                if observed.get("observation") == "native_owned_shield_consumption_and_MAIN_SHIELD":
                    log["shieldUseConfirmed"] = True
                    save()
                    break
                time.sleep(.35)
            log["finalShieldInventory"] = command("finalShieldInventory", "feature018_shield_inventory_get", {})
        elif args.stage == "shieldUseOwned":
            before = command("shopPreviouslyPurchased", "feature018_shield_shop",
                             {"mode": "inspect", "slot": 8})
            if (before.get("readOnly") is not True or before.get("itemId") != "200410" or
                before.get("count") != 1 or before.get("gold") != 176):
                raise RuntimeError("The previously confirmed 8h purchase is no longer in its expected state")
            native_stacks = before.get("nativeMatchedShieldStacks", [])
            if len(native_stacks) != 1 or native_stacks[0].get("validItemUseUuid") is not True:
                raise RuntimeError("Native ItemUse UTF UUID still unavailable; no item use sent")
            inventory = command("ownedShieldReady", "feature018_shield_inventory_get", {})
            matches = [row for row in inventory.get("items", []) if row.get("itemId") == "200410" and
                       row.get("durationHours") == 8 and row.get("count") == 1 and
                       row.get("uniqueDuration") is True]
            if len(matches) != 1 or matches[0].get("uuid") != native_stacks[0].get("uuidText"):
                raise RuntimeError("Native owned 8h ItemUse token and exact duration mismatch")
            pressed = call("physicalF6OnTaskGame", {"kind": "key", "binding": "F6"})
            delivered = pressed.get("delivered", {})
            if delivered.get("pressed") is True:
                log["physicalF6Pressed"] = True
            elif delivered.get("pressed") is False:
                log["physicalF6Pressed"] = False
                result = command("singleNativeItemUseAfterDeniedF6", "feature018_hotkey_action",
                                 {"action": "shieldUse", "slot": 8})
                if result.get("requestDispatched") is not True:
                    raise RuntimeError("Native shield did not dispatch ItemUse; no retry")
                log["nativeItemUseSentOnce"] = True
            else:
                raise RuntimeError("Physical F6 activation status unknown; native ItemUse withheld")
            save()
            for ordinal in range(15):
                observed = command(f"nativeItemUseStatus{ordinal}", "feature018_hotkey_action",
                                   {"action": "shieldUse", "slot": 8, "queryOnly": True})
                if observed.get("observation") == "native_owned_shield_consumption_and_MAIN_SHIELD":
                    log["shieldUseConfirmed"] = True
                    log["confirmedEffect"] = observed
                    break
                time.sleep(.35)
            log["finalShieldInventory"] = command("finalShieldInventory", "feature018_shield_inventory_get", {})
            log["finalShop8"] = command("finalShop8", "feature018_shield_shop",
                                        {"mode": "inspect", "slot": 8})
            save()
        elif args.stage == "land":
            current = command("beforeCurrentLand", "mini_land_status", {})
            cell_id = current.get("cellId")
            if (current.get("state") != "locked" or not isinstance(cell_id, int) or
                cell_id < 1 or cell_id > 10000 or cell_id == 5):
                raise RuntimeError("No fresh native locked Land Cell distinct from earlier cell 5")
            before = command("beforeExactLand", "mini_land_status", {"expectedCellId": cell_id})
            if before.get("state") != "locked" or before.get("exactCellLookup") is not True:
                raise RuntimeError("Exact native Land Cell is no longer locked")
            log["exactTargetCellId"] = cell_id
            save()
            sent = command("oneLandUnlock", "monopoly_cell_open", {"expectedCellId": cell_id})
            if sent.get("requestDispatched") is not True or sent.get("cellId") != cell_id:
                raise RuntimeError("Land unlock has no single matched native dispatch; never resend")
            log["landRequestSubmittedOnce"] = True
            save()
            for ordinal in range(12):
                after = command(f"landExactAfter{ordinal}", "mini_land_status",
                                {"expectedCellId": cell_id})
                request = after.get("requestProof") or {}
                if (after.get("state") == "unlocked" and
                    request.get("requestCausalityConfirmed") is True):
                    log["landCausalityConfirmed"] = True
                    save()
                    break
                time.sleep(.35)
            log["landLastNativeState"] = after
            save()
    except Exception as error:
        log["state"] = "partial"
        log["error"] = str(error)
        print(f"FEATURE018_{args.stage}_PARTIAL: {error}", flush=True)
    else:
        log["state"] = "returned"
    log["endedAtUtc"] = datetime.now(timezone.utc).isoformat()
    save()
    print(output, flush=True)


if __name__ == "__main__":
    main()
