"""Build the managed SendAoi bridge used by the bounded World AOI probe."""

from __future__ import annotations

import hashlib
from pathlib import Path
import subprocess


CSC = Path(r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe")
SOURCE = Path(__file__).with_name("world_block_sender.cs")


def build(output: Path) -> dict[str, object]:
    if not CSC.is_file():
        raise RuntimeError(f"C# compiler is unavailable: {CSC}")
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    proc = subprocess.run(
        [
            str(CSC),
            "/nologo",
            "/target:library",
            "/optimize+",
            f"/out:{output}",
            str(SOURCE),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    if proc.returncode != 0 or not output.is_file():
        raise RuntimeError(
            "WorldBlockSender build failed: " + (proc.stderr or proc.stdout).strip()
        )
    payload = output.read_bytes()
    return {
        "path": str(output),
        "bytes": len(payload),
        "sha256": hashlib.sha256(payload).hexdigest(),
        "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    }
