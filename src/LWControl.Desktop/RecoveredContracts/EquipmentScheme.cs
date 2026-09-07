#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record EquipmentScheme(int Slot, string Name, IReadOnlyList<EquipmentSchemeAssignment> Assignments, DateTimeOffset CapturedAt, IReadOnlyList<EquipmentSchemeTeam>? Teams = null);
