"""Campaign 007 in-memory semantic inverse for forbidden Ghost/Treasure public providers.

A static mutation check, not a recovered protected-provider execution or game test.
"""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/"src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs"
text=SOURCE.read_text(encoding="utf-8")
def ghost_ok(value):
 start=value.find("public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(")
 if start<0:return False
 end=value.find("internal static IReadOnlyList<JsonElement> PrepareGhostPlunderRows",start)
 if end<0:return False
 body=value[start:end]
 return all(marker in body for marker in (
    "ValueTask.FromException<IReadOnlyList<JsonElement>>",
    "ProviderUnavailable(",
    "prepareGhostPlunderTasks row transformation/rejection semantics are incomplete",
    "downstream Ghost execution terminal-result correlation is separately unresolved"))
assert ghost_ok(text),"actual production Ghost preparation is not explicitly failed closed"
start=text.index("public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(")
def mutate_once(old,new,count=1):
 return text[:start]+text[start:].replace(old,new,1)
mutations=[
 ("remove provider gate",mutate_once("ProviderUnavailable(","FakeAvailable(",1)),
 ("remove returned exception",mutate_once("ValueTask.FromException<IReadOnlyList<JsonElement>>","ValueTask.FromResult<IReadOnlyList<JsonElement>>",1)),
 ("remove preparer boundary",mutate_once("prepareGhostPlunderTasks row transformation/rejection semantics are incomplete","source-proven prepare rows",1)),
 ("remove correlation warning",mutate_once("downstream Ghost execution terminal-result correlation is separately unresolved","correlation is assumed",1)),
]
for label, mutant in mutations:
 assert not ghost_ok(mutant),f"guard check did not reject semantic inverse: {label}"
for method in ("GetTreasureClaimStatusAsync","ClaimTreasuresAsync"):
 assert method in text
assert "current-client Treasure claim status provider is unavailable" in text
assert "current-client Treasure claim provider is unavailable" in text
print("CAMPAIGN007_GHOST_TREASURE_FENCES_ACTUAL_SOURCE_4_MUTATIONS_PASS")
