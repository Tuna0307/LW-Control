# PM-016 — accept shared switch state descriptions

Date: 2026-10-02. Reviewed commit: 47c243af1a311e331b6ac0f51d84e16782f32c47.
Disposition: **COMPLETE / ACCEPTED for focused source/local switch descriptions**.
LWB317-UI-SWITCH-LOCALE-001 is closed. Other Home lead deliveries and Trade 003E
remain AWAITING_REVIEW; global UI/native/original-pixel parity is not accepted.

The lead inspected the entire product diff and focused worker harness. Only
ToggleRow adds useI18n and translates its checked-state suffix. An independent
scope checker reverses exactly these two additions in the committed product file
and compares all remaining bytes with assignment baseline 1cfce34. All callers,
visible content, handlers, predicates, CSS and other product files are unchanged.
The current product file matches the submitted commit. Exact original/production
hashes and helper/11-caller byte locators validate independently.

Original Bn is at zero-based UTF-8 byte 213332 in index-BVfnK1wp.js, SHA-256
44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Its label is translated internally; clone callers already supply the translated
label. The disclosed adapter preserves that distinction. Clone optional-callback
behavior is intentionally retained, rather than falsely claimed as original parity.
Target executable hash rechecked against AGENTS.md.

Independent reruns pass the actual original/production 360-case nine-locale
comparison, enabled callback/optional callback checks and evidence validation.
The three current Home saved-report regressions pass unchanged. Canonical check,
rebuilt production package and package verification pass with worker fingerprints:
53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e /
96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782.
git diff --check passes; HEAD and direct remote were the exact submitted SHA.

Worker browser evidence records Settings Show FPS off/on/off with unchanged
visible label and translated state descriptions; Japanese Home checked/unchecked
disabled descriptions and rejected disabled input. Lead inspected the recorded
JSON and verified/viewed the nonempty Japanese screenshot. The screenshot is
disclosed as an isolated Edge capture; it illustrates visible content and cannot
prove the inaccessible aria-label suffix. The source/render comparison and browser
DOM observations establish that suffix. No new lead browser/game session was used.

Evidence: evidence/lwbridge-0.3.17/ui/LWB317-PM-016/lead-results.json and replay
check-switch-scope.mjs, plus the preserved worker evidence/review. Unrelated AFK,
scratch and parent screenshot WIP remains unstaged. Rebuild includes those existing
AFK fixture bytes; this review does not accept them or assert a clean-tree build.

Next bounded assignment: independent review of HOME-ERROR-001 translation only,
implemented by the lead at 537a2b3 and retained in the current checkout. This
closes review debt without starting native lifecycle or another backend family.
