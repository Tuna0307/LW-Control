"""Adversarial offline tests; no game/application is started."""

from __future__ import annotations

from copy import deepcopy
from pathlib import Path
import json
import unittest

from receipt_gate import validate_abc, validate_group, validate_region


ROOT = Path(__file__).resolve().parents[2] / "artifacts" / "map-013-live"
POSITIVE = ROOT / "native-causal-20261014-004239"
NEGATIVE = ROOT / "native-causal-20261014-003652"


def load(directory: Path, n: int) -> dict:
    return json.loads((directory / f"sequence-region-{n}.json").read_text(encoding="utf-8"))


class ConsecutiveGateTests(unittest.TestCase):
    def test_archive_abc_source_positive(self):
        validate_abc(POSITIVE)
        validate_abc(ROOT / "native-causal-20261014-004916")

    def test_archive_old_a_subscription_rejected(self):
        old_b = load(NEGATIVE, 2)
        with self.assertRaisesRegex(ValueError, "region did not finish proven"):
            validate_region(old_b, x=545, y=495, final=False)
        self.assertNotIn(4954, old_b["requestedIndices"])
        self.assertEqual(old_b["map013NativeReplyState"], "waiting")

    def test_target_echo_without_native_coverage_rejected(self):
        b = load(POSITIVE, 2)
        b["requestedIndices"].remove(4954)
        with self.assertRaisesRegex(ValueError, "native AOI rectangle|native footprint omits"):
            validate_region(b, x=545, y=495, final=False)

    def test_false_reply_transition_rejected(self):
        b = load(POSITIVE, 2)
        b["responseFlagsTransitioned"] = False
        with self.assertRaisesRegex(ValueError, "reply flags"):
            validate_region(b, x=545, y=495, final=False)

    def test_stale_response_proven_without_correlation_rejected(self):
        b = load(POSITIVE, 2)
        b["map013NativeReplyState"] = "waiting"
        with self.assertRaisesRegex(ValueError, "correlated native reply"):
            validate_region(b, x=545, y=495, final=False)

    def test_post_update_stale_a_current_indices_rejected(self):
        b = load(POSITIVE, 2)
        old_a = load(POSITIVE, 1)["requestedIndices"]
        next(s for s in b["map014SubscriptionStages"]
             if s["stage"] == "post-update")["currentIndices"] = old_a
        with self.assertRaisesRegex(ValueError, "_curViewIndex"):
            validate_region(b, x=545, y=495, final=False)

    def test_malformed_aoi_list_rejected_cleanly(self):
        b = load(POSITIVE, 2)
        b["requestedIndices"][0] = {"invalid": "unhashable"}
        with self.assertRaisesRegex(ValueError, "invalid native AOI rectangle"):
            validate_region(b, x=545, y=495, final=False)

    def test_post_lens_fov_120_rejected_despite_target_coverage(self):
        b = load(POSITIVE, 2)
        next(s for s in b["map014SubscriptionStages"]
             if s["stage"] == "post-lens")["geometry"]["fov"] = 120.0
        with self.assertRaisesRegex(ValueError, "native FOV"):
            validate_region(b, x=545, y=495, final=False)

    def test_final_home_geometry_missing_rejected(self):
        c = load(POSITIVE, 3)
        for key in ("cameraGeometryRestored", "cameraGeometryConfirmed",
                    "cameraProjectionRestored", "positionRestoredAfterResponse"):
            corrupted = deepcopy(c)
            corrupted[key] = False
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, key):
                validate_region(corrupted, x=655, y=565, final=True)

    def test_session_reassignment_rejected(self):
        a, b, c = (load(POSITIVE, n) for n in (1, 2, 3))
        b["launchSessionId"] = "different-session"
        with self.assertRaisesRegex(ValueError, "group changed native owner"):
            validate_group([a, b, c])

    def test_duplicate_region_request_id_rejected(self):
        a, b, c = (load(POSITIVE, n) for n in (1, 2, 3))
        b["requestId"] = a["requestId"]
        with self.assertRaisesRegex(ValueError, "request IDs"):
            validate_group([a, b, c])

    def test_false_final_restore_checkpoint_rejected(self):
        c = load(POSITIVE, 3)
        c["restoredCameraGeometry"]["position"]["x"] += 100
        with self.assertRaisesRegex(ValueError, "native camera checkpoint"):
            validate_region(c, x=655, y=565, final=True)
        c = load(POSITIVE, 3)
        c["restoredTileX"] += 1
        with self.assertRaisesRegex(ValueError, "camera tile differs"):
            validate_region(c, x=655, y=565, final=True)


if __name__ == "__main__":
    unittest.main()
