"""Source-bound negative inverses over unchanged genuine private receipts."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import unittest

from audit_city_bounded_union import ROOT, RECEIPTS, examine
from audit_city_source_coverage import rows_for
from audit_city_records import SOURCES


class CityNativeUnionAdmission(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = json.loads((ROOT/RECEIPTS[0]).read_text(encoding="utf-8"))
        cls.saved = rows_for(SOURCES[2])

    def test_genuine_source_and_zero_city_allowed_only_if_native_agrees(self):
        _, owners, result = examine(copy.deepcopy(self.source), RECEIPTS[0], self.saved)
        self.assertEqual(len(owners), 62)
        self.assertEqual(result["sameOwnerIndexOrCoordinateChanged"], 0)
        zero = json.loads((ROOT/RECEIPTS[2]).read_text(encoding="utf-8"))
        _, empty, report = examine(zero, RECEIPTS[2], self.saved)
        self.assertEqual(empty, {})
        self.assertEqual(report["nativeCurrentSourceCityCount"], 0)

    def test_stale_wrong_server_duplicate_missing_out_of_footprint_and_restore(self):
        changes = (
            lambda d: d.update(map013NativeReplyState="waiting"),
            lambda d: d["point_records"][0].update(serverId=2213),
            lambda d: d["point_records"].append(copy.deepcopy(
                next(r for r in d["point_records"] if r.get("kind")=="player_base"))),
            lambda d: d["cityCompleteness"].update(missingNativeCurrentPointIndices=1),
            lambda d: d["point_records"][0].update(x=0,y=0,kind="player_base"),
            lambda d: d.update(cameraGeometryConfirmed=False),
            lambda d: d["requestedIndices"].append(d["requestedIndices"][0]),
        )
        for change in changes:
            modified=copy.deepcopy(self.source)
            change(modified)
            with self.subTest(changed=str(change)), self.assertRaises(ValueError):
                examine(modified,"inert_modified_current_source",self.saved)


if __name__=="__main__":
    unittest.main()
