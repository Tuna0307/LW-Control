#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record EquipmentSchemeTeam(int Index, string Uuid, bool IsFree, IReadOnlyList<EquipmentSchemeHero> Heroes);
