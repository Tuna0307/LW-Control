# LWBridge 0.3.17 tools

Reserved for tools written specifically for the 0.3.17 target.

## `extract_frontend_package.ps1`

Hash-locked static extractor for `LWB317-UI-001A`. It reads the proven Tauri
asset table in the exact 0.3.17 executable, preserves each embedded Brotli blob,
losslessly expands the web asset bytes, extracts the relevant PE resources, and
writes a manifest with byte locators, sizes, and SHA-256 hashes.

Run with PowerShell 7+:

```powershell
pwsh -NoProfile -File tools/lwbridge317/extract_frontend_package.ps1 `
  -ReferencePath 'C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe' `
  -OutputRoot 'evidence\lwbridge-0.3.17\ui\frontend-package'
```

The script refuses to run when the reference SHA-256 differs from the 0.3.17
project baseline.

Each future tool should document:

- target SHA-256/version;
- purpose;
- exact invocation;
- expected output;
- related `LWB317-*` finding.
