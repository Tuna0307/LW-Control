#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;
using System.Collections.Generic;
using System.Linq;

namespace LWControl.Desktop;

internal static class WorldBlockScanCommandArguments
{
	internal const string StartMode = "block_scan_start";

	internal const string StopMode = "block_scan_stop";

	internal const string ClearMode = "block_scan_clear";

	public static Dictionary<string, string?> Create(string action, int concurrency, int blockCount, int blockSize, string? serverId = null, int? centerBlockX = null, int? centerBlockY = null)
	{
		string text = action switch
		{
			"start" => "block_scan_start", 
			"stop" => "block_scan_stop", 
			"clear" => "block_scan_clear", 
			_ => throw new ArgumentOutOfRangeException("action", action, "World block scan action must be start, stop, or clear."), 
		};
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["mode"] = text,
			["scan_scope"] = "blocks",
			["block_concurrency"] = NormalizeConcurrency(concurrency).ToString()
		};
		if (text == "block_scan_start")
		{
			dictionary["enter_world"] = "true";
			dictionary["scan_block_count"] = NormalizeBlockCount(blockCount).ToString();
			dictionary["scan_block_size"] = NormalizeBlockSize(blockSize).ToString();
			dictionary["enrich_details"] = "false";
			dictionary["enrich_power"] = "true";
			dictionary["max_records"] = "25000";
			dictionary["max_objects"] = "30000";
			dictionary["manager_only"] = "true";
			if (centerBlockX.HasValue)
			{
				dictionary["center_block_x"] = NormalizeBlockCoordinate(centerBlockX.Value).ToString();
			}
			if (centerBlockY.HasValue)
			{
				dictionary["center_block_y"] = NormalizeBlockCoordinate(centerBlockY.Value).ToString();
			}
		}
		string text2 = serverId?.Trim() ?? string.Empty;
		int length = text2.Length;
		if (length > 0 && length <= 12 && text2.All(char.IsAsciiDigit))
		{
			dictionary["target_server_id"] = text2;
			dictionary["expected_server_id"] = text2;
		}
		return dictionary;
	}

	public static int NormalizeConcurrency(int value)
	{
		return Math.Clamp(value, 1, 20);
	}

	public static int NormalizeBlockCount(int value)
	{
		int num = Math.Clamp(value, 1, 99);
		if (num % 2 != 0)
		{
			return num;
		}
		return num - 1;
	}

	public static int NormalizeBlockSize(int value)
	{
		return Math.Clamp(value, 16, 512);
	}

	public static int NormalizeBlockCoordinate(int value)
	{
		return Math.Clamp(value, 0, 99);
	}

	public static int TotalBlocks(IReadOnlyDictionary<string, string?> arguments)
	{
		int num = ((arguments.TryGetValue("scan_block_count", out string value) && int.TryParse(value, out var result)) ? NormalizeBlockCount(result) : 0);
		return num * num;
	}

	public static bool IsWorldBlockScan(IReadOnlyDictionary<string, string?> arguments)
	{
		bool flag = arguments.TryGetValue("mode", out string value);
		if (flag)
		{
			bool flag2;
			switch (value)
			{
			case "block_scan_start":
			case "block_scan_stop":
			case "block_scan_clear":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (flag && arguments.TryGetValue("scan_scope", out string value2))
		{
			return string.Equals(value2, "blocks", StringComparison.Ordinal);
		}
		return false;
	}
}
