# Equipment Motion 0.3.17 recovered dependency notice

The exact runtime slice in `equipmentMotion317.js` is recovered from the
owner-supplied LWBridge 0.3.17 `SquadPanel-HC3-DJei.js` asset. Its dependency graph,
byte range and hash are pinned in the final-integration motion evidence packet.
Its package version is not embedded in the supplied asset and remains UNKNOWN.

The source's optional-import error identifies `framer-motion`. The upstream
project is Motion (formerly Framer Motion), authored by Matt Perry / Motion B.V.
and previously Framer B.V. Upstream primary license references inspected on
2026-10-04:

- https://github.com/motiondivision/motion/blob/main/packages/motion/LICENSE.md
- https://github.com/motiondivision/motion/blob/main/packages/framer-motion/LICENSE.md

Current upstream Motion is MIT licensed. This provenance observation does not
identify a guessed original package version. The extracted browser library is
kept separate from original application, authentication, service and native
implementation code; canonical React and JSX imports replace bundled runtime
imports. Inert original bundler helpers are retained as exact pinned bytes.
