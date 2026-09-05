# GameAssembly RGMD runtime recovery

This note records read-only static analysis of the native Last War runtime used
to load the transformed `*.rdl` managed assemblies. No DLL was loaded into a
helper process during this pass, no game file was changed, and no bridge/game
command was sent.

## Current native runtime identity

The inspected file is:

`%LOCALAPPDATA%\FunFly\Last War-Survival Game\Game\GameAssembly.dll`

Current evidence on 2026-09-06:

- size: `4,387,296` bytes;
- SHA-256: `496fbb32195086deaf39221668957d130a96a94aa90f87dd5adaab23bb800279`;
- PE image base: `0x180000000`;
- the file contains one `RGMD` marker and four `BSJB` markers;
- the `RGMD` marker is at file offset `0x332B54`, RVA `0x333D54`.

`tools/inspect_gameassembly_rgmd.py` reproduces the hash/signature evidence and
uses `pefile` plus `capstone` without loading the DLL.

## Loader exports and call chain

The current DLL exports the relevant entry points with MSVC-decorated names:

- `InitMono` at RVA `0x3BC0`;
- `LoadAssemblyByName` at RVA `0x3CD0`;
- `LoadAssemblyWithImageBinary` at RVA `0x3CE0`;
- `SetAssemblyOnlyReadFromPackage` at RVA `0x3D40`;
- `il2cpp_init` at RVA `0x225F0`.

Static disassembly of `LoadAssemblyWithImageBinary` shows a small wrapper. It
first calls RVA `0x5320` to check for an already loaded assembly. If none is
found it calls RVA `0x6610`. That second routine calls RVA `0x28100` to create an
image from the supplied bytes, then RVA `0x6D200` to load an assembly from that
image, and finally RVA `0x48C0` to register the resulting assembly.

The RVA `0x28100` image creation path constructs an image object and calls RVA
`0x24BE0`. The latter initializes the image and walks a registered image-format
handler list. At RVA `0x24D70` it calls the first function pointer of each
handler until one returns true, then stores the chosen handler pointer at image
offset `+0x6D0`. Later stages call additional functions from that same handler.
This provides a concrete native route to the custom RDL parser.

One handler address is materialized at RVA `0x24E44` with a RIP-relative `lea`;
the resolved table address is RVA `0x332D00` in `.rdata`. The six recovered
callback pointers are:

| Index | Callback RVA | Recovered behavior |
| ---: | ---: | --- |
| 0 | `0x24220` | probes for `MZ` |
| 1 | `0x240E0` | normal image load path |
| 2 | `0x241B0` | later load/metadata stage |
| 3 | `0x24210` | returns success |
| 4 | `0x24240` | probes for `RG` |
| 5 | `0x24260` | custom RG image loader |

`0x24240` directly compares the first two input bytes with `0x52 0x47`, proving
the registered handler recognizes the custom `RG` container. `0x24260` repeats
that check, parses the custom image header, and marks the image as an RG image.

## Recovered 32-bit transform

The RG loader also exposes the exact transformation used on encoded 32-bit
metadata values. When the RG-image flag is set, native code repeatedly performs:

`ror32(((encoded ^ 0xA5A5A5A5) - 0x075BCD16) ^ 0x3ADE68B1, 5)`

with 32-bit wrapping subtraction. The exact machine-code sequence occurs six
times inside the RG loader beginning at RVA `0x245B3`. It appears another fifteen
times beginning at RVA `0x27191`, inside the metadata-stage routine reached from
callback `0x241B0` through RVA `0x270D0`.

Applying this native formula to stored BaseUtils operands restores normal
ECMA-335 tokens across several tables. Examples include:

- `0x679F3862 -> 0x040005C0`: FieldDef RID 1472, the LENC key initializer;
- `0x679F9902 -> 0x04000039`: FieldDef RID 57, `ChaCha20.Constants`;
- `0xA79F9182 -> 0x0600007D`: MethodDef RID 125, the three-argument
  `ChaCha20.Xor` wrapper;
- `0x279FBD03 -> 0x0A000119`: MemberRef RID 281,
  `RuntimeHelpers.InitializeArray`;
- `0x879F9A82 -> 0x010000D5`: TypeRef token.

`tools/rdl_il.py` now exposes both the decoder and its inverse. The read-only
MemberRef inspector uses the inverse for RGMD assemblies and now finds the real
stored call sites; for example, it resolves the two `InitializeArray` calls in
`LencCodec..cctor` from stored operand `0x279FBD03`.

## Direct `RGMD` references

The first executable-section xref scan found no direct RIP-relative reference or
inline four-byte comparison to the `RGMD` string marker. The handler does not
need that literal to recognize the container: the recovered probe checks the
leading `RG` bytes directly.

The next static step is to continue through RVA `0x270D0` and its callees to map
which image/metadata fields receive the transform and then locate the runtime
path that accounts for `ChaCha20.Constants`. A previous isolated `InitMono` call
crashed because Unity/IL2CPP initialization prerequisites were absent, so runtime
execution is not being retried while the static path is available.
