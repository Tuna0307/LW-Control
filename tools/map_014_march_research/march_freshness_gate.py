"""Pure offline qualification gate for a future *passively observed* march trace.

This is not an active observer. It performs no game I/O, sends no messages,
and never turns AOI coverage into a claim of complete march acquisition.
Observer events must be source-attributed by the exclusive native session owner.
"""


def _rect(value):
    return (tuple(value) if isinstance(value, list) and len(value) == 4
            and all(type(n) in (int, float) for n in value)
            and value[0] < value[2] and value[1] < value[3] else None)


def assess_region(trace):
    """Return conservative barrier and same-time inverse verdict.

    Expected input: one target region with pointReady and observed march
    request/response/processing/snapshot, plus independent time-matched oracle.
    No guessed network request IDs. Correlation IDs are observer-local sequence
    numbers only when causally tied to source-attributed events.
    """
    problems = []
    if trace.get("pointReady") is not True:
        problems.append("point AOI response not ready")
    server = trace.get("serverId")
    world = trace.get("worldId")
    rect = _rect(trace.get("targetMarchRect"))
    if type(server) is not int or server <= 0 or type(world) is not int or rect is None:
        problems.append("missing valid target server/world/march rectangle")
    request = trace.get("request")
    response = trace.get("response")
    process = trace.get("processing")
    snapshot = trace.get("snapshot")
    oracle = trace.get("independentOracle")
    for name, item in (("request", request), ("response", response),
                       ("processing", process), ("snapshot", snapshot)):
        if not isinstance(item, dict) or item.get("sourceObserved") is not True:
            problems.append(f"no source-observed march {name}")
    if problems:
        return {"barrierPass": False, "sameTimeInversePass": False, "reasons": problems}

    for name, event in (("request", request), ("response", response), ("processing", process),
                        ("snapshot", snapshot)):
        if event.get("serverId") != server or event.get("worldId") != world:
            problems.append(f"{name} owner/server mismatch")
        if _rect(event.get("rect")) != rect:
            problems.append(f"{name} march rectangle mismatch")

    seq = request.get("observerLocalSequence")
    if type(seq) is not int or seq < 0 or response.get("observerLocalSequence") != seq or process.get("observerLocalSequence") != seq:
        problems.append("no exact observed march request/reply/process correlation")
    sent = request.get("atMs")
    recv = response.get("atMs")
    handled = process.get("atMs")
    sampled = snapshot.get("atMs")
    if not all(type(value) in (int, float) and value > 0 for value in
               (sent, recv, handled, sampled)) or not (sent <= recv <= handled <= sampled):
        problems.append("march receive/processing/snapshot timestamps not monotonic")
    if type(process.get("pendingQueueLength")) is not int or process["pendingQueueLength"] != 0:
        problems.append("march processing queue not drained or not observable")
    if process.get("processingFinished") is not True:
        problems.append("march processing completion not observed")
    barrier = not problems
    if not isinstance(oracle, dict) or oracle.get("sourceObserved") is not True:
        problems.append("no independent same-time native march comparison")
    else:
        if oracle.get("serverId") != server or oracle.get("worldId") != world or _rect(oracle.get("rect")) != rect:
            problems.append("independent oracle is for a different world/rectangle")
        # A one-second local observation-skew ceiling is an intentionally strict
        # controlled inverse policy, NOT a native server SLA or fixed wait.
        if (type(oracle.get("atMs")) not in (int, float)
                or type(sampled) not in (int, float)
                or not (0 <= oracle["atMs"] - sampled <= 1000)):
            problems.append("independent oracle not contemporaneous with candidate snapshot")
        identities = oracle.get("activeMarchUUIDs")
        observed = snapshot.get("marchUUIDs")
        if not isinstance(identities, list) or not isinstance(observed, list) or not all(
            isinstance(u, str) and u for u in identities + observed
        ):
            problems.append("native march identities unavailable")
        elif len(identities) != len(set(identities)) or len(observed) != len(set(observed)):
            problems.append("duplicate native march UUID in compared snapshots")
        elif set(identities) - set(observed):
            problems.append("independent active march UUID missing despite point readiness")
    return {
        "barrierPass": barrier,
        "sameTimeInversePass": barrier and not problems,
        "reasons": problems,
        "globalMobileAccuracyQualified": False,
    }
