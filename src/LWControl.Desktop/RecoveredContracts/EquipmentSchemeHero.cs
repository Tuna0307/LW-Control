#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record EquipmentSchemeHero(int FormationSlot, string Uuid, int HeroId, string Name, string? PortraitKey, string? PortraitSource, string HeroType, int Level, long Power, IReadOnlyList<EquipmentSchemeEquipment> Equipment);
