# LWBridge 0.3.17 frontend package evidence

Work item: `LWB317-UI-001A`

Reference executable:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

This directory contains losslessly recovered frontend bytes from the exact
reference executable.

- `web/` — Brotli-expanded frontend files using their embedded asset paths.
- `embedded-brotli/` — the exact compressed blobs stored in the executable.
- `pe-resources/` — exact PE icon/group-icon/manifest/version resource bytes.
- `frontend-package-manifest.json` — source offsets, sizes, and SHA-256 hashes
  for every recovered web asset and extracted PE resource.

The extractor is hash-locked to the reference above:

```powershell
pwsh -NoProfile -File tools/lwbridge317/extract_frontend_package.ps1 `
  -ReferencePath 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe' `
  -OutputRoot 'evidence\lwbridge-0.3.17\ui\frontend-package'
```

See `docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`
for the evidence interpretation and scope limits.
