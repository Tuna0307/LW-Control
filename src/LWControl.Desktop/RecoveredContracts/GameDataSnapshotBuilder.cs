#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LWControl.Desktop;

internal static class GameDataSnapshotBuilder
{
	private static readonly IReadOnlyDictionary<string, string> ExpectedFeatures = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["dispatch"] = "secret_mobile_squad",
		["alliance_dispatch"] = "alliance_ghost_scout",
		["ghost"] = "ghost_scout",
		["truck"] = "resource_grab",
		["train"] = "resource_grab",
		["alliance_help"] = "alliance_help",
		["alliance_tech"] = "alliance_tech_donate",
		["battle_squads"] = "auto_join_rally",
		["alliance_train_state"] = "alliance_train"
	};

	private static readonly IReadOnlyDictionary<string, string> ExpectedActions = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["dispatch"] = "secret_mobile_squad_state",
		["alliance_dispatch"] = "alliance_ghost_scout_state",
		["ghost"] = "ghost_scout_state",
		["truck"] = "resource_grab_truck_data_state",
		["train"] = "resource_grab_railway_data_state",
		["alliance_help"] = "alliance_help_state",
		["alliance_tech"] = "alliance_tech_donate_state",
		["battle_squads"] = "auto_join_rally_state",
		["alliance_train_state"] = "alliance_train_state"
	};

	public static bool IsSupportedRequest(string view, string featureId)
	{
		if (ExpectedFeatures.TryGetValue(view, out string value))
		{
			return string.Equals(featureId, value, StringComparison.Ordinal);
		}
		return false;
	}

	public static GameDataSnapshot Build(string view, string featureId, JsonElement output)
	{
		if (!IsSupportedRequest(view, featureId))
		{
			return Unavailable(view, featureId, "game_data_feature_mismatch");
		}
		if (output.ValueKind != JsonValueKind.Object)
		{
			return Unavailable(view, featureId, "game_data_output_missing");
		}
		string text = Scalar(output, "action");
		if (!ExpectedActions.TryGetValue(view, out string value) || !string.Equals(text, value, StringComparison.Ordinal))
		{
			return Unavailable(view, featureId, "game_data_action_mismatch", text);
		}
		bool num = Boolean(output, "ok");
		string text2 = Scalar(output, "error");
		if (!num)
		{
			return Unavailable(view, featureId, string.IsNullOrWhiteSpace(text2) ? "game_data_read_failed" : text2, text);
		}
		switch (view)
		{
		case "dispatch":
			return BuildDispatch(view, featureId, text, output);
		case "alliance_dispatch":
			return BuildTaskCollection(view, featureId, text, output, "state", "alliance_tasks", new string[4] { "alliance_task_count", "joinable_count", "joined_count", "team_max_member_num" });
		case "ghost":
			return BuildTaskCollection(view, featureId, text, output, "state", "tasks", new string[5] { "task_count", "ready_count", "running_count", "rewardable_count", "claimed_count" });
		case "train":
		case "truck":
			return BuildGlobalRailwayData(view, featureId, text, output);
		case "alliance_help":
			return BuildTaskCollection(view, featureId, text, output, "state", "items", new string[6] { "help_num", "max_help_count", "today_help_point", "other_help_count", "pending_help_count", "enabled" });
		case "alliance_tech":
			return BuildTaskCollection(view, featureId, text, output, "state", "items", new string[7] { "donation_count", "max_donation_count", "tech_count", "target_id", "resource_type", "resource_count", "enabled" });
		case "battle_squads":
			return BuildBattleSquads(view, featureId, text, output);
		case "alliance_train_state":
			return BuildAllianceTrainState(view, featureId, text, output);
		default:
			return Unavailable(view, featureId, "game_data_view_unsupported", text);
		}
	}

	private static GameDataSnapshot BuildAllianceTrainState(string view, string featureId, string action, JsonElement output)
	{
		JsonElement jsonElement = Object(output, "state");
		if (jsonElement.ValueKind != JsonValueKind.Object)
		{
			return Unavailable(view, featureId, "alliance_train_state_missing", action);
		}
		if (jsonElement.TryGetProperty("available", out var value) && value.ValueKind == JsonValueKind.False)
		{
			return Unavailable(view, featureId, Scalar(jsonElement, "error", "alliance_train_state_unavailable"), action);
		}
		IReadOnlyDictionary<string, string> summary = ReadSummary(jsonElement, new string[15]
		{
			"captured_at", "train_uuid", "current_position", "current_carriage_id", "target_carriage_id", "passenger_count", "capacity", "queue_count", "target_reward_type", "target_reward_id",
			"target_reward_count", "target_reward_name", "target_reward_icon", "last_checked_at", "next_at"
		});
		List<GameDataSnapshotRecord> list = new List<GameDataSnapshotRecord>();
		JsonElement jsonElement2 = Array(jsonElement, "carriages");
		if (jsonElement2.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in jsonElement2.EnumerateArray().Take(4))
			{
				if (item.ValueKind == JsonValueKind.Object)
				{
					string text = Scalar(item, "carriage_id");
					string value2 = Scalar(item, "passenger_count");
					string value3 = Scalar(item, "capacity");
					string value4 = Scalar(item, "queue_count");
					list.Add(new GameDataSnapshotRecord(string.IsNullOrWhiteSpace(text) ? (list.Count + 1).ToString() : text, string.IsNullOrWhiteSpace(text) ? "Carriage" : ("Carriage " + text), Scalar(item, "position"), Scalar(item, "quality"), Scalar(jsonElement, "train_uuid"), string.Empty, string.Empty, $"Passengers {ValueOrDash(value2)}/{ValueOrDash(value3)} · Queue {ValueOrDash(value4)}", ScalarDetails(item, "items"))
					{
						Items = ReadItems(item)
					});
				}
			}
		}
		return new GameDataSnapshot(view, featureId, action, Available: true, string.Empty, summary, list);
	}

	private static string ValueOrDash(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return "--";
	}

	private static GameDataSnapshot BuildDispatch(string view, string featureId, string action, JsonElement output)
	{
		JsonElement state = Object(output, "dispatch_state");
		if (state.ValueKind != JsonValueKind.Object)
		{
			state = Object(output, "data_center");
		}
		return BuildFromState(view, featureId, action, state, "tasks", new string[6] { "task_count", "running_count", "normal_count", "rewardable_count", "max_march", "current_star_level" });
	}

	private static GameDataSnapshot BuildTaskCollection(string view, string featureId, string action, JsonElement output, string stateProperty, string recordsProperty, IReadOnlyList<string> summaryFields)
	{
		return BuildFromState(view, featureId, action, Object(output, stateProperty), recordsProperty, summaryFields);
	}

	private static GameDataSnapshot BuildFromState(string view, string featureId, string action, JsonElement state, string recordsProperty, IReadOnlyList<string> summaryFields)
	{
		if (state.ValueKind != JsonValueKind.Object)
		{
			return Unavailable(view, featureId, "game_data_state_missing", action);
		}
		if (state.TryGetProperty("available", out var value) && value.ValueKind == JsonValueKind.False)
		{
			return Unavailable(view, featureId, Scalar(state, "error", "game_data_state_unavailable"), action);
		}
		List<GameDataSnapshotRecord> list = new List<GameDataSnapshotRecord>();
		JsonElement jsonElement = Array(state, recordsProperty);
		if (jsonElement.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in jsonElement.EnumerateArray().Take(200))
			{
				if (item.ValueKind == JsonValueKind.Object)
				{
					list.Add(CreateRecord(view, item, list.Count + 1));
				}
			}
		}
		return new GameDataSnapshot(view, featureId, action, Available: true, string.Empty, ReadSummary(state, summaryFields), list);
	}

	private static GameDataSnapshot BuildGlobalRailwayData(string view, string featureId, string action, JsonElement output)
	{
		JsonElement jsonElement = Object(output, "state");
		if (jsonElement.ValueKind != JsonValueKind.Object)
		{
			return Unavailable(view, featureId, "game_data_state_missing", action);
		}
		if (jsonElement.TryGetProperty("available", out var value) && value.ValueKind == JsonValueKind.False)
		{
			return Unavailable(view, featureId, Scalar(jsonElement, "error", "game_data_state_unavailable"), action);
		}
		IReadOnlyDictionary<string, string> summary = ReadSummary(jsonElement, new string[10] { "server_id", "total", "page", "page_size", "total_pages", "rejected_count", "cache_revision", "captured_at", "refreshed_at", "refresh_verified" });
		List<GameDataSnapshotRecord> list = new List<GameDataSnapshotRecord>();
		JsonElement jsonElement2 = Array(jsonElement, "records");
		if (jsonElement2.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in jsonElement2.EnumerateArray().Take(200))
			{
				if (item.ValueKind == JsonValueKind.Object)
				{
					list.Add((view == "truck") ? CreateTruckDataRecord(item, list.Count + 1) : CreateRailwayDataRecord(item, list.Count + 1));
				}
			}
		}
		return new GameDataSnapshot(view, featureId, action, Available: true, string.Empty, summary, list);
	}

	private static GameDataSnapshotRecord CreateTruckDataRecord(JsonElement item, int index)
	{
		string text = First(item, "uuid", "march_uuid");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = index.ToString();
		}
		string text2 = First(item, "owner_name", "owner_id");
		IReadOnlyList<GameDataSnapshotItem> readOnlyList = ReadItems(item);
		return new GameDataSnapshotRecord(text, string.IsNullOrWhiteSpace(text2) ? ("Truck " + text) : text2, Scalar(item, "items_known"), Scalar(item, "quality"), First(item, "owner_name", "owner_id"), Scalar(item, "departure_ts"), Scalar(item, "arrive_ts"), $"Items {readOnlyList.Count}", ScalarDetails(item, "items", "item_errors"))
		{
			Items = readOnlyList
		};
	}

	private static GameDataSnapshotRecord CreateRailwayDataRecord(JsonElement item, int index)
	{
		string text = First(item, "uuid", "march_uuid");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = index.ToString();
		}
		string text2 = First(item, "alliance_abbr", "alliance_name", "alliance_id");
		IReadOnlyList<long> powers = ReadPowers(item);
		bool flag = Boolean(item, "is_ur");
		bool flag2 = Boolean(item, "is_ur_known");
		string text3 = Scalar(item, "rob_times");
		return new GameDataSnapshotRecord(text, string.IsNullOrWhiteSpace(text2) ? ("Railway " + text) : text2, (!flag2) ? string.Empty : (flag ? "UR" : "Normal"), First(item, "gift_level", "quality"), text2, string.Empty, First(item, "protect_time_seconds", "protect_time"), string.IsNullOrWhiteSpace(text3) ? string.Empty : ("Rob " + text3), ScalarDetails(item, "power", "power_slots"))
		{
			Powers = powers
		};
	}

	private static GameDataSnapshot BuildBattleSquads(string view, string featureId, string action, JsonElement output)
	{
		JsonElement jsonElement = Array(output, "squads");
		IReadOnlyDictionary<string, string> summary;
		if (jsonElement.ValueKind == JsonValueKind.Array)
		{
			JsonElement[] array = (from item in jsonElement.EnumerateArray()
				where item.ValueKind == JsonValueKind.Object
				select item).Take(20).ToArray();
			jsonElement = JsonSerializer.SerializeToElement(array);
			summary = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["count"] = array.Length.ToString(),
				["free_count"] = array.Count((JsonElement item) => Boolean(item, "is_idle")).ToString()
			};
		}
		else
		{
			JsonElement element = Object(output, "state");
			JsonElement jsonElement2 = Object(element, "formations");
			if (element.ValueKind != JsonValueKind.Object || jsonElement2.ValueKind != JsonValueKind.Object)
			{
				return Unavailable(view, featureId, "battle_squad_state_missing", action);
			}
			if (jsonElement2.TryGetProperty("available", out var value) && value.ValueKind == JsonValueKind.False)
			{
				return Unavailable(view, featureId, Scalar(jsonElement2, "error", "battle_squad_state_unavailable"), action);
			}
			jsonElement = Array(jsonElement2, "formations");
			summary = ReadSummary(jsonElement2, new string[2] { "count", "free_count" });
		}
		List<GameDataSnapshotRecord> list = new List<GameDataSnapshotRecord>();
		if (jsonElement.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in jsonElement.EnumerateArray().Take(20))
			{
				if (item.ValueKind == JsonValueKind.Object)
				{
					string text = First(item, "index", "squad_id");
					string text2 = First(item, "uuid", "squad_uuid", "index", "squad_id");
					if (string.IsNullOrWhiteSpace(text2))
					{
						text2 = (list.Count + 1).ToString();
					}
					string text3 = Scalar(item, "current_rally_id");
					bool flag = Boolean(item, "is_free");
					string text4 = Scalar(item, "status");
					string state = ((!string.IsNullOrWhiteSpace(text4)) ? text4 : (flag ? "Idle" : ((!string.IsNullOrWhiteSpace(text3)) ? "Rallying" : "Busy")));
					string text5 = Scalar(item, "power");
					string text6 = Scalar(item, "stamina");
					string extra = string.Join(" · ", new string[2]
					{
						string.IsNullOrWhiteSpace(text5) ? string.Empty : ("Power " + text5),
						string.IsNullOrWhiteSpace(text6) ? string.Empty : ("Stamina " + text6)
					}.Where((string text7) => text7.Length > 0));
					list.Add(new GameDataSnapshotRecord(text2, string.IsNullOrWhiteSpace(text) ? $"Squad {list.Count + 1}" : ("Squad " + text), state, string.Empty, text3, string.Empty, string.Empty, extra, ScalarDetails(item)));
				}
			}
		}
		return new GameDataSnapshot(view, featureId, action, Available: true, string.Empty, summary, list.OrderBy((GameDataSnapshotRecord record) => ParseSquadIndex(record)).ToArray());
	}

	private static int ParseSquadIndex(GameDataSnapshotRecord record)
	{
		if ((record.Details.TryGetValue("index", out string value) || record.Details.TryGetValue("squad_id", out value)) && int.TryParse(value, out var result))
		{
			return result;
		}
		return int.MaxValue;
	}

	private static GameDataSnapshotRecord CreateRecord(string view, JsonElement item, int index)
	{
		string text = First(item, "uuid", "build_uuid", "task_uuid", "help_id", "science_id", "id", "cfg_id", "index");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = index.ToString();
		}
		string text2 = First(item, "name", "title", "task_name", "description", "content");
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = view switch
			{
				"truck" => "Truck " + First(item, "index", "cfg_id", "uuid"), 
				"train" => "Alliance Train", 
				"alliance_dispatch" => "Alliance Task " + text, 
				"ghost" => "Ghost Task " + text, 
				"alliance_help" => "Alliance Help " + text, 
				"alliance_tech" => "Alliance Tech " + text, 
				_ => "Dispatch Task " + text, 
			};
		}
		string state = First(item, "station_state", "bubble_type", "state", "status", "task_state", "finished");
		string level = First(item, "level", "cur_level", "rarity", "quality", "star", "current_star_level");
		string owner = First(item, "owner_name", "owner_id", "player_name", "player_id", "sender_id", "alliance_id");
		string startTime = First(item, "start_time", "team_start_time", "departure_ts", "dispatch_begin_time");
		string endTime = First(item, "end_time", "completion_time", "arrive_ts", "task_expire_time", "finish_time", "update_time");
		string text3;
		switch (view)
		{
		case "truck":
			text3 = JoinDetail(item, ("Heroes", "hero_count"), ("Squad", "squad_no_client"));
			break;
		case "train":
			text3 = JoinDetail(item, ("Passengers", "passenger_count"), ("Carriages", "carriages"));
			break;
		case "ghost":
		case "alliance_dispatch":
			text3 = JoinDetail(item, ("Members", "member_count"), ("Heroes", "hero_count"));
			break;
		case "alliance_help":
			text3 = JoinProgress(item, "now_count", "max_count", "Help");
			break;
		case "alliance_tech":
			text3 = JoinProgress(item, "current_pro", "need_pro", "Progress");
			break;
		default:
			text3 = JoinDetail(item, ("Heroes", "need_hero_num"), ("Reward", "rewardable"));
			break;
		}
		string extra = text3;
		return new GameDataSnapshotRecord(text, text2, state, level, owner, startTime, endTime, extra, ScalarDetails(item))
		{
			Items = ReadItems(item)
		};
	}

	private static string JoinProgress(JsonElement item, string currentProperty, string maximumProperty, string label)
	{
		string value = Scalar(item, currentProperty);
		string value2 = Scalar(item, maximumProperty);
		if (!string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(value2))
		{
			return $"{label} {value}/{value2}";
		}
		return string.Empty;
	}

	private static string JoinDetail(JsonElement item, params (string Label, string Property)[] fields)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < fields.Length; i++)
		{
			(string, string) tuple = fields[i];
			string text = Scalar(item, tuple.Item2);
			if (string.IsNullOrWhiteSpace(text) && item.TryGetProperty(tuple.Item2, out var value) && value.ValueKind == JsonValueKind.Array)
			{
				text = value.GetArrayLength().ToString();
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(tuple.Item1 + " " + text);
			}
		}
		return string.Join(" · ", list);
	}

	private static IReadOnlyDictionary<string, string> ReadSummary(JsonElement state, IReadOnlyList<string> fields)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string field in fields)
		{
			string value = Scalar(state, field);
			if (!string.IsNullOrWhiteSpace(value))
			{
				dictionary[field] = value;
			}
		}
		return dictionary;
	}

	private static IReadOnlyList<GameDataSnapshotItem> ReadItems(JsonElement item)
	{
		JsonElement jsonElement = Array(item, "items");
		if (jsonElement.ValueKind != JsonValueKind.Array)
		{
			return System.Array.Empty<GameDataSnapshotItem>();
		}
		List<GameDataSnapshotItem> list = new List<GameDataSnapshotItem>();
		foreach (JsonElement item2 in jsonElement.EnumerateArray().Take(64))
		{
			if (item2.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			string text = First(item2, "id", "item_id", "goods_id", "resource_id");
			long? num = Integer(item2, "count");
			bool flag = string.IsNullOrWhiteSpace(text);
			if (!flag)
			{
				bool flag2 = ((!num.HasValue || num.GetValueOrDefault() <= 0) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				continue;
			}
			long? num2 = Integer(item2, "quality");
			string type = First(item2, "type", "lane");
			long value = num.Value;
			string name = First(item2, "name", "item_name");
			string icon = First(item2, "icon", "icon_path");
			int? quality;
			if (num2.HasValue)
			{
				long valueOrDefault = num2.GetValueOrDefault();
				if (valueOrDefault >= int.MinValue && valueOrDefault <= int.MaxValue)
				{
					quality = (int)num2.Value;
					goto IL_016d;
				}
			}
			quality = null;
			goto IL_016d;
			IL_016d:
			list.Add(new GameDataSnapshotItem(type, text, value, name, icon, quality));
		}
		return list;
	}

	private static IReadOnlyList<long> ReadPowers(JsonElement item)
	{
		JsonElement jsonElement = Array(item, "power");
		if (jsonElement.ValueKind != JsonValueKind.Array)
		{
			return System.Array.Empty<long>();
		}
		List<long> list = new List<long>();
		foreach (JsonElement item2 in jsonElement.EnumerateArray().Take(3))
		{
			long? num = Integer(item2);
			if (num.HasValue && num.GetValueOrDefault() > 0)
			{
				list.Add(num.Value);
			}
		}
		return list;
	}

	private static IReadOnlyDictionary<string, string> ScalarDetails(JsonElement item, params string[] excludedProperties)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		HashSet<string> hashSet = excludedProperties.ToHashSet<string>(StringComparer.Ordinal);
		foreach (JsonProperty item2 in item.EnumerateObject())
		{
			if (!hashSet.Contains(item2.Name))
			{
				if (dictionary.Count >= 24)
				{
					break;
				}
				string text;
				switch (item2.Value.ValueKind)
				{
				case JsonValueKind.String:
				case JsonValueKind.Number:
				case JsonValueKind.True:
				case JsonValueKind.False:
					text = ScalarValue(item2.Value);
					break;
				case JsonValueKind.Array:
					text = $"[{item2.Value.GetArrayLength()}]";
					break;
				case JsonValueKind.Object:
					text = "{...}";
					break;
				default:
					text = string.Empty;
					break;
				}
				string value = text;
				if (!string.IsNullOrWhiteSpace(value))
				{
					dictionary[item2.Name] = Truncate(value, 256);
				}
			}
		}
		return dictionary;
	}

	private static long? Integer(JsonElement element, string name)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
		{
			return null;
		}
		return Integer(value);
	}

	private static long? Integer(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var value2))
		{
			return value2;
		}
		if (value.ValueKind != JsonValueKind.String || !long.TryParse(value.GetString(), out value2))
		{
			return null;
		}
		return value2;
	}

	private static string First(JsonElement element, params string[] names)
	{
		foreach (string name in names)
		{
			string text = Scalar(element, name);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	private static JsonElement Object(JsonElement element, string name)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return default(JsonElement);
		}
		return value;
	}

	private static JsonElement Array(JsonElement element, string name)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return default(JsonElement);
		}
		return value;
	}

	private static bool Boolean(JsonElement element, string name)
	{
		if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
		{
			return value.ValueKind == JsonValueKind.True;
		}
		return false;
	}

	private static string Scalar(JsonElement element, string name, string fallback = "")
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
		{
			return fallback;
		}
		return ScalarValue(value, fallback);
	}

	private static string ScalarValue(JsonElement value, string fallback = "")
	{
		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? fallback, 
			JsonValueKind.Number => value.GetRawText(), 
			JsonValueKind.True => "true", 
			JsonValueKind.False => "false", 
			_ => fallback, 
		};
	}

	private static GameDataSnapshot Unavailable(string view, string featureId, string error, string action = "")
	{
		return new GameDataSnapshot(view, featureId, action, Available: false, error, new Dictionary<string, string>(StringComparer.Ordinal), System.Array.Empty<GameDataSnapshotRecord>());
	}

	private static string Truncate(string value, int maximumLength)
	{
		if (value.Length > maximumLength)
		{
			return value.Substring(0, maximumLength);
		}
		return value;
	}
}
