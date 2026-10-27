"""Inert counterexample to an unqualified march-cache optimization.

This is a hypothetical event schedule permitted by the recovered native
receive->handle->cache separation, NOT a real game trace. It distinguishes
per-frame snapshots from a tempting receipt-stamp-only optimization. It must
never be used as evidence of live performance or actual native row loss.
"""

import unittest


def observed_rows(events, *, only_on_new_receipt: bool) -> set[str]:
    seen: set[str] = set()
    previous_receipt: int | None = None
    for receipt_stamp, native_cache in events:
        fresh_receipt = receipt_stamp != previous_receipt
        previous_receipt = receipt_stamp
        if only_on_new_receipt and not fresh_receipt:
            continue
        seen.update(native_cache)
    return seen


class DeferredNativeHandlingCounterexample(unittest.TestCase):
    def test_late_cache_population_after_unchanged_receipt(self):
        # The response timestamp has changed while the asynchronous handler
        # has not yet populated its TrainData row. A subsequent frame can show
        # the row even though the receive timestamp is unchanged.
        events = [(100, ()), (101, ()), (101, ("train-current",))]
        self.assertEqual(observed_rows(events, only_on_new_receipt=False), {"train-current"})
        self.assertEqual(observed_rows(events, only_on_new_receipt=True), set())

    def test_receipt_and_cache_coincident_do_not_distinguish_policies(self):
        events = [(100, ()), (101, ("train-current",)), (101, ("train-current",))]
        self.assertEqual(observed_rows(events, only_on_new_receipt=True),
                         observed_rows(events, only_on_new_receipt=False))


if __name__ == "__main__":
    unittest.main()
