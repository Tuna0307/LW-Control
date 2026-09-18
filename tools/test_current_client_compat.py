from __future__ import annotations

import current_client_compat as compat


def check(value: str, content: int, marker: int | None, error: str | None) -> None:
    actual_marker, actual_error = compat.validate_version_marker(value, content)
    assert actual_marker == marker, (value, content, actual_marker, marker)
    assert actual_error == error, (value, content, actual_error, error)


check("19", 19, 19, None)
check("18", 19, 18, None)
check("20", 19, 20, "version.txt is newer than the current LWLF content version")
check("0", 19, 0, "version.txt is not a positive numeric Lua content marker")
check("not-a-version", 19, None, "version.txt is not a positive numeric Lua content marker")
print("current-client compatibility marker policy: ok")
