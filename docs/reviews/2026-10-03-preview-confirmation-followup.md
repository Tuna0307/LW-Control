# Delayed local preview confirmation — owner follow-up

The owner supplied a screenshot of a delayed 127.0.0.1:4319 browser confirmation:
Delete “Lead checkpoint B”? The owner reports clicking Cancel.

This originated from the lead's explicit local two-profile AFK QA during
AUTOMATION-AFK-CLOSEOUT-001. Lead checkpoint B was the renamed synthetic Gold
profile, not a live game profile. The Delete input stalled the IAB connector;
its dialog retrieval also stalled. Closing the owned tab did not prevent this
confirmation from subsequently appearing in the app. No claim that tab closure
had dismissed the dialog is warranted. Cancellation was the correct response to
leave the test profile intact; the resulting preview state was not independently
read after the owner's click.

The prior evidence already records physical confirmation as unverified and proves
cancel/accept only through inert actual original/current callbacks. Its pinned
browser record and manifest are preserved unchanged. Future bounded Equipment
QA should avoid opening native JS confirmation dialogs; verify any destructive
confirmation with inert handlers and explicitly disclosed disposable test data.
