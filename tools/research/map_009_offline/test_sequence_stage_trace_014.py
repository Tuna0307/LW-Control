"""Execute generated private helpers, including the former snapshot failure."""
import unittest

from test_map013_sequence_instrument import SequenceInstrumentTests
from sequence_post_lens_014 import instrument_post_lens_refresh
from sequence_stage_trace_014 import instrument_subscription_stages


class SubscriptionStageTests(SequenceInstrumentTests):
    def source(self):
        return instrument_subscription_stages(
            instrument_post_lens_refresh(super().source().encode())).decode()

    def test_complete_first_camera_snapshot_survives_later_capture(self):
        lua, module, request = self.setup_runtime()
        first = lua.table_from({"checkpoint": "home-projection-and-transform"})
        second = lua.table_from({"checkpoint": "retained-first-region"})
        module.M._map014CameraRestore = first
        self.assertTrue(module.M._map013SequenceAdmit(request, "owned", 498, 63))
        module.M._map014CameraRestore = second
        module.change()
        self.assertTrue(module.M._map013SequenceAdmit(request, "owned", 455, 425))
        module.M._map013UseOriginalHome()
        self.assertEqual(module.M._map014CameraRestore.checkpoint,
                         "home-projection-and-transform")

    def test_old_helper_keeps_wrong_second_snapshot(self):
        # Replay the actual old generated helper, establishing the distinction.
        old = SequenceInstrumentTests()
        lua, module, request = old.setup_runtime()
        module.M._map014CameraRestore = lua.table_from({"checkpoint": "home"})
        self.assertTrue(module.M._map013SequenceAdmit(request, "owned", 498, 63))
        module.M._map014CameraRestore = lua.table_from({"checkpoint": "wrong-region"})
        module.M._map013UseOriginalHome()
        self.assertEqual(module.M._map014CameraRestore.checkpoint, "wrong-region")

    def test_exact_source_anchors_reject_drift(self):
        with self.assertRaisesRegex(ValueError, "anchor drift"):
            instrument_subscription_stages(b"not an owned generated sequence")

    def test_bounded_lens_is_opt_in_and_compiles(self):
        from lupa.lua53 import LuaRuntime
        original = instrument_post_lens_refresh(super().source().encode())
        ordinary = instrument_subscription_stages(original)
        candidate = instrument_subscription_stages(original, bounded_lens=True)
        self.assertIn(b'call(camera_manager, "SetFOV", request.coverageFov)', ordinary)
        self.assertIn(b'call(camera_manager, "SetFOV", 40.0)', candidate)
        self.assertNotIn(b'call(camera_manager, "SetFOV", request.coverageFov)', candidate)
        compiled = LuaRuntime().eval("load")(candidate, "owned-FOV40", "t")
        self.assertFalse(isinstance(compiled, tuple), compiled)


if __name__ == "__main__":
    unittest.main()
