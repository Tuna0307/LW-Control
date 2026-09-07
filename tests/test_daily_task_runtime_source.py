import unittest

from tools.prepare_daily_task_runtime import _runtime_source, _world_scan_source, _wrapper_source


class DailyTaskRuntimeSourceChecks(unittest.TestCase):
    def test_runtime_uses_proven_scheduler_and_protocol(self):
        source = _runtime_source().decode("utf-8")
        self.assertIn("UpdateManager", source)
        self.assertIn("AddUpdate", source)
        self.assertIn("defines.DailyQuestLs", source)
        self.assertIn("defines.DailyTaskReward", source)
        self.assertIn("defines.DailyQuestReward", source)
        self.assertIn("DailyQuestLsMessageHandle", source)
        self.assertIn("DailyTaskRewardMessageHandle", source)
        self.assertIn("DailyQuestRewardMessageHandle", source)
        self.assertIn('require, "Net.Msgs.Alliance.PushDailyQuestMessage"', source)
        self.assertNotIn("DailyQuestReward, -1", source)

    def test_runtime_redetects_after_every_confirmed_claim(self):
        source = _runtime_source().decode("utf-8")
        self.assertIn("target_received", source)
        self.assertIn("active.confirmedClaims = active.confirmedClaims + 1", source)
        self.assertIn("local next_target = select_one(snapshot)", source)
        self.assertIn('phase = "waiting_managers"', source)
        self.assertIn('active.phase = "settling_confirmed_claim"', source)
        self.assertIn("continue_after_settle", source)
        self.assertIn('finish_command(now, "unknown", "claim_effect_unconfirmed"', source)
        self.assertIn("maximumClaims", source)
        self.assertIn("maximum < 1 or maximum > 20", source)

    def test_runtime_rejects_replay_and_wrapper_preserves_original_entry(self):
        runtime = _runtime_source().decode("utf-8")
        wrapper = _wrapper_source().decode("utf-8")
        self.assertIn("file_exists(result)", runtime)
        self.assertIn("command_id_already_completed", runtime)
        self.assertIn('require, "DataCenter.Global.LuaEntry_original"', wrapper)
        self.assertIn('require, "LWControlDailyTaskRuntime"', wrapper)

    def test_persistent_package_hosts_world_scan_command_runtime(self):
        wrapper = _wrapper_source().decode("utf-8")
        world_scan = _world_scan_source().decode("utf-8")
        self.assertIn('rawset(_G, "LWControlWorldScanPersistentRuntime", true)', wrapper)
        self.assertIn('require, "LWControlWorldScanRuntime"', wrapper)
        self.assertIn('PATHS.command = root .. [[\\world-map-scan-command.txt]]', world_scan)
        self.assertIn('values.mode ~= "run_once"', world_scan)
        self.assertIn('M.ResetScan(command_id)', world_scan)
        self.assertIn('if M.PERSISTENT then M.ProcessScanCommand(now) end', world_scan)


if __name__ == "__main__":
    unittest.main()
