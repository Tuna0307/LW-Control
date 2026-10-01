# LWB317-PM-004 — owner scope clarification

Date: 2026-10-01 (Asia/Singapore).
Starting checkpoint: `8e77843b48b92928f2c2b4cba07ab2b74ec40b9f`.
Source: owner's message in the project-lead chat on this date.

The owner confirmed that the clone does not need a login page and permits
auth-related research where required to produce the working in-scope clone.
This supersedes earlier blanket bans on local auth dependency research. Current
rules are recorded in `AGENTS.md` section 6 and the current task/parity documents.
Required local state producers/consumers may be traced using supplied artifacts
and authorized access. This does not authorize defeating original-program or
service access controls or obtaining other people's credentials. The original
commercial account/licensing system is not added as a clone product feature.

The owner wants one production implementation, with defects fixed in that path
rather than a fallback. Historical sources remain evidence. The existing
selectable `--legacy-ui` option is still present in code; its retirement is an
explicit pending bounded host/package task. No runtime option was removed by
this documentation checkpoint, and no new fallback is authorized.

The next worker remains `LWB317-UI-HOME-STATES-001`: complete exact-source Home
render states and isolated clone QA. It can inspect required auth-related local
rendering-state dependencies, but does not launch/control the game, perform
native lifecycle integration, bypass original access controls or begin unrelated
service/protocol research. The dispatch supplies the new exact starting SHA.

This checkpoint changes scope documentation and the assignment only. No code,
reference bytes or historical evidence changed; `git diff --check` passed.
