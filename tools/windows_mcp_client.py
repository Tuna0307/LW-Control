"""Use the local Windows-MCP installation over stdio; list tools by default.

Desktop tool calls require the project/owner's separate authorization. This bridge
lets an owner-relayed worker use the MCP via Remote Desktop Commander shell tools
when its chat host does not expose Windows-MCP directly.
"""

import argparse
import asyncio
import base64
import json
import os
from pathlib import Path
import sys

from fastmcp import Client
from fastmcp.client.transports import StdioTransport


async def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tool", help="Exact registered name; omit to list schemas")
    parser.add_argument("--arguments-file", type=Path, help="UTF-8 JSON object")
    parser.add_argument("--image-output", type=Path, help="Save a returned image")
    parser.add_argument("--timeout", type=float, default=120)
    options = parser.parse_args()
    if not options.tool and (options.arguments_file or options.image_output):
        parser.error("Arguments/images require --tool")

    root = Path(__file__).resolve().parents[1]
    python = root / "Windows-MCP/.venv/Scripts/python.exe"
    source = root / "Windows-MCP/src"
    if not python.is_file() or not source.is_dir():
        raise SystemExit("Windows-MCP installation missing; see docs/WINDOWS_MCP_SETUP.md")
    arguments = {}
    if options.arguments_file:
        arguments = json.loads(options.arguments_file.read_text(encoding="utf-8-sig"))
        if not isinstance(arguments, dict):
            parser.error("Tool arguments must be a JSON object")

    transport = StdioTransport(
        command=str(python), args=["-m", "windows_mcp", "serve"], cwd=str(source),
        env={**os.environ, "ANONYMIZED_TELEMETRY": "false",
             "WINDOWS_MCP_WATCHDOG": "off", "PYTHONIOENCODING": "utf-8"},
    )
    async with asyncio.timeout(options.timeout):
        async with Client(transport) as client:
            if not options.tool:
                tools = await client.list_tools()
                print(json.dumps({"tools": [tool.model_dump(mode="json") for tool in tools],
                                  "desktopToolCalls": 0}, ensure_ascii=False))
                return
            result = await client.call_tool(options.tool, arguments, raise_on_error=False)
            contents = []
            image_index = 0
            for content in result.content:
                if content.type == "image":
                    mime_type = getattr(content, "mime_type", None) or getattr(content, "mimeType", None)
                    if options.image_output:
                        output = options.image_output
                        if image_index:
                            output = output.with_name(f"{output.stem}-{image_index}{output.suffix}")
                        if output.exists():
                            raise FileExistsError(f"Refusing to overwrite evidence: {output}")
                        output.parent.mkdir(parents=True, exist_ok=True)
                        output.write_bytes(base64.b64decode(content.data))
                        contents.append({"type": "image", "path": str(output.resolve()),
                                         "mimeType": mime_type})
                    else:
                        contents.append({"type": "image", "mimeType": mime_type,
                                         "saved": False})
                    image_index += 1
                else:
                    contents.append(content.model_dump(mode="json"))
            print(json.dumps({"isError": result.is_error, "content": contents},
                             ensure_ascii=False))
            if result.is_error:
                raise SystemExit(1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    asyncio.run(main())
