# Shared runtime / Release — strict parity status

**Current through:** `LWB-R8-097`, 2026-09-27.

The previous Release page asked whether the reconstructed Home/Map build was stable. The current question is stricter: does the entire rebuilt program reproduce LWBridge 0.3.1 one-for-one and still work against the current Last War client?

### Owner working/acceptance reset — 2026-09-27

For owner-facing status, **WORKING is binary**. A feature is WORKING only when the recovered original LWBridge 0.3.1 logic for that feature is the production path and that path has succeeded against the real current Last War client. A live-successful substitute, compatibility reconstruction, `EQUIVALENT_REIMPLEMENTATION`, old custom scanner, fallback, test harness, or output-equivalent path is **NOT WORKING** for this acceptance label.

For Home and Map there is **no production fallback acceptance**. Equivalent implementations may remain only as historical evidence, comparison/oracle tooling, or isolated research/test harnesses. They must not silently activate, mask failure of the recovered path, or be used to report the feature as working.

Immediate priority is **original Map acquisition engine first**, then any remaining original Home lifecycle gaps. Freeze unrelated auth/entitlement/secondary-feature recovery unless it is a demonstrated direct dependency of Home/Map recovery. Server season progression is not evidence that the installed client implementation is incompatible; compatibility changes require concrete current-client evidence.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Shared parity requirements

- Preserve exact recovered frontend/runtime assets byte-for-byte.
- Recover original host/proxy request, readiness and failure semantics.
- Recover original launcher/profile/multi-hook behavior.
- Keep current-client compatibility changes below the observable parity boundary.
- Restore original product surfaces previously removed.
- Remove rebuild-only product behavior.
- Validate reference-vs-rebuild states, not only our own regression suite.
- Keep the reference artifact immutable and hash-check it before major recovery work.

## Old acceptance matrix

The R7 47-case matrix remains historical proof that selected reconstructed behaviors worked.

It is not the R8 release gate.

The R8 release gate is `docs/lwbridge-parity-matrix.md`: every required reference feature must be exact or proven equivalent, with no unexplained deviation/unknown, followed by integrated live acceptance.

## Compatibility

The owner explicitly prioritizes the end product working. Internal adapters may compensate for newer Last War builds, changed Lua/managed layouts, update sequencing, or process/runtime differences, but those adapters must not intentionally alter the original LWBridge UI or behavior.

## Validation expectation

Every future release candidate needs two classes of proof:

1. **Reference parity** — bytes, UI, contracts, errors, defaults, state transitions and command payloads match recovered original behavior.
2. **Current-client operation** — the parity implementation actually works against the installed Last War version.

A build passing only one class is not a finished release.
