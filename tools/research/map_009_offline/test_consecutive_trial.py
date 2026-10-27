"""Adversarial *model* checks; not proof of native server response semantics."""
from __future__ import annotations

import unittest
from dataclasses import replace

from consecutive_region_trial import (ConsecutiveRegionTrial, Owner, Region, Reply,
                                      TrialRejected, View)


OWNER = Owner("main", "session-1", 4312, 3, 2172, 0)
HOME = View(330, 440, 4.0, 62.0, 1.75)
REGIONS = (Region("target-a", 550, 573, frozenset({1, 2, 3})),
           Region("target-b", 590, 573, frozenset({3, 4, 5})))


def trial():
    return ConsecutiveRegionTrial(OWNER, HOME, REGIONS, frozenset(range(1, 6)))


def reply(region=REGIONS[0], **overrides):
    base = Reply(OWNER, region.request_id, region.target_x, region.target_y,
                 OWNER.server, OWNER.world, OWNER.generation, 0, True,
                 region.required_aoi, frozenset({"known:1", "known:2"}),
                 True, False, True, True, True, 2)
    return replace(base, **overrides)


class ConsecutiveTrialTests(unittest.TestCase):
    def test_two_regions_advance_before_single_exact_restoration(self):
        model = trial()
        self.assertEqual(model.pending.request_id, "target-a")
        self.assertEqual(model.accept(reply()), "next-region")
        self.assertFalse(model.restoration_pending)
        self.assertEqual(model.pending.request_id, "target-b")
        self.assertEqual(model.accept(reply(REGIONS[1])), "restore-home")
        self.assertEqual(model.covered, set(range(1, 6)))
        self.assertEqual(model.records, {"known:1", "known:2"})
        self.assertTrue(model.restoration_pending)
        model.confirm_home(OWNER, HOME)
        self.assertTrue(model.finished)
        with self.assertRaises(TrialRejected):
            model.accept(reply())

    def test_split_out_of_order_and_duplicate(self):
        model = trial()
        first = reply(segment=0, final_segment=False, native_split_pending=True,
                      native_flags_ready=False, native_aoi=frozenset({1}),
                      record_keys=frozenset({"known:1"}), expected_list_total=2)
        self.assertEqual(model.accept(first), "pending-split")
        self.assertEqual(model.accept(first), "pending-split")
        second = reply(segment=1, final_segment=True, native_split_pending=False,
                       native_aoi=frozenset({2, 3}), record_keys=frozenset({"known:2"}))
        self.assertEqual(model.accept(second), "next-region")
        self.assertEqual(model.records, {"known:1", "known:2"})
        self.assertEqual(model.covered, {1, 2, 3})
        with self.assertRaises(TrialRejected):
            model.accept(first)  # late reply for completed region A

    def test_wrong_owner_server_world_generation_and_request(self):
        changed = [
            dict(owner=replace(OWNER, session="session-2")),
            dict(owner=replace(OWNER, game_pid=8888)),
            dict(owner=replace(OWNER, generation=4)),
            dict(generation=4),
            dict(server=2173),
            dict(world=7),
            dict(request_id="target-b"),
            dict(target_x=550 + 1),
        ]
        for mismatch in changed:
            with self.subTest(mismatch=mismatch):
                model = trial()
                with self.assertRaises(TrialRejected):
                    model.accept(reply(**mismatch))
                self.assertFalse(model.covered)
                self.assertEqual(model.abort(OWNER), "restore-home")
                model.confirm_home(OWNER, HOME)

    def test_stale_missing_or_conflicting_replies(self):
        changed = [
            dict(captured_after_request=False),
            dict(point_snapshot_ready=False),
            dict(march_snapshot_ready=False),
            dict(native_flags_ready=False),
            dict(native_split_pending=True),
            dict(native_aoi=frozenset({1, 2})),
            dict(expected_list_total=3),
        ]
        for mismatch in changed:
            with self.subTest(mismatch=mismatch):
                with self.assertRaises(TrialRejected):
                    trial().accept(reply(**mismatch))
        model = trial()
        with self.assertRaises(TrialRejected):
            model.accept(reply(segment=1, native_aoi=frozenset({1, 2, 3})))
        model = trial()
        segment0 = reply(segment=0, final_segment=False, native_flags_ready=False,
                         native_split_pending=True)
        model.accept(segment0)
        with self.assertRaises(TrialRejected):
            model.accept(replace(segment0, record_keys=frozenset({"different"})))

    def test_completion_requires_global_aoi_coverage(self):
        model = ConsecutiveRegionTrial(OWNER, HOME, REGIONS, frozenset(range(1, 7)))
        model.accept(reply())
        with self.assertRaises(TrialRejected):
            model.accept(reply(REGIONS[1]))

    def test_positive_flags_and_generic_network_activity_do_not_prove_second_aoi(self):
        # MAP-010 R10 *real* diagnostic: scene tile (545,495) settled,
        # StartViewRequest returned successfully, unscoped packet counters
        # increased, and both response flags became true, but native
        # _curViewIndex had nine AOIs without the requested target index.
        # The synthetic Reply here is ONLY an adversarial acceptance model:
        # never promote manager flags or network totals into native identity.
        model = trial()
        self.assertEqual(model.accept(reply()), "next-region")
        self.assertFalse(model.restoration_pending)
        wrong_footprint = reply(REGIONS[1], native_aoi=frozenset({1, 2, 3}),
                                native_flags_ready=True, native_split_pending=False,
                                captured_after_request=True,
                                point_snapshot_ready=True, march_snapshot_ready=True)
        with self.assertRaisesRegex(TrialRejected, "coverage gap"):
            model.accept(wrong_footprint)
        self.assertEqual(model.covered, {1, 2, 3})
        self.assertEqual(model.ordinal, 1)
        self.assertEqual(model.abort(OWNER), "restore-home")
        model.confirm_home(OWNER, HOME)
        self.assertFalse(model.finished)

    def test_cancellation_error_owner_replacement_and_exact_home(self):
        model = trial()
        self.assertEqual(model.abort(OWNER), "restore-home")
        with self.assertRaises(TrialRejected):
            model.confirm_home(OWNER, replace(HOME, fov=HOME.fov + 0.1))
        with self.assertRaises(TrialRejected):
            model.confirm_home(replace(OWNER, server=2173), HOME)
        model.confirm_home(OWNER, HOME)
        self.assertFalse(model.finished)  # aborted, not reported completed
        replaced = trial()
        self.assertEqual(replaced.abort(replace(OWNER, session="new-session")),
                         "owner-changed-no-restoration")
        self.assertTrue(replaced.unrestorable_owner_change)
        with self.assertRaises(TrialRejected):
            replaced.confirm_home(OWNER, HOME)

    def test_ambiguous_plan_is_rejected_before_any_capture(self):
        with self.assertRaises(TrialRejected):
            ConsecutiveRegionTrial(OWNER, HOME, (REGIONS[0], REGIONS[0]), frozenset({1}))
        with self.assertRaises(TrialRejected):
            ConsecutiveRegionTrial(OWNER, HOME, (Region("x", 0, 0, frozenset({10000})),),
                                   frozenset({10000}))


if __name__ == "__main__":
    unittest.main()
