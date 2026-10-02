# LWB317-UI-MAP-NAVIGATION-001 — worker delivery

Status: AWAITING_REVIEW. This is a worker implementation handoff; the project
lead owns the final acceptance decision.

The assigned defect was MapDataPage clearing page, rows and total on every tab
change. Recovered LWBridge 0.3.17 instead caches each normal tab's view for the
mounted component, restores it when returning and then refreshes it.

The production correction adds that mounted normal-tab cache, preserves a
same-tab no-op, restores cached page/rows/total, starts uncached tabs at page 1
with empty rows/loading, and keeps Scheduled Plunder outside the normal cache.
Searches use a monotonically increasing generation so stale success, failure or
finally callbacks cannot mutate a later tab. Server changes and server loss
invalidate cached views.

Exact original evidence is hash-locked in the work-item packet. Revalidated
UTF-8 byte anchors are ye 554, server transition 33502, normal-query effect
34677, rr 38513 and ir 39238. The saved pre-edit MapDataPage baseline reproduces
six distinguishing navigation failures. The corrected component passes the
same actual-callback/effect harness with persistent hook state and deferred
promises across 17 searches.

Required scenarios pass: Truck page 2 -> Train -> Truck, independent pages,
cached/uncached loading, active-tab no-op, rapid A/B/A switching, obsolete
success/failure/finally, current failure, shrinking page clamp/requery, positive
server change, server loss and Scheduled Plunder entry/exit.

Real browser preview used the required map-truck URL in light English mode.
Before claiming navigation success it visibly confirmed server 321, Truck total
55 and populated rows. Truck page 2 showed Truck Owner 51; Train opened on page
1; returning restored Truck page 2 and Truck Owner 51. Scheduled Plunder removed
normal pagination and exit restored Truck page 2. Captured browser console
errors: zero. Saved screenshots also show populated, loading and deterministic
error states. Preview remained online:false.

Focused FILTERS-001, Treasure Checking and row-action executable regressions
pass. npm.cmd run check, npm.cmd run build and npm.cmd run
check:production-build pass. Production package fingerprints at verification
were 852c81285e576bdd8f31c22426a3ae2df56258ffaa736ae9974739ebd1f9294a
and a450ce2cc43c1be1dfd5e6d0e998995f594486a114598be11ead23e9427e301d.

Historical LEAD-TABLES and transport executable checkers predate the accepted
per-kind item-filter setter and now fail while evaluating changeItemFilter.
Their evidence validators/manifests were not rewritten. The historical FILTERS
validator similarly pins the pre-navigation MapDataPage hash and is expected to
reject a changed production file. This work item has a dedicated evidence
validator that passes current production and the protected-WIP manifest.

No scan, clear, jump, export, mark, plunder, claim, scheduling or other gameplay
operation was executed. Open separate scopes remain keyword/debounce recovery,
selection/plunder lifecycle, native Treasure frontend/context refresh wiring,
Scheduled Plunder operations, native text/image integration and original
post-auth pixel comparison.
