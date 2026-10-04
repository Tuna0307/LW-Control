# Independent review and lead disposition

Read-only reviewer inspected actual original/current code, raw results, browser
records and saved images. The lead independently reran renderers, browser
capture, inert selection case, repository checks and production-package check.

| Reported issue | Lead validity/disposition | Change made |
|---|---|---|
| Original/current extraction is faithful for static scope | Valid; original body and current JSX execute without presentation rewrites. Persistent-hook/native substitutions remain explicit. | No production change. |
| Five mismatch categories are real | Valid; raw markup remains different and provider guard explains only sampled visual difference. | Preserve all findings; no blanket parity pass. |
| Last selected type cannot be deselected in clone | Valid; independently reproduced original [] vs current [city]. Both UI predicate and helper block it. | Added reproducible inert case and four exact source slices; defect remains open. |
| Reused harnesses were not pinned | Valid integrity gap. | Added dependency/import closure and asset pins to final manifest/validator. |
| Error proof does not cover local action alert | Valid coverage limit. | Explicitly restrict error claim to lastError/status; alert branch remains unproved here. |
| Browser readyState alone can race navigation | Valid harness risk; existing images already showed correct documents. | Require matching URL/title/lang/theme and fonts ready; reran all eight captures. |
| Rectangle tolerance hides subpixel differences | Lead improvement: preserve any measured delta. | Changed comparison to strict equality; still zero deltas. |

The lead accepts the comparison packet only. No code correction, full-shell,
native or protected original-runtime pixel acceptance is implied.
