# Reproduced page evidence

Work item: `LWB317-UI-006`

These screenshots capture the standalone `src/LWBridge.UI-0.3.17` clone at a
fixed `1120 x 720` browser viewport. `previewPage=<route>` is a clone-only
nonvisual evidence selector used to make repeatable captures; normal startup
still opens the exact recovered `overview` / Home route.

The screenshots are implementation evidence. They are **not** reference-runtime
visual proof because the original post-auth UI remains blocked by the excluded
LWBridge authorization boundary.

Dynamic runtime values that were not recoverable are either left unloaded,
rendered with exact disconnected/processing/empty-state copy, or represented by
deterministic disabled clone fixtures whose state is not claimed as a reference
default.

SHA-256:

- `overview-1120x720.png` — `7015C4B3AB13EBE9DC0114938084CF654B4C4CD5A1B2596238A79D2F32A23909`
- `automation-1120x720.png` — `2F2E04B64EEEBC536A24CF91423FAFEF128683BA704002AD67500D4987F1270C`
- `map-data-1120x720.png` — `482C066208D4C1C59DBD566BAEE9370BC59D4722EBB8340E24FF827117ADA54A`
- `march-1120x720.png` — `B8901E548954BE2AFA7DD8E037A255E45E04442B6D43FB94B4BF64F4F25E604A`
- `city-layout-1120x720.png` — `E5F4C6B880FC4315C6CB56569037FD464693A544B675C6A736B664D609139075`
- `hotkeys-1120x720.png` — `A35A05DF484D41E829CB2B879FE98DF769B4B6C48F5CAD11E56399A5F3A2018B`
- `mini-games-1120x720.png` — `6AC5C48A2EF1FF62D966E87774D8459108457725CAB5D742D5741536A729FD4D`
- `settings-1120x720.png` — `F239BA14E09C6AF9C3E313EFD99A5F0E01C6838DAE2B72800A9A66A74F4FA248`
