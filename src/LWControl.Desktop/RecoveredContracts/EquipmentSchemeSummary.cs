#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;

namespace LWControl.Desktop;

internal sealed record EquipmentSchemeSummary(int Slot, string Name, bool Saved, int AssignmentCount, DateTimeOffset CapturedAt, IReadOnlyList<EquipmentSchemeTeam> Teams, IReadOnlyList<int> ActiveTeamIndexes, DateTimeOffset? ActiveVerifiedAt);
