# Independent Equipment motion integration review

Recommendation: **ACCEPT for source/local Equipment motion scope after the corrected position element tag**, subject to project-lead acceptance. No unresolved assigned-scope defect remains.

I read the actual current SquadsPage/EquipmentMotion/vendor implementation, the pre-motion canonical snapshot, exact original Squad callers and the original modal hierarchy. I ran the source/current engine proof and independently checked the integrated declarations, JSX handlers, presence boundaries and closure identity.

## Findings evaluated

| Finding | Assessment | Reason / change |
|---|---|---|
| Position caller used motion.div while the original uses motion.article | **Valid; corrected** | Exact original Squad asset byte **188223** calls `td.article`, with class at 188247. Current caller now uses `motion.article` opening/closing. Independent integration assertion pins the corrected tag. |
| Motion integration might change busy/save/rename/drag callbacks or timers | Invalid after inspection | The complete EquipmentContent prefix before JSX is byte-identical to the preserved baseline after excluding the single added `useReducedMotion` declaration. All **26** JSX event-handler attributes are exact. EquipmentDialog and other 47 declarations are unchanged. |
| AnimatePresence might close the rename native dialog too early or omit source exit behavior | Invalid | The rename dialog remains the direct conditional child of the transient presence boundary; its motion.div stays inside the native EquipmentDialog. The original hierarchy at bytes 189760–191200 has the same modal/presence structure. The source-shaped engine keeps the child mounted through exit, then unmounts and allows the unchanged dialog effect/focus cleanup. |
| Preset switching may overlap old/new content or lose layout IDs | Invalid | Canonical presets are keyed by preset ID inside `AnimatePresence mode="wait"`; slots retain layout/layoutId from the saved equipment identity. The source/current engine proof detects old-key retirement before next entry and compares the exact props. |
| Reduced-motion preference should dynamically update mounted panels | Invalid as a requested parity change | The recovered hook snapshots preference until remount; exact source/current proof verifies that behavior. The clone uses that original hook and suppresses only the properties suppressed by original callers. No redesigned responsiveness was added. |
| Copied motion engine may depend on hidden foreign/native imports | Invalid for this closure | Independent proof verifies the actual vendor contains the exact **125,058-byte** original closure and exact **836-byte** runtime helper, with only existing `react` and `react/jsx-runtime` imports. Closure globals are browser/runtime primitives. No native/game provider is connected. |

Runtime proof passes **84 original/current prop comparisons**, **seven current/original element tags**, and **12 mounted engine cases** covering entry frames, wait presence, transient exit/effect cleanup, hover/tap, reduced-motion snapshot and unmount during entry. `route-loading/check-integration-review.mjs --record` independently verifies handler/effect preservation and corrected integrated hierarchy.

These are exact-source/local engine and integration results. Physical HTML5 Equipment drag, live providers/persistence/gameplay and original protected-runtime pixels remain separate proof limits. Browser motion/layout observations do not substitute for those missing proofs.
