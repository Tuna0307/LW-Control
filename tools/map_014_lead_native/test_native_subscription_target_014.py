"""Native view cannot be qualified by a matching logical camera tile alone."""
import copy
import json
import unittest

from audit_native_subscription_target_014 import ROOT, LATEST, SEQUENCE, ZOOM, classify


class NativeViewSourceGate(unittest.TestCase):
    def test_new_genuine_source_positive_and_failed_second_region(self):
        first=json.loads((ROOT/LATEST/SEQUENCE[0]).read_text(encoding="utf-8"))
        second=json.loads((ROOT/LATEST/SEQUENCE[-1]).read_text(encoding="utf-8"))
        self.assertTrue(classify(first)["subscribedTargetPresent"])
        self.assertEqual(classify(first)["issues"],[])
        observed=classify(second)
        self.assertEqual(observed["targetNativeAoi"],4954)
        self.assertEqual(observed["nativeSubscribedAois"],9)
        self.assertFalse(observed["subscribedTargetPresent"])
        self.assertIn("native_subscription_excludes_target",observed["issues"])
        self.assertIn("not_fresh_source_matched",observed["issues"])
        self.assertIn("camera_not_restored",observed["issues"])

    def test_wrong_target_and_coarse_view_are_rejected(self):
        first=json.loads((ROOT/LATEST/SEQUENCE[0]).read_text(encoding="utf-8"))
        changed=copy.deepcopy(first)
        changed["targetTileX"]=999
        self.assertIn("native_subscription_excludes_target",classify(changed)["issues"])
        changed=copy.deepcopy(first)
        changed["serverLod"]=2
        self.assertIn("not_required_native_lod0",classify(changed)["issues"])
        zoom=json.loads((ROOT/ZOOM).read_text(encoding="utf-8"))
        self.assertIn("post_zoom_coarse_native_lod",classify(zoom)["issues"])


if __name__=="__main__":
    unittest.main()
