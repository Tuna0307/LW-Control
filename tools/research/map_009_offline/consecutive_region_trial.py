"""INERT trial: consecutive same-world AOI captures, one final restoration.

This expresses the acceptance contract needed before considering a live route.
It has no I/O, network, Unity or camera code and is NOT wired to production.

Grounding: current v24 managed WorldPointManager.UpdateLWAoi/SendAoiRequest,
HandleViewPointsReply, _curViewIndex, _splitLastAOIRequest; and the production
probe's UpdateViewRequest(true) + response-held snapshots. Actual native replies
are not known to carry this trial's adapter-supplied requestId/generation.
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Owner:
    profile: str
    session: str
    game_pid: int
    generation: int
    server: int
    world: int


@dataclass(frozen=True)
class View:
    tile_x: int
    tile_y: int
    zoom: float
    fov: float
    aspect: float


@dataclass(frozen=True)
class Region:
    request_id: str
    target_x: int
    target_y: int
    required_aoi: frozenset[int]


@dataclass(frozen=True)
class Reply:
    owner: Owner
    request_id: str
    target_x: int
    target_y: int
    server: int
    world: int
    generation: int
    segment: int
    final_segment: bool
    native_aoi: frozenset[int]
    record_keys: frozenset[str]
    native_flags_ready: bool
    native_split_pending: bool
    point_snapshot_ready: bool
    march_snapshot_ready: bool
    captured_after_request: bool
    expected_list_total: int | None = None


class TrialRejected(ValueError):
    pass


class ConsecutiveRegionTrial:
    """One pending request at a time; observations are accepted atomically.

    The caller must supply one exact owner/generation-bound native snapshot per
    region and authenticate its request envelope. This class never sends it.
    """

    def __init__(self, owner: Owner, home: View, regions: tuple[Region, ...],
                 world_aoi: frozenset[int]):
        if not regions or len({r.request_id for r in regions}) != len(regions):
            raise TrialRejected("regions must have unique request IDs")
        if not world_aoi or any(not r.required_aoi for r in regions):
            raise TrialRejected("coverage footprint must be defined")
        if not all(0 <= index < 10000 for r in regions for index in r.required_aoi):
            raise TrialRejected("native 100x100 AOI coordinate out of range")
        self.owner = owner
        self.home = home
        self.regions = regions
        self.world_aoi = world_aoi
        self.ordinal = 0
        self.records: set[str] = set()
        self.covered: set[int] = set()
        self.segments: dict[int, Reply] = {}
        self.restoration_pending = False
        self.finished = False
        self.aborted = False
        self.unrestorable_owner_change = False

    @property
    def pending(self) -> Region:
        if self.finished or self.aborted or self.ordinal >= len(self.regions):
            raise TrialRejected("no target region can be requested")
        return self.regions[self.ordinal]

    def accept(self, reply: Reply) -> str:
        """Return 'pending-split', 'next-region' or 'restore-home'."""
        target = self.pending
        if (reply.owner != self.owner or reply.generation != self.owner.generation or
                reply.server != self.owner.server or reply.world != self.owner.world or
                reply.request_id != target.request_id or
                (reply.target_x, reply.target_y) != (target.target_x, target.target_y)):
            raise TrialRejected("stale, reordered or wrong-owner/server/world response")
        if reply.segment < 0 or (reply.final_segment and reply.native_split_pending):
            raise TrialRejected("invalid native split completion state")
        if not reply.captured_after_request:
            raise TrialRejected("stale snapshot predates current request")
        if not (reply.point_snapshot_ready and reply.march_snapshot_ready):
            raise TrialRejected("point or march snapshot is not ready")
        prior = self.segments.get(reply.segment)
        if prior is not None:
            if prior != reply:
                raise TrialRejected("conflicting duplicate segment")
            return "pending-split"  # idempotent duplicate before completion
        self.segments[reply.segment] = reply
        if not reply.final_segment:
            return "pending-split"
        if not reply.native_flags_ready or reply.native_split_pending:
            raise TrialRejected("native view request not fully acknowledged")
        if set(self.segments) != set(range(reply.segment + 1)):
            raise TrialRejected("missing intermediate reply segment")
        covered = set().union(*(segment.native_aoi for segment in self.segments.values()))
        if not target.required_aoi <= covered:
            raise TrialRejected("AOI request footprint has a coverage gap")
        segment_rows = set().union(*(segment.record_keys for segment in self.segments.values()))
        totals = {segment.expected_list_total for segment in self.segments.values()
                  if segment.expected_list_total is not None}
        if len(totals) > 1 or (totals and len(segment_rows) != next(iter(totals))):
            raise TrialRejected("list count does not match independently supplied total")
        self.covered.update(covered)
        self.records.update(segment_rows)
        self.segments.clear()
        self.ordinal += 1
        if self.ordinal < len(self.regions):
            return "next-region"
        if not self.world_aoi <= self.covered:
            raise TrialRejected("full AOI union incomplete after last target")
        self.restoration_pending = True
        return "restore-home"

    def abort(self, current_owner: Owner) -> str:
        """Return safe restoration intent, or suppress it after owner replacement."""
        self.aborted = True
        self.segments.clear()
        if current_owner != self.owner:
            self.unrestorable_owner_change = True
            return "owner-changed-no-restoration"
        self.restoration_pending = True
        return "restore-home"

    def confirm_home(self, current_owner: Owner, actual: View) -> None:
        if not self.restoration_pending or current_owner != self.owner:
            raise TrialRejected("restoration cannot be attributed to original owner")
        if actual != self.home:
            raise TrialRejected("original camera tile/zoom/FOV/aspect not restored exactly")
        self.restoration_pending = False
        self.finished = not self.aborted
