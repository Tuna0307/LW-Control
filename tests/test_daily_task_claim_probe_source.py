import unittest

from tools.prepare_daily_task_claim_probe import _probe_source


class DailyTaskClaimProbeSourceChecks(unittest.TestCase):
    def test_probe_is_bounded_to_one_explicit_claim(self):
        source = _probe_source().decode("utf-8")
        self.assertIn("if completed or send_count > 0 then return true end", source)
        self.assertIn("send_count = 1", source)
        self.assertIn("defines.DailyTaskReward", source)
        self.assertIn("defines.DailyQuestReward", source)
        self.assertIn("target.stage >= 1 and target.stage <= 5", source)
        self.assertNotIn("DailyQuestReward, -1", source)

    def test_probe_records_response_and_requires_fresh_post_state(self):
        source = _probe_source().decode("utf-8")
        self.assertIn("DailyTaskRewardMessageHandle", source)
        self.assertIn("DailyQuestRewardMessageHandle", source)
        self.assertIn("response_contains_task", source)
        self.assertIn("response_contains_push_task", source)
        self.assertIn('require, "Net.Msgs.Alliance.PushDailyQuestMessage"', source)
        self.assertIn('capture_wire("PushDailyQuest"', source)
        self.assertIn("response_contains_stage", source)
        self.assertIn("request_post_claim_refresh_once", source)
        self.assertIn("finish_from_post_refresh", source)
        self.assertIn('verificationMode = "fresh_daily_quest_list_state"', source)
        self.assertIn("postRefreshObserved = true", source)
        self.assertIn('task_state(after_snapshot, target.taskId) == "Received"', source)
        self.assertIn('box_state(after_snapshot, target.stage) == "Received"', source)


if __name__ == "__main__":
    unittest.main()
