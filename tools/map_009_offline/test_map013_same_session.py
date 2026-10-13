import json
import unittest
from audit_map013_same_session import fresh_reply_context, same_footprint


class SameSessionTests(unittest.TestCase):
    def row(self, i, x):
        return {"id": i, "x": x, "y": 25, "serverId": 2212, "worldId": 0, "kind": "resource"}

    def test_same_footprint_detects_missing_extra_and_field_changes(self):
        wide = {"point_records": [self.row(1, 25), self.row(2, 45)], "requestedIndices": [22, 24]}
        narrow = {"blockSize": 10, "blockCount": 10, "requestedIndices": [22],
                  "point_records": [self.row(1, 25)]}
        self.assertEqual(same_footprint(wide, narrow)["missing"], 0)
        narrow["point_records"] = []
        self.assertEqual(same_footprint(wide, narrow)["missing"], 1)
        narrow["point_records"] = [self.row(1, 25), self.row(2, 45)]
        self.assertEqual(same_footprint(wide, narrow)["extra"], 1)
        narrow["point_records"] = [{**self.row(1, 25), "level": 7}]
        self.assertEqual(same_footprint(wide, narrow)["differingRows"], 1)

    def test_foreign_rows_and_duplicate_identity_reject(self):
        wide = {"point_records": [self.row(1, 25)], "requestedIndices": [22]}
        narrow = {"blockSize": 10, "blockCount": 10, "requestedIndices": [22],
                  "point_records": [{**self.row(1, 25), "serverId": 2213}]}
        with self.assertRaisesRegex(ValueError, "foreign"):
            same_footprint(wide, narrow)
        narrow["point_records"] = [self.row(1, 25), self.row(1, 25)]
        with self.assertRaisesRegex(ValueError, "duplicate"):
            same_footprint(wide, narrow)

    def test_cached_wrong_context_and_truncated_replies_cannot_be_fresh(self):
        numbers = {"serverId": 2212, "worldId": 0, "x": 545, "y": 495, "viewLvl": 0}
        packet = {"direction": "receive", "command": "world.get.block", "packetHash": 42,
                  "segments": {"entries": [{"numbers": numbers, "arrayCounts": {"points": 18}}]}}
        data = {"packets": [packet], "receiveEnumerationTruncated": False}
        trace = {"managedPacketSnapshots": [{"elapsed": 1, "json": json.dumps(data)},
                                           {"elapsed": 2, "json": json.dumps(data)}]}
        match = lambda: fresh_reply_context(trace, server=2212, world=0, x=545, y=495)
        self.assertEqual(match(), [])
        trace["managedPacketSnapshots"] = [{"elapsed": 1, "json": json.dumps({"packets": []})},
                                           {"elapsed": 2, "json": json.dumps(data)}]
        self.assertEqual(len(match()), 1)
        self.assertEqual(fresh_reply_context(trace, server=2213, world=0, x=545, y=495), [])
        data["receiveEnumerationTruncated"] = True
        trace["managedPacketSnapshots"][1]["json"] = json.dumps(data)
        with self.assertRaisesRegex(ValueError, "incomplete"):
            match()
