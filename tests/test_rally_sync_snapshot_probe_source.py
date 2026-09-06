from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
TEMPLATE = ROOT / "tools" / "current_rally_sync_snapshot_probe.lua"
PREPARER = ROOT / "tools" / "prepare_rally_sync_snapshot_probe.py"
RUNNER = ROOT / "tools" / "run_rally_sync_snapshot_probe.py"


class RallySyncSnapshotProbeSourceTests(unittest.TestCase):
    def test_sync_call_matches_current_ui_and_recovered_original_contract(self):
        source = TEMPLATE.read_text(encoding="utf-8")
        self.assertIn('safe_get(msg_defines, "GetAllianceWarList")', source)
        self.assertIn('invoke(player, "GetCurServerId")', source)
        self.assertIn('invoke(player, "GetCurWorldId")', source)
        self.assertIn('pcall(send, sync_protocol, target_server)', source)

    def test_sync_probe_has_exact_one_send_no_retry_gates(self):
        source = TEMPLATE.read_text(encoding="utf-8")
        self.assertIn("owned_send_count ~= 1", source)
        self.assertIn("foreign_sync_send_count ~= 0", source)
        self.assertIn("handler_count ~= 1", source)
        self.assertIn("noRetry = true", source)
        self.assertIn("sync correlation became ambiguous", source)

    def test_sync_probe_correlates_current_message_handler_and_manager_refresh(self):
        source = TEMPLATE.read_text(encoding="utf-8")
        self.assertIn('require, "Net.Msgs.Alliance.GetAllianceWarListMessage"', source)
        self.assertIn('message_module["HandleMessage"] = handle_wrapper', source)
        self.assertIn('response_teams_present = safe_get(payload, "teams") ~= nil', source)
        self.assertIn("listRefreshCorrelated", source)

    def test_sync_probe_contains_no_rally_join_or_reward_action_path(self):
        source = TEMPLATE.read_text(encoding="utf-8")
        for forbidden in (
            "StartMarch",
            "JOIN_RALLY",
            "OnClickStartMarch",
            "ReceiveReward",
            "TryReqUpdateData",
        ):
            self.assertNotIn(forbidden, source)

    def test_hooks_restore_before_success_snapshot(self):
        source = TEMPLATE.read_text(encoding="utf-8")
        self.assertIn('network["SendMessage"] = original_send', source)
        self.assertIn('message_module["HandleMessage"] = original_handle', source)
        self.assertIn("local hooks_restored = restore_hooks()", source)
        self.assertIn('if not hooks_restored then return false, "sync hooks did not restore" end', source)

    def test_candidate_preserves_official_entry_and_lenc_round_trips(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn('"changed_installed_files": False', source)

    def test_runner_restores_exact_pre_run_hashes_and_limits_action_scope(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('"maximum_owned_network_sends": 1', source)
        self.assertIn('"rally_join_actions": 0', source)
        self.assertIn('"reward_claim_actions": 0', source)


if __name__ == "__main__":
    unittest.main()
