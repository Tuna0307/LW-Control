#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LWControl.Desktop;

internal sealed class EquipmentSchemeStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly string path;

	private readonly Dictionary<int, EquipmentScheme> schemes = new Dictionary<int, EquipmentScheme>();

	private readonly Dictionary<int, Dictionary<int, DateTimeOffset>> activeTeamsByScheme = new Dictionary<int, Dictionary<int, DateTimeOffset>>();

	public IReadOnlyList<EquipmentSchemeSummary> Summaries => Enumerable.Range(1, 4).Select(delegate(int slot)
	{
		int[] activeTeamIndexes = (activeTeamsByScheme.TryGetValue(slot, out Dictionary<int, DateTimeOffset> value) ? value.Keys.OrderBy((int index) => index).ToArray() : Array.Empty<int>());
		DateTimeOffset? activeVerifiedAt = ((value != null && value.Count > 0) ? new DateTimeOffset?(value.Values.Max()) : ((DateTimeOffset?)null));
		EquipmentScheme value2;
		return (!schemes.TryGetValue(slot, out value2)) ? new EquipmentSchemeSummary(slot, $"Scheme {slot}", Saved: false, 0, DateTimeOffset.MinValue, Array.Empty<EquipmentSchemeTeam>(), Array.Empty<int>(), null) : new EquipmentSchemeSummary(slot, value2.Name, Saved: true, value2.Assignments.Count, value2.CapturedAt, value2.Teams ?? Array.Empty<EquipmentSchemeTeam>(), activeTeamIndexes, activeVerifiedAt);
	}).ToArray();

	public EquipmentSchemeStore(string path)
	{
		this.path = path ?? throw new ArgumentNullException("path");
		Load();
	}

	public bool TryGet(int slot, out EquipmentScheme scheme)
	{
		return schemes.TryGetValue(slot, out scheme);
	}

	public string SerializeAssignments(int slot, int? teamIndex = null)
	{
		if (!TryGet(slot, out EquipmentScheme scheme))
		{
			throw new InvalidOperationException("equipment_scheme_not_saved");
		}
		IReadOnlyList<EquipmentSchemeAssignment> readOnlyList = scheme.Assignments;
		bool flag;
		if (teamIndex.HasValue)
		{
			if (!teamIndex.HasValue)
			{
				goto IL_0069;
			}
			switch (teamIndex.GetValueOrDefault())
			{
			case 1:
			case 2:
			case 3:
			case 4:
				goto IL_0069;
			}
			flag = true;
			goto IL_006c;
		}
		goto IL_0139;
		IL_0139:
		return JsonSerializer.Serialize(readOnlyList, JsonOptions);
		IL_006c:
		if (flag)
		{
			throw new InvalidOperationException("equipment_scheme_team_invalid");
		}
		EquipmentSchemeTeam equipmentSchemeTeam = scheme.Teams?.SingleOrDefault((EquipmentSchemeTeam item) => item.Index == teamIndex.Value);
		if ((object)equipmentSchemeTeam == null)
		{
			throw new InvalidOperationException("equipment_scheme_team_unavailable");
		}
		HashSet<string> heroUuids = (from hero in equipmentSchemeTeam.Heroes
			select hero.Uuid into uuid
			where !string.IsNullOrWhiteSpace(uuid)
			select uuid).ToHashSet<string>(StringComparer.Ordinal);
		readOnlyList = scheme.Assignments.Where((EquipmentSchemeAssignment assignment) => heroUuids.Contains(assignment.HeroUuid)).ToArray();
		if (readOnlyList.Count == 0)
		{
			throw new InvalidOperationException("equipment_scheme_team_has_no_equipment");
		}
		goto IL_0139;
		IL_0069:
		flag = false;
		goto IL_006c;
	}

	public bool TryMarkVerifiedApplied(int slot, int? teamIndex, string? verifiedAssignmentsJson, DateTimeOffset verifiedAt)
	{
		if (!TryGet(slot, out EquipmentScheme scheme) || string.IsNullOrWhiteSpace(verifiedAssignmentsJson))
		{
			return false;
		}
		string a;
		try
		{
			a = SerializeAssignments(slot, teamIndex);
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		if (!string.Equals(a, verifiedAssignmentsJson, StringComparison.Ordinal))
		{
			return false;
		}
		IReadOnlyList<int> readOnlyList = ((!teamIndex.HasValue) ? ((IReadOnlyList<int>)(from index in (from team in scheme.Teams ?? Array.Empty<EquipmentSchemeTeam>()
				where TeamAssignments(scheme, team).Count > 0
				select team.Index).Distinct()
			orderby index
			select index).ToArray()) : ((IReadOnlyList<int>)new int[1] { teamIndex.Value }));
		if (readOnlyList.Any((int index) => (index < 1 || index > 4) ? true : false))
		{
			return false;
		}
		activeTeamsByScheme.Clear();
		if (readOnlyList.Count == 0)
		{
			return false;
		}
		activeTeamsByScheme[slot] = readOnlyList.ToDictionary((int index) => index, (int _) => verifiedAt);
		return true;
	}

	public void ReconcileActiveTeams(EquipmentStatePreview preview)
	{
		if (!preview.Available)
		{
			return;
		}
		activeTeamsByScheme.Clear();
		Dictionary<int, EquipmentSchemeTeam> dictionary = (from team in preview.Teams
			where team.Index > 0
			group team by team.Index).ToDictionary((IGrouping<int, EquipmentSchemeTeam> group) => group.Key, (IGrouping<int, EquipmentSchemeTeam> group) => group.First());
		Dictionary<string, string> actualHolders = (from item in preview.Teams.SelectMany((EquipmentSchemeTeam team) => team.Heroes ?? Array.Empty<EquipmentSchemeHero>()).SelectMany((EquipmentSchemeHero hero) => (hero.Equipment ?? Array.Empty<EquipmentSchemeEquipment>()).Select((EquipmentSchemeEquipment equipment) => new
			{
				Uuid = equipment.Uuid,
				HeroUuid = hero.Uuid
			}))
			where !string.IsNullOrWhiteSpace(item.Uuid)
			select item).GroupBy(item => item.Uuid, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().HeroUuid, StringComparer.Ordinal);
		foreach (EquipmentScheme value4 in schemes.Values)
		{
			foreach (EquipmentSchemeTeam item in value4.Teams ?? Array.Empty<EquipmentSchemeTeam>())
			{
				IReadOnlyList<EquipmentSchemeAssignment> readOnlyList = TeamAssignments(value4, item);
				if (readOnlyList.Count == 0 || !dictionary.TryGetValue(item.Index, out var value))
				{
					continue;
				}
				HashSet<string> actualHeroUuids = value.Heroes.Select((EquipmentSchemeHero hero) => hero.Uuid).ToHashSet<string>(StringComparer.Ordinal);
				if (readOnlyList.All((EquipmentSchemeAssignment assignment) => actualHeroUuids.Contains(assignment.HeroUuid) && actualHolders.TryGetValue(assignment.EquipmentUuid, out var value3) && string.Equals(value3, assignment.HeroUuid, StringComparison.Ordinal)))
				{
					if (!activeTeamsByScheme.TryGetValue(value4.Slot, out Dictionary<int, DateTimeOffset> value2))
					{
						value2 = new Dictionary<int, DateTimeOffset>();
						activeTeamsByScheme[value4.Slot] = value2;
					}
					value2[item.Index] = preview.CapturedAt;
				}
			}
		}
	}

	public void ClearActiveVerifications()
	{
		activeTeamsByScheme.Clear();
	}

	public EquipmentScheme Capture(int slot, string? requestedName, JsonElement output)
	{
		if ((slot < 1 || slot > 4) ? true : false)
		{
			throw new InvalidOperationException("equipment_scheme_slot_invalid");
		}
		if (output.ValueKind != JsonValueKind.Object || !output.TryGetProperty("state", out var value) || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("available", out var value2) || value2.ValueKind != JsonValueKind.True || !value.TryGetProperty("teams", out var value3) || value3.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidOperationException("equipment_scheme_state_unavailable");
		}
		Dictionary<string, EquipmentSchemeAssignment> dictionary = new Dictionary<string, EquipmentSchemeAssignment>(StringComparer.Ordinal);
		foreach (JsonElement item in value3.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("heroes", out var value4) || value4.ValueKind != JsonValueKind.Array)
			{
				continue;
			}
			foreach (JsonElement item2 in value4.EnumerateArray())
			{
				string text = ScalarString(item2, "uuid");
				if (string.IsNullOrWhiteSpace(text) || !item2.TryGetProperty("equips", out var value5) || value5.ValueKind != JsonValueKind.Array)
				{
					continue;
				}
				foreach (JsonElement item3 in value5.EnumerateArray())
				{
					string text2 = ScalarString(item3, "uuid");
					int num = ScalarInt32(item3, "slot");
					if (!string.IsNullOrWhiteSpace(text2) && num > 0)
					{
						dictionary[text2] = new EquipmentSchemeAssignment(text2, text, num);
					}
				}
			}
		}
		if (dictionary.Count == 0)
		{
			throw new InvalidOperationException("equipment_scheme_has_no_equipment");
		}
		string name = NormalizeName(slot, requestedName);
		IReadOnlyList<EquipmentSchemeTeam> teams = ParseTeams(value3);
		EquipmentScheme equipmentScheme = new EquipmentScheme(slot, name, dictionary.Values.OrderBy<EquipmentSchemeAssignment, string>((EquipmentSchemeAssignment item) => item.HeroUuid, StringComparer.Ordinal).ThenBy((EquipmentSchemeAssignment item) => item.Slot).ThenBy<EquipmentSchemeAssignment, string>((EquipmentSchemeAssignment item) => item.EquipmentUuid, StringComparer.Ordinal)
			.ToArray(), DateTimeOffset.UtcNow, teams);
		schemes[slot] = equipmentScheme;
		activeTeamsByScheme.Remove(slot);
		Save();
		return equipmentScheme;
	}

	public EquipmentScheme SaveDraft(int slot, string? requestedName, IReadOnlyList<EquipmentSchemeTeam>? teams)
	{
		if ((slot < 1 || slot > 4) ? true : false)
		{
			throw new InvalidOperationException("equipment_scheme_slot_invalid");
		}
		if (teams == null || teams.Count == 0)
		{
			throw new InvalidOperationException("equipment_scheme_has_no_teams");
		}
		HashSet<int> hashSet = new HashSet<int>();
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet3 = new HashSet<string>(StringComparer.Ordinal);
		List<EquipmentSchemeAssignment> list = new List<EquipmentSchemeAssignment>();
		foreach (EquipmentSchemeTeam team in teams)
		{
			bool flag = (object)team == null;
			if (!flag)
			{
				int index = team.Index;
				bool flag2 = ((index < 1 || index > 4) ? true : false);
				flag = flag2;
			}
			if (flag || !hashSet.Add(team.Index))
			{
				throw new InvalidOperationException("equipment_scheme_team_invalid");
			}
			HashSet<int> hashSet4 = new HashSet<int>();
			foreach (EquipmentSchemeHero item in team.Heroes ?? Array.Empty<EquipmentSchemeHero>())
			{
				flag = (object)item == null;
				if (!flag)
				{
					int index = item.FormationSlot;
					bool flag2 = ((index < 1 || index > 5) ? true : false);
					flag = flag2;
				}
				if (flag || !hashSet4.Add(item.FormationSlot) || string.IsNullOrWhiteSpace(item.Uuid) || !hashSet2.Add(item.Uuid))
				{
					throw new InvalidOperationException("equipment_scheme_hero_invalid");
				}
				HashSet<int> hashSet5 = new HashSet<int>();
				foreach (EquipmentSchemeEquipment item2 in item.Equipment ?? Array.Empty<EquipmentSchemeEquipment>())
				{
					flag = (object)item2 == null;
					if (!flag)
					{
						int index = item2.Slot;
						bool flag2 = ((index < 1 || index > 4) ? true : false);
						flag = flag2;
					}
					if (flag || !hashSet5.Add(item2.Slot))
					{
						throw new InvalidOperationException("equipment_scheme_duplicate_hero_slot");
					}
					if (string.IsNullOrWhiteSpace(item2.Uuid) || !hashSet3.Add(item2.Uuid))
					{
						throw new InvalidOperationException("equipment_scheme_duplicate_equipment");
					}
					list.Add(new EquipmentSchemeAssignment(item2.Uuid, item.Uuid, item2.Slot));
				}
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException("equipment_scheme_has_no_equipment");
		}
		EquipmentScheme equipmentScheme = new EquipmentScheme(slot, NormalizeName(slot, requestedName), list.OrderBy<EquipmentSchemeAssignment, string>((EquipmentSchemeAssignment item) => item.HeroUuid, StringComparer.Ordinal).ThenBy((EquipmentSchemeAssignment item) => item.Slot).ThenBy<EquipmentSchemeAssignment, string>((EquipmentSchemeAssignment item) => item.EquipmentUuid, StringComparer.Ordinal)
			.ToArray(), DateTimeOffset.UtcNow, NormalizeTeams(teams));
		schemes[slot] = equipmentScheme;
		activeTeamsByScheme.Remove(slot);
		Save();
		return equipmentScheme;
	}

	public EquipmentStatePreview Preview(JsonElement output)
	{
		if (output.ValueKind != JsonValueKind.Object || !output.TryGetProperty("state", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return new EquipmentStatePreview(Available: false, "equipment_scheme_state_unavailable", 0, 0, 0, 0, DateTimeOffset.UtcNow, Array.Empty<EquipmentSchemeTeam>());
		}
		bool available = ScalarBoolean(value, "available");
		string error = ScalarString(value, "error");
		IReadOnlyList<EquipmentSchemeTeam> readOnlyList2;
		if (!value.TryGetProperty("teams", out var value2) || value2.ValueKind != JsonValueKind.Array)
		{
			IReadOnlyList<EquipmentSchemeTeam> readOnlyList = Array.Empty<EquipmentSchemeTeam>();
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = ParseTeams(value2);
		}
		IReadOnlyList<EquipmentSchemeTeam> teams = readOnlyList2;
		return new EquipmentStatePreview(available, error, ScalarInt32(value, "team_count"), ScalarInt32(value, "equipment_count"), ScalarInt32(value, "worn_count"), ScalarInt32(value, "free_count"), DateTimeOffset.UtcNow, teams);
	}

	private void Load()
	{
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			foreach (EquipmentScheme item in JsonSerializer.Deserialize<List<EquipmentScheme>>(File.ReadAllText(path), JsonOptions) ?? new List<EquipmentScheme>())
			{
				int slot = item.Slot;
				bool flag = ((slot < 1 || slot > 4) ? true : false);
				if (!flag && item.Assignments != null && item.Assignments.Count != 0)
				{
					EquipmentSchemeAssignment[] array = (from @group in item.Assignments.Where((EquipmentSchemeAssignment item) => !string.IsNullOrWhiteSpace(item.EquipmentUuid) && !string.IsNullOrWhiteSpace(item.HeroUuid) && item.Slot > 0).GroupBy<EquipmentSchemeAssignment, string>((EquipmentSchemeAssignment item) => item.EquipmentUuid, StringComparer.Ordinal)
						select @group.First()).ToArray();
					if (array.Length != 0)
					{
						schemes[item.Slot] = item with
						{
							Assignments = array,
							Teams = NormalizeTeams(item.Teams)
						};
					}
				}
			}
		}
		catch (JsonException)
		{
			schemes.Clear();
		}
		catch (IOException)
		{
			schemes.Clear();
		}
	}

	private void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("equipment_scheme_path_invalid"));
		string sourceFileName = path + ".tmp";
		File.WriteAllText(sourceFileName, JsonSerializer.Serialize(schemes.Values.OrderBy((EquipmentScheme item) => item.Slot).ToArray(), JsonOptions));
		File.Move(sourceFileName, path, overwrite: true);
	}

	private static string NormalizeName(int slot, string? requestedName)
	{
		string text = (string.IsNullOrWhiteSpace(requestedName) ? $"Scheme {slot}" : requestedName.Trim());
		if (text.Length <= 40)
		{
			return text;
		}
		return text.Substring(0, 40);
	}

	private static IReadOnlyList<EquipmentSchemeTeam> ParseTeams(JsonElement teams)
	{
		List<EquipmentSchemeTeam> list = new List<EquipmentSchemeTeam>();
		foreach (JsonElement item in teams.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			List<EquipmentSchemeHero> list2 = new List<EquipmentSchemeHero>();
			if (item.TryGetProperty("heroes", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item2 in value.EnumerateArray())
				{
					if (item2.ValueKind != JsonValueKind.Object)
					{
						continue;
					}
					List<EquipmentSchemeEquipment> list3 = new List<EquipmentSchemeEquipment>();
					if (item2.TryGetProperty("equips", out var value2) && value2.ValueKind == JsonValueKind.Array)
					{
						foreach (JsonElement item3 in value2.EnumerateArray())
						{
							string text = ScalarString(item3, "uuid");
							int num = ScalarInt32(item3, "slot");
							if (!string.IsNullOrWhiteSpace(text) && num > 0)
							{
								list3.Add(new EquipmentSchemeEquipment(text, ScalarInt32(item3, "template_id"), ScalarString(item3, "icon_key"), ScalarString(item3, "icon_source"), num, ScalarInt32(item3, "level"), ScalarInt32(item3, "promote_level"), ScalarInt64(item3, "power")));
							}
						}
					}
					list2.Add(new EquipmentSchemeHero(ScalarInt32(item2, "formation_slot"), ScalarString(item2, "uuid"), ScalarInt32(item2, "hero_id"), ScalarString(item2, "name"), ScalarString(item2, "portrait_key"), ScalarString(item2, "portrait_source"), ScalarString(item2, "hero_type"), ScalarInt32(item2, "level"), ScalarInt64(item2, "power"), list3.OrderBy((EquipmentSchemeEquipment item) => item.Slot).ThenBy<EquipmentSchemeEquipment, string>((EquipmentSchemeEquipment item) => item.Uuid, StringComparer.Ordinal).ToArray()));
				}
			}
			list.Add(new EquipmentSchemeTeam(ScalarInt32(item, "index"), ScalarString(item, "uuid"), ScalarBoolean(item, "is_free"), list2.OrderBy((EquipmentSchemeHero hero) => hero.FormationSlot).ThenBy<EquipmentSchemeHero, string>((EquipmentSchemeHero hero) => hero.Uuid, StringComparer.Ordinal).ToArray()));
		}
		return NormalizeTeams(list);
	}

	private static IReadOnlyList<EquipmentSchemeTeam> NormalizeTeams(IReadOnlyList<EquipmentSchemeTeam>? teams)
	{
		return (from team in teams ?? Array.Empty<EquipmentSchemeTeam>()
			where team.Index > 0
			orderby team.Index
			select team with
			{
				Heroes = (from hero in team.Heroes ?? Array.Empty<EquipmentSchemeHero>()
					where hero.FormationSlot > 0
					orderby hero.FormationSlot
					select hero with
					{
						Name = (string.IsNullOrWhiteSpace(hero.Name) ? string.Empty : hero.Name.Trim()),
						Equipment = (from item in hero.Equipment ?? Array.Empty<EquipmentSchemeEquipment>()
							where !string.IsNullOrWhiteSpace(item.Uuid) && item.Slot > 0
							orderby item.Slot
							select item).ToArray()
					}).ToArray()
			}).ToArray();
	}

	private static IReadOnlyList<EquipmentSchemeAssignment> TeamAssignments(EquipmentScheme scheme, EquipmentSchemeTeam team)
	{
		HashSet<string> heroUuids = (from hero in team.Heroes ?? Array.Empty<EquipmentSchemeHero>()
			select hero.Uuid into uuid
			where !string.IsNullOrWhiteSpace(uuid)
			select uuid).ToHashSet<string>(StringComparer.Ordinal);
		return scheme.Assignments.Where((EquipmentSchemeAssignment assignment) => heroUuids.Contains(assignment.HeroUuid)).ToArray();
	}

	private static string ScalarString(JsonElement element, string property)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
		{
			return string.Empty;
		}
		if (value.ValueKind != JsonValueKind.String)
		{
			return value.ToString();
		}
		return value.GetString() ?? string.Empty;
	}

	private static int ScalarInt32(JsonElement element, string property)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
		{
			return 0;
		}
		if (!value.TryGetInt32(out var value2))
		{
			if (!int.TryParse(value.ToString(), out value2))
			{
				return 0;
			}
			return value2;
		}
		return value2;
	}

	private static long ScalarInt64(JsonElement element, string property)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
		{
			return 0L;
		}
		if (!value.TryGetInt64(out var value2))
		{
			if (!long.TryParse(value.ToString(), out value2))
			{
				return 0L;
			}
			return value2;
		}
		return value2;
	}

	private static bool ScalarBoolean(JsonElement element, string property)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
		{
			return false;
		}
		bool result = default(bool);
		if (value.ValueKind != JsonValueKind.True)
		{
			return (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out result)) & result;
		}
		return true;
	}
}
