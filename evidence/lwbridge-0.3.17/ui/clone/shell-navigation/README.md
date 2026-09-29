# Reproduced shell/navigation evidence

Work item: `LWB317-UI-005`

`shell-light-1120x720.png` is a headless-Chrome capture of the standalone
`src/LWBridge.UI-0.3.17` clone. The `1120 x 720` viewport was chosen as a stable
comparison canvas because it matches the previously measured auth-boundary
client size; it is **not** claimed as proof of the blocked post-auth reference
geometry.

The clone imports the recovered 0.3.17 stylesheet and status-dot raster assets
without modification. Runtime comparison against the original post-auth shell
remains blocked by the out-of-scope auth boundary.
