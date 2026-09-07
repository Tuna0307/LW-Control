#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
namespace LWControl.Desktop;

internal sealed record GameDataSnapshotItem(string Type, string Id, long Count, string Name, string Icon, int? Quality);
