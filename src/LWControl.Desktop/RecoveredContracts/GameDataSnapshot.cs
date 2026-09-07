#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record GameDataSnapshot(string View, string FeatureId, string Action, bool Available, string Error, IReadOnlyDictionary<string, string> Summary, IReadOnlyList<GameDataSnapshotRecord> Records);
