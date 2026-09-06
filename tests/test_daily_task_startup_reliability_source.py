from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "tools" / "run_daily_task_startup_reliability.py").read_text(encoding="utf-8")


def test_startup_reliability_probe_is_claim_free_and_fail_closed() -> None:
    assert 'command_path = runtime / "daily-task-command.txt"' in SOURCE
    assert "refusing a startup-only check" in SOURCE
    assert '"commandsCreated": 0' in SOURCE
    assert '"rewardClaimSends": 0' in SOURCE
    assert "WriteCommand" not in SOURCE
    assert "run-once" not in SOURCE
    assert "DailyTaskReward" not in SOURCE
    assert "DailyQuestReward" not in SOURCE


def test_startup_reliability_probe_requires_hash_stability_and_fresh_registration() -> None:
    assert "installed game hashes changed" in SOURCE
    assert 'EXPECTED_VERSION = "lwcontrol-daily-task-runtime-1"' in SOURCE
    assert '"UpdateManager.AddUpdate"' in SOURCE
    assert "updated_at > before_updated_at" in SOURCE
