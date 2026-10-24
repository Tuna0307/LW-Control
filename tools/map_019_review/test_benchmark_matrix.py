"""No-game checks: matrix validation must precede native startup."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("map019_owned_runner", ROOT / "tools/map_014_lead_native/run_owned.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
mobile_spec = importlib.util.spec_from_file_location("map019_mobile_audit", ROOT / "tools/map_019_review/audit_mobile.py")
mobile_audit = importlib.util.module_from_spec(mobile_spec)
mobile_spec.loader.exec_module(mobile_audit)
live_spec = importlib.util.spec_from_file_location("map019_live_audit", ROOT / "tools/map_019_review/audit_live.py")
live_audit = importlib.util.module_from_spec(live_spec)
live_spec.loader.exec_module(live_audit)


class MatrixChecks(unittest.TestCase):
    def test_old_bounds_are_unchanged_even_if_review_env_is_set(self):
        env={"LWBRIDGE_MAP019_MAX_MINUTES":"180"}
        self.assertIsNone(runner.review_plan("benchmark-city",env))
        self.assertEqual(runner.managed_deadline_seconds("benchmark-city",env),1860)
        self.assertEqual(runner.managed_deadline_seconds("city",env),780)
        env["LWBRIDGE_MAP015_PAIRED_TRUCK"]="1"
        self.assertEqual(runner.managed_deadline_seconds("direct-full",env),2100)

    def test_default_review_has_all_ten_singles_and_current_combined_workloads(self):
        plan=runner.review_plan("benchmark-review",{})
        self.assertEqual(plan["measurements"],48)
        self.assertIn("other",plan["workloads"])
        self.assertIn("all-ten",plan["workloads"])
        self.assertNotIn("all-nine",plan["workloads"])
        self.assertEqual(plan["methods"],("direct","camera"))
        self.assertEqual(plan["roundsPerWorkload"],4)
        self.assertEqual(plan["deadlineSeconds"],10800)

    def test_explicit_same_method_rounds_and_bounded_matrix(self):
        plan=runner.review_plan("benchmark-review",{
            "LWBRIDGE_MAP019_WORKLOADS":"city,railway,truck",
            "LWBRIDGE_MAP019_METHODS":"direct,direct",
            "LWBRIDGE_MAP019_ROUNDS":"2","LWBRIDGE_MAP019_MAX_MINUTES":"60"})
        self.assertEqual(plan["measurements"],6)
        self.assertEqual(plan["deadlineSeconds"],3600)

    def test_invalid_requests_fail_before_game_start(self):
        for key,value in (("WORKLOADS","city,city"),("WORKLOADS","attack"),("WORKLOADS",""),
                          ("METHODS","parallel"),("METHODS",""),("ROUNDS","7"),("ROUNDS","0"),
                          ("MAX_MINUTES","181"),("MAX_MINUTES","0")):
            with self.subTest(key=key,value=value), self.assertRaises(ValueError):
                runner.review_plan("benchmark-review",{f"LWBRIDGE_MAP019_{key}":value})

    def test_remote_targets_are_explicit_owner_supplied_views(self):
        for server in ('2175','2180','2198','2203'):
            self.assertIsNotNone(runner.review_plan('benchmark-review',{'LWBRIDGE_MAP019_SERVER':server}))
        for server in ('9999','0','attack'):
            with self.assertRaises(ValueError):
                runner.review_plan('benchmark-review',{'LWBRIDGE_MAP019_SERVER':server})

    def test_coarse_boss_cannot_be_applied_to_another_kind(self):
        with self.assertRaises(ValueError):
            runner.review_plan("benchmark-review",{"LWBRIDGE_MAP019_METHODS":"coarse-boss"})
        with self.assertRaises(ValueError):
            runner.review_plan("benchmark-review",{"LWBRIDGE_MAP019_METHODS":"coarse-boss","LWBRIDGE_MAP019_WORKLOADS":"city"})
        plan=runner.review_plan("benchmark-review",{"LWBRIDGE_MAP019_METHODS":"direct,coarse-boss,coarse-boss,direct",
            "LWBRIDGE_MAP019_WORKLOADS":"zombie_boss"})
        self.assertEqual(plan["methods"],("direct","coarse-boss","coarse-boss","direct"))

    def test_mobile_auditor_can_bind_server_from_camera_first_plan(self):
        self.assertEqual(mobile_audit.bind_server(None, 2212), 2212)
        self.assertEqual(mobile_audit.bind_server(2212, 2212), 2212)
        with self.assertRaises(ValueError):
            mobile_audit.bind_server(2212, 2211)

    def test_sanitized_auditors_redact_local_paths_and_private_ids(self):
        for helper in (mobile_audit.redacted, live_audit.redacted):
            with self.subTest(helper=helper.__module__):
                path_value = helper("OSError('C:\\Users\\private\\secret.txt')")
                self.assertNotIn("C:\\Users\\private", path_value)
                self.assertIn("[local-path]", path_value)
                id_value = helper("failure 1234567890123 token 0123456789abcdef0123456789abcdef")
                self.assertNotIn("1234567890123", id_value)
                self.assertNotIn("0123456789abcdef0123456789abcdef", id_value)
                self.assertEqual(id_value.count("[private-id]"), 2)


if __name__=="__main__": unittest.main()
