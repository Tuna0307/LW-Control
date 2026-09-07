#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
namespace LWControl.Desktop;

internal sealed record EquipmentSchemeEquipment(string Uuid, int TemplateId, string? IconKey, string? IconSource, int Slot, int Level, int PromoteLevel, long Power);
