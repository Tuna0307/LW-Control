"""Inert source and retained-receipt inverses for remote scan feasibility."""

import json
import unittest
from pathlib import Path

import map_016_remote_feasibility as feasibility


class RemoteFeasibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.il = feasibility.IL.read_text(encoding="utf-8")
        cls.sampler = feasibility.SAMPLER.read_text(encoding="utf-8")

    def test_current_native_guard_is_before_protobuf(self):
        result = feasibility.inspect_contract(self.il, self.sampler)
        self.assertTrue(result["singleServerGuardBeforeProtobuf"])
        self.assertFalse(result["samplerEmitsOriginalProtobufBytes"])
        with self.assertRaises(ValueError):
            feasibility.inspect_contract(self.il.replace("IL_005e: ret", "IL_005e: nop"), self.sampler)
        with self.assertRaises(ValueError):
            feasibility.inspect_contract(self.il.replace("ISFSArray::GetByteArray", "ISFSArray::GetSFSObject"),
                                         self.sampler)

    def test_observer_does_not_elevate_request_or_fake_shape_to_source(self):
        point = {"keys": [], "mapNumbers": {"pointId": None, "type": None}}
        segment = {"numbers": {"serverId": 2213, "worldId": 0, "viewLvl": 0,
                                "x": 545, "y": 495},
                   "arrayCounts": {"points": 12}, "pointShapes": [point]}
        def snapshot(direction, source):
            return {"json": json.dumps({"packets": [
                {"direction": direction, "command": "world.get.block",
                 "packetHash": 1, "segments": {"count": 1, "entries": [source]}}]})}
        requested_only = feasibility.summarize_snapshots([snapshot("send", segment)], 2212)
        self.assertEqual(requested_only["foreignServerSegments"], 0)
        received = feasibility.summarize_snapshots([snapshot("receive", segment)], 2212)
        self.assertEqual(received["foreignServerSegments"], 1)
        self.assertEqual(received["pointCountReported"], 12)
        self.assertEqual(received["pointShapeSamples"], 1)
        self.assertEqual(received["nonemptyPointShapes"], 0)
        self.assertEqual(received["observedProtobufByteArrays"], 0)
        decision = feasibility.evaluate(
            feasibility.inspect_contract(self.il, self.sampler),
            {**received, "tracesWithKnownCurrentServer": 1})
        self.assertFalse(decision["productionRemoteProducerQualified"])
        self.assertTrue(any("byte arrays" in item for item in decision["missing"]))

    def test_retained_traces_have_no_off_server_typed_point_payload(self):
        paths = sorted(feasibility.ARCHIVE.glob(
            "native-causal-*/live-resource/map013-native-trace-result.json"))
        if not paths:
            self.skipTest("ignored retained native archive not installed")
        counts = feasibility.audit_archive(paths)
        self.assertGreater(counts["tracesWithKnownCurrentServer"], 0)
        self.assertGreater(counts["sameServerSegments"], 0)
        self.assertEqual(counts["foreignServerSegments"], 0)
        self.assertEqual(counts["observedProtobufByteArrays"], 0)
        self.assertEqual(counts["nonemptyPointShapes"], 0)


if __name__ == "__main__":
    unittest.main()
