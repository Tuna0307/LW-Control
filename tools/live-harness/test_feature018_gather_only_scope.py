"""Per-run main-account waiver stays limited to the approved gathering proof."""
import unittest
from feature018_live_queue import submit


class GatherOnlyScope(unittest.TestCase):
    def test_default_still_refuses_other_accounts(self):
        with self.assertRaisesRegex(ValueError, 'owner-authorized proof pairs'):
            submit('not-a-live-session', {'kind': 'finish'}, expected_uid='123456789', expected_server=2212)

    def test_explicit_gather_scope_rejects_other_features_before_any_file_access(self):
        for operation in (
            {'kind': 'key', 'binding': 'Q'},
            {'kind': 'r23Ui', 'surface': 'r30-allianceGift'},
            {'kind': 'command', 'command': 'feature018_hotkey_action', 'payload': {'action': 'recall', 'slot': 1}},
            {'kind': 'command', 'command': 'feature018_automation_stop', 'payload': {'kind': 'dispatch'}},
            {'kind': 'profileCommand', 'command': 'feature018_automation_config_save'},
        ):
            with self.subTest(operation=operation), self.assertRaisesRegex(ValueError, 'scope exceeded'):
                submit('not-a-live-session', operation, expected_uid='123456789', expected_server=2212, gather_only=True)


if __name__ == '__main__':
    unittest.main()
