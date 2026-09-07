#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record GameDataSnapshotRecord(string Identity, string Name, string State, string Level, string Owner, string StartTime, string EndTime, string Extra, IReadOnlyDictionary<string, string> Details)
{
	public IReadOnlyList<GameDataSnapshotItem> Items { get; init; } = Array.Empty<GameDataSnapshotItem>();

	public IReadOnlyList<long> Powers { get; init; } = Array.Empty<long>();
}
