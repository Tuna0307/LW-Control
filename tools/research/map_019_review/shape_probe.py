"""Private-only 80/160 AOI experiment; normal probe source remains byte-identical.

No parallel requests, larger AOI cap, game actions or production integration.
Only the shape-review diagnostic scenario may use this transformed module.
"""
def instrument(source: bytes) -> bytes:
    changes = (
        (b"int32 = CS.System.Int32, nextAoi = 0, pending = nil,",
         b"int32 = CS.System.Int32, nextAoi = 0, pending = nil,\n"
         b"            map019AreaCap = values.map019AreaCap == '80' and 80 or 160,"),
        (b"math.min(9999, s.nextAoi + 159)",
         b"math.min(9999, s.nextAoi + s.map019AreaCap - 1)"),
        (b"completedAois = s.completed, requestsSent = s.sent,",
         b"map019AreaCap = s.map019AreaCap,\n        completedAois = s.completed, requestsSent = s.sent,"),
    )
    transformed = source
    for before, after in changes:
        if transformed.count(before) != 1:
            raise ValueError("MAP019 shape source seam is missing or nonunique: " + repr(before))
        transformed = transformed.replace(before, after)
    return transformed
