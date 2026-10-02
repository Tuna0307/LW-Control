# PM-028 — Map filter lifecycle acceptance

Reviewed submitted R1 `ff4ed369a132f4b3646188d5afdc81ad2d59383d`.
Decision: **ACCEPT parent FILTER-LIFECYCLE-001 and R1 for focused source/local UI scope**.

Lead inspected the production correction and recovered state correspondence:
original C.serverId maps to canonical scanState.serverId; original local R maps
to browse/data server. The combined effects now produce [321,322,321] and settle
at 321 for the PM-027 redirect. Positive-server loss behavior is independently
covered by the worker's exact original/current runner. The corrected delayed
Clear path remains [322,321]. PM-027's failed baseline is retained.

Lead executed the unchanged six-case PM-027 test (6/6), five-case R1 comparison
(5/5), output-redirected parent lifecycle assertions, R1 evidence validator,
request lifetime (38, zero current failures), navigation (17 searches, zero current
failures), interactions (38/38), strict integration (14/14), and historical
filter/table/Checking/row/state replay. All pass. Canonical check/build/package
pass with fingerprints df8d3132fea74cdbbe3b8ee9cff964e5f3fcf76c7edf935fff565cb2d4566e9c /
5e4b411c08e9e69a28d75cdfccc36a751ddea0f5407c30c915ae9c83f8310589.

The submitted browser smoke is worker evidence; no new lead browser session was
needed for this state correction. Native actions/persistence, original post-auth
pixels, and overall Map/global UI parity remain unproved. Protected WIP remains
unchanged. The parent JSON's reported CRLF-only working-tree change is preserved,
not staged or discarded. No product edits are part of this acceptance.

The next lead-owned UI unit is MAP-TREASURE-PICKER-001: replace the native select
with the exact recovered details/menu surface, names/counts, strict key selection
and closing behavior. No native Treasure producer/action integration is assigned.
