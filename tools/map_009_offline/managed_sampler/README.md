# MAP-013 diagnostic packet reader

This standalone netstandard2.0 assembly is an opt-in, task-owned observer. It is
not part of the shipped application and never sends a packet, replaces a game
handler, reads raw network buffers, dumps objects or modifies game state.

The admitted current game uses `GameKit.Base.NetRawProxy` and
`ProtoBufNet.NetProfiler`. Reflection enumerates its actual managed concurrent
packet dictionaries. The reader returns only ASCII map-like command names,
protocol key names and numeric region context from `world.get.block` segments.
It reports enumeration, row and segment limits. Incoming packet object hashes
are ephemeral observation identities, not network request IDs. Outgoing `info`
was null in the live observations; no outgoing attribution is claimed.

Build and execute the real reader against inert API-shaped objects:

```powershell
dotnet run --project tools/map_009_offline/managed_sampler_checks/Checks.csproj -c Release
```

The owned runner copies the built DLL to its fresh isolated root, sets a
process-only path and records its hash. Default packaging remains unchanged.
Missing DLLs do not produce packet success. The Lua observer uses bounded reads
and records failures; acceptance still requires actual native observation.

Do not run hash-gated original-bytecode tests while a diagnostic candidate is
installed: they correctly reject its different hash. Finish exact owned Stop,
verify restoration, then run those tests against the original installed package.
