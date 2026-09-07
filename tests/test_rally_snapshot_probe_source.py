from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_rally_snapshot_probe.lua"
PREPARER = ROOT / "tools" / "prepare_rally_snapshot_probe.py"
RUNNER = ROOT / "tools" / "run_rally_snapshot_probe.py"


class RallySnapshotProbeSourceTests(unittest.TestCase):
    def test_builder_uses_recovered_current_manager_contract(self):
        source = SOURCE.read_text(encoding="utf-8")
        for contract in (
            "GetAllianceWarIdList",
            "GetAllianceWarDataByUuid",
            "CheckJoinAllianceWar",
            "GetAllianceWarDurationSec",
            "GetCurFormationList",
            "GetCurStaminaByUuid",
            "GetOwnerFormationMarch",
            "IsFree",
        ):
            self.assertIn(contract, source)

    def test_builder_contains_no_join_send_or_claim_path(self):
        source = SOURCE.read_text(encoding="utf-8")
        for forbidden in (
            "SFSNetwork",
            "SendMessage",
            "StartMarch",
            "OnClickStartMarch",
            "TryReqUpdateData",
            "ReceiveReward",
        ):
            self.assertNotIn(forbidden, source)
        self.assertNotIn('invoke(manager, "OnJoinClick"', source)
        self.assertNotIn('invoke(data, "OnJoinClick"', source)

    def test_builder_fails_closed_on_missing_identity_or_manager_calls(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("observed rally has no stable uuid", source)
        self.assertIn("CheckJoinAllianceWar failed for rally", source)
        self.assertIn("formation list contains an entry without a stable uuid", source)
        self.assertIn("GetCurStaminaByUuid failed for", source)
        self.assertIn("GetCurFormationList is empty; refusing a pre-login/incomplete snapshot", source)

    def test_builder_uses_current_server_clock_for_current_rally_remaining_seconds(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('rawget(_G, "UITimeManager")', source)
        self.assertIn('invoke(instance, "GetServerTime")', source)
        self.assertIn('invoke(manager, "GetAllianceWarDurationSec", data, current_time)', source)
        self.assertIn('remainingSecondsSource = "AllianceWarDataManager.GetAllianceWarDurationSec"', source)

    def test_builder_uses_recovered_current_alliance_team_enum_and_join_routing(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('[0] = "ATTACK_BOSS"', source)
        self.assertIn('[11] = "ATTACK_EPIDEMIC_CITY"', source)
        self.assertNotIn('[9] =', source)
        self.assertIn('ATTACK_BOSS = "RALLY_FOR_BOSS"', source)
        self.assertIn('ATTACK_BUILDING = "RALLY_FOR_BUILDING"', source)
        self.assertIn('ATTACK_AL_CITY = "RALLY_FOR_ALLIANCE_CITY"', source)
        self.assertIn('ATTACK_CITY = "RALLY_FOR_CITY"', source)
        self.assertIn('ATTACK_EPIDEMIC_CITY = "RALLY_EPIDEMIC_CITY"', source)
        self.assertIn('ATTACK_CITY_STRONGHOLD = "RALLY_CITY_STRONGHOLD"', source)
        self.assertIn('warTypeSource = "Global.EnumType.AllianceTeamType"', source)
        self.assertIn('joinRallyTypeSource = "UIAllianceWarMainTableCtrl.OnJoinClick"', source)

    def test_builder_records_main_table_server_world_join_routing(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('server = server_id', source)
        self.assertIn('serverSource = "AllianceWarInfo.ParseData: message.server"', source)
        self.assertIn('worldId = world_id', source)
        self.assertIn('worldIdSource = "AllianceWarInfo.ParseData: message.worldId"', source)
        self.assertIn('joinTargetServerId = server_id', source)
        self.assertIn('joinTargetWorldId = world_id', source)
        self.assertIn('joinTargetSource = "UIAllianceWarMainTableCtrl.OnJoinClick: leaderMarch.startId + rally uuid + data.server + data.worldId"', source)

    def test_builder_derives_boss_monster_special_type_from_current_ui_source(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('safe_get(data_center, "MonsterTemplateManager")', source)
        self.assertIn('invoke(monster_manager, "GetMonsterTemplate", value(data, "targetUid"))', source)
        self.assertIn('join_monster_special_type = number_or_nil(value(monster, "special"))', source)
        self.assertIn('joinMonsterSpecialType = join_monster_special_type', source)
        self.assertIn('joinMonsterSpecialTypeSource = join_monster_special_type_source', source)

    def test_builder_uses_leader_inclusive_member_count_and_boss_display_metadata(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local member_count = listed_member_count + 1", source)
        self.assertIn(
            'member_source = "AllianceWarDataManager.CheckJoinAllianceWarByWarData: table.count(memberList)+1"',
            source,
        )
        self.assertNotIn('value(data, "join")', source)
        self.assertIn('resolved_target_name = text(value(monster, "name"))', source)
        self.assertIn('resolved_target_level = number_or_nil(value(monster, "level"))', source)
        self.assertIn('safe_get(safe_get(cs, "GameEntry"), "Localization")', source)
        self.assertIn('invoke(localization, "GetString", value(monster, "name"))', source)
        self.assertIn("resolvedTargetDisplayName = resolved_target_display_name", source)

    def test_builder_keeps_current_target_fields_distinct_and_uses_proven_level_fields(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('targetUuid = text(value(data, "targetUuid"))', source)
        self.assertIn('targetUid = text(value(data, "targetUid"))', source)
        self.assertIn('targetBaseSkinId = number_or_nil(value(data, "targetBaseSkinId"))', source)
        self.assertIn('targetLevel = number_or_nil(value(data, "targetLevel"))', source)
        self.assertIn('targetBaseSkinIdSource = "AllianceWarInfo.ParseData: message.targetBaseSkinId"', source)
        self.assertIn('targetLevelSource = "AllianceWarInfo.ParseData: message.targetLevel"', source)
        self.assertNotIn('value(data, "level")', source)
        self.assertIn('joinTargetUuid = uuid', source)
        self.assertIn('joinTargetPointId = leader_start_id', source)
        self.assertIn('startId = leader_start_id', source)

    def test_runtime_uses_global_datacenter_paths_confirmed_by_current_bytecode(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('safe_get(data_center, "AllianceWarDataManager")', source)
        self.assertIn('safe_get(data_center, "ArmyFormationDataManager")', source)
        self.assertNotIn('safe_get(data_center, "AllianceData")', source)

    def test_runtime_requires_stable_read_only_state_before_writing_snapshot(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("STABLE_SECONDS = 3", source)
        self.assertIn('write_status(now, "stabilizing"', source)
        self.assertIn("current_signature ~= stable_signature", source)
        self.assertIn("capture_complete = true", source)
        self.assertIn('explicitNetworkSends = 0', source)
        self.assertIn('joinActions = 0', source)

    def test_runtime_uses_proven_recurring_pump_registration(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("function M.Register()", source)
        self.assertIn('rawget(_G, "UpdateManager")', source)
        self.assertIn('safe_get(instance, "AddUpdate")', source)
        self.assertIn('safe_get(timer, "RegisterTimerRepeat")', source)
        self.assertIn('registration_method = "UpdateManager.AddUpdate"', source)
        self.assertIn('registration_method = "GameEntry.Timer.RegisterTimerRepeat"', source)
        self.assertIn("M.Register()", source)

    def test_runtime_world_march_lookup_is_recovered_read_only_path(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('safe_get(rawget(_G, "SceneManager"), "World")', source)
        self.assertIn('safe_get(world, "MarchDataManager")', source)
        self.assertIn('invoke(world, "get_MarchDataManager")', source)
        self.assertIn('invoke_static(resources, "FindObjectsOfTypeAll", reflected_type)', source)

    def test_candidate_round_trips_encrypted_probe_and_preserves_official_entry(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)
        self.assertIn('"changed_installed_files": False', source)

    def test_runner_restores_exact_pre_run_hashes_in_finally(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)
        self.assertIn('for key in ("data", "metadata", "version", "baseutils")', source)
        self.assertIn('"explicit_network_sends": 0', source)
        self.assertIn('"rally_join_actions": 0', source)
        self.assertIn('"reward_claim_actions": 0', source)


if __name__ == "__main__":
    unittest.main()
