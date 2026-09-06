import unittest

from tools.run_daily_task_claim_probe import verify_effect


def _snapshot(capture_id: str, captured_at: str, *, stage_state: str, received: list[int], task_state: str = "NoComplete"):
    return {
        "captureId": capture_id,
        "capturedAt": captured_at,
        "receivedStages": received,
        "boxes": [{"index": 1, "state": stage_state}],
        "tasks": [{"taskId": "101", "state": task_state}],
    }


class DailyTaskClaimRunnerChecks(unittest.TestCase):
    def test_chest_accepts_authoritative_post_refresh_when_direct_response_has_empty_stage_array(self):
        before = _snapshot("before-1", "2026-09-06T04:36:09Z", stage_state="CanReceive", received=[])
        after = _snapshot("after-1", "2026-09-06T04:38:18Z", stage_state="Received", received=[1])
        claim = {
            "state": "claimed",
            "sendCount": 1,
            "verificationMode": "fresh_daily_quest_list_state",
            "postRefreshRequested": True,
            "postRefreshObserved": True,
            "responseObserved": True,
            "responseCorrelated": False,
            "responseHadError": False,
            "handlerOk": True,
            "effectConfirmed": True,
            "target": {"kind": "DailyQuestStage", "stage": 1, "captureId": "before-1"},
            "beforeCaptureId": "before-1",
            "afterCaptureId": "after-1",
        }
        ok, reason = verify_effect(before, after, claim)
        self.assertTrue(ok, reason)

    def test_task_accepts_authoritative_post_refresh_even_when_reward_handler_was_not_observed(self):
        before = _snapshot(
            "before-2", "2026-09-06T04:12:15Z", stage_state="NoComplete", received=[], task_state="CanReceive"
        )
        after = _snapshot(
            "after-2", "2026-09-06T04:14:28Z", stage_state="CanReceive", received=[], task_state="Received"
        )
        claim = {
            "state": "claimed",
            "sendCount": 1,
            "verificationMode": "fresh_daily_quest_list_state",
            "postRefreshRequested": True,
            "postRefreshObserved": True,
            "responseObserved": False,
            "responseCorrelated": False,
            "responseHadError": False,
            "effectConfirmed": True,
            "target": {"kind": "DailyTask", "taskId": "101", "captureId": "before-2"},
            "beforeCaptureId": "before-2",
            "afterCaptureId": "after-2",
        }
        ok, reason = verify_effect(before, after, claim)
        self.assertTrue(ok, reason)

    def test_observed_response_error_still_fails_closed(self):
        before = _snapshot("before-3", "2026-09-06T04:36:09Z", stage_state="CanReceive", received=[])
        after = _snapshot("after-3", "2026-09-06T04:38:18Z", stage_state="Received", received=[1])
        claim = {
            "state": "claimed",
            "sendCount": 1,
            "verificationMode": "fresh_daily_quest_list_state",
            "postRefreshRequested": True,
            "postRefreshObserved": True,
            "responseObserved": True,
            "responseHadError": True,
            "handlerOk": True,
            "effectConfirmed": True,
            "target": {"kind": "DailyQuestStage", "stage": 1, "captureId": "before-3"},
            "beforeCaptureId": "before-3",
            "afterCaptureId": "after-3",
        }
        ok, _ = verify_effect(before, after, claim)
        self.assertFalse(ok)


if __name__ == "__main__":
    unittest.main()
