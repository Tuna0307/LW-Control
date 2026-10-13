"""MAP-012: inert independent source/storage snapshot comparison.

No game, transport, database mutation or authority inference. Snapshots here
are caller-supplied *independent observations*, never counts invented from a
requested region or from the current production scan. A point with a changed
value outside a bounded observation bracket is a mismatch, while live objects
which may move/expire are explicitly categorized as inconclusive unless
independently time-bracketed.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable


@dataclass(frozen=True)
class Cell:
    kind: str
    server: int
    world: int
    key: str
    captured_ms: int
    fields: tuple[tuple[str, str], ...]


@dataclass(frozen=True)
class Difference:
    category: str
    key: str
    detail: str


def aoi_index(tile_x: int, tile_y: int, size: int = 10, columns: int = 100) -> int:
    if not (size > 0 and columns > 0 and 0 <= tile_x < size * columns
            and 0 <= tile_y < size * columns):
        raise ValueError("invalid target tile/AOI geometry")
    return (tile_y // size) * columns + tile_x // size


def audit(
    native: Iterable[Cell],
    stored: Iterable[Cell],
    *,
    server: int,
    world: int,
    earliest_ms: int,
    latest_ms: int,
    mutable_kinds: frozenset[str] = frozenset({"monster", "truck", "railway", "dispatch", "ghost", "treasure"}),
) -> list[Difference]:
    if server <= 0 or world < 0 or earliest_ms > latest_ms:
        raise ValueError("invalid authoritative comparison context")
    errors: list[Difference] = []
    snapshots: list[dict[tuple[str, str], Cell]] = []
    for side, records in (("native", native), ("stored", stored)):
        seen: dict[tuple[str, str], Cell] = {}
        for item in records:
            ident = (item.kind, item.key)
            if not item.kind or not item.key or not item.fields:
                errors.append(Difference("invalid", item.key, side + ": missing identity/fields"))
                continue
            if item.server != server or item.world != world:
                errors.append(Difference("wrong-context", item.key, side))
            if not earliest_ms <= item.captured_ms <= latest_ms:
                errors.append(Difference("stale", item.key, side))
            if ident in seen:
                errors.append(Difference("duplicate", item.key, side))
            seen[ident] = item
        snapshots.append(seen)
    native_map, stored_map = snapshots
    for ident in sorted(native_map.keys() | stored_map.keys()):
        n, s = native_map.get(ident), stored_map.get(ident)
        kind, key = ident
        if n is None or s is None:
            category = "time-bracket-needed" if kind in mutable_kinds else "missing" if s is None else "extra"
            errors.append(Difference(category, key, "native-only" if s is None else "stored-only"))
        elif dict(n.fields) != dict(s.fields):
            errors.append(Difference("field-mismatch", key, str(sorted(
                field for field in dict(n.fields).keys() | dict(s.fields).keys()
                if dict(n.fields).get(field) != dict(s.fields).get(field)))))
    return errors
