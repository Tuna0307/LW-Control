"""Update affected rows of 47-row R1 Home audit, keeping source locators."""
from pathlib import Path
path = Path(__file__).resolve().parents[1] / "docs" / "HOME_004_R1_CURRENT_MATRIX.md"
text = path.read_text(encoding="utf-8")
updated = {
    "H-26": ("CONTROLLED_R2: held obsolete termination after Stop/successor Start cannot stop successor; exact session ownership", "Live late-response race not intentionally induced; generation fence remains adaptation"),
    "H-32": ("PACKAGED_R2_LIVE: OFF real owned exit no relaunch; ON triggers two automatic native relaunches to authenticated Connected; user Stop clears desired", "No original protected service witness; real network-only disconnect remains separate"),
    "H-33": ("PACKAGED_R2_LIVE + CONTROLLED: persisted OFF/ON affects actual recovery, in-flight disable stops run at end of current iteration", "Forwarded game-side setAutomation and encrypted Lua remain missing local/unknown original"),
    "H-34": ("PACKAGED_R2_LIVE + CONTROLLED: real owned process exit OFF no relaunch, ON two automatic recoveries; native 2 miss, 30s hang, 60s disconnect, 180s login boundaries pass", "Long timed real hang/disconnect/login-unavailable waits not witnessed; no live 180s claim"),
    "H-35": ("PACKAGED_R2_LIVE + CONTROLLED: two native recoveries reach Connected and stable Home running, 15s stable and first 15s failed helper retry tested", "Live repeated failed retry ladder, maintenance log and 15min update stall not executed"),
    "H-36": ("PACKAGED_R2_LIVE: reproduced stale protected adoption causing fresh recovery rollback; corrected exact-old-owner adoption and registry retire after helper restoration; real repeat automatic launch/connected succeeds and Stop restores", "Original encrypted updater/log producer parity still unknown; current-client helper adaptation remains"),
    "H-37": ("PACKAGED_R2_LIVE: OFF exit retains persisted desired true; ON recovers; actual Home Stop clears desired false and no relaunch", "Reboot with persisted desired/no tracked PID still separate"),
    "H-40": ("CONTROLLED_R2 + PACKAGED: Stop invalidates current run; late old effect cannot damage new owner; real Home Stop while ON prevents relaunch", "Concurrent native profile replacement and live old late completion unexecuted"),
    "H-42": ("PACKAGED_R2_LIVE: actual Home stopped OFF, launching/verifying ON, Connected with changed PID, stopped after Stop", "Full original 12-field instance status and protected lease fields remain partial"),
    "H-43": ("PACKAGED_R2_LIVE + CONTROLLED: waiting, launching, verifying, succeeded/idle and Stop reset pass production paths and actual Home conditional UI", "Original maintenance and full error notices not live witnessed"),
    "H-45": ("PACKAGED_R2_LIVE: corrected native Home Chinese/light and Japanese/dark Connected, recovery launch, stable verification, stopped and user Stop", "Original conditional error/busy locale pairing remains unproved"),
    "H-47": ("PACKAGED_R2_LIVE + CONTROLLED: real user Stop prevents further relaunch after two ON recovery cycles, late original effect cannot mutate successor", "Real overlapping launch/Stop while helper in flight remains unexecuted"),
}
lines = text.splitlines()
counts = {key: 0 for key in updated}
for i, line in enumerate(lines):
    for key, (status, next_action) in updated.items():
        if line.startswith("| " + key + " "):
            parts = line.split(" | ")
            if len(parts) != 5:
                raise RuntimeError("Bad matrix structure: " + key)
            parts[-2] = status
            parts[-1] = next_action + " |"
            lines[i] = " | ".join(parts)
            counts[key] += 1
assert all(value == 1 for value in counts.values()), counts
assert sum(line.startswith("| H-") for line in lines) == 47
header = "## R2 actual owned-game recovery continuation"
if header not in text:
    lines.extend(["", header, "",
        "Affected rows above supersede their R1 proof classifications. Original locators, paths and all 47 IDs remain. "
        "Proof: [R2 native witness](proof/HOME-004-R2-NATIVE-2026-10-09.md) and HomeR2RecoveryChecks.cs. "
        "The first failed package and corrected live witness have separate ignored task roots; no failed evidence was overwritten.", "",
        "This R2 unit proves OFF no relaunch after exact unexpected owned-process exit, ON native automatic recovery, "
        "two successful repeat ON relaunches to distinct authenticated Connected processes, stable 15-second confirmation, "
        "user Stop/no relaunch, restoration and cleanup. It does not certify all original licensed runtime, "
        "multi-profile, encrypted game-side Lua, maintenance, timed live network/hang or updater paths. Whole Home PARTIAL."])
path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("HOME004_R2_MATRIX_OK updated=%s total=47" % len(updated))
