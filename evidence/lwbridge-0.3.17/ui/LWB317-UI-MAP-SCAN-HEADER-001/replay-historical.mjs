import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
process.env.NODE_OPTIONS=`${process.env.NODE_OPTIONS || ''} --import=${pathToFileURL(path.join(here,'legacy-bindings.mjs')).href}`.trim();
await import('../LWB317-UI-MAP-INTERACTIONS-001/replay-historical.mjs');
