using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace LWBridge.MapTrace
{
    // Diagnostic-only; no network sends, handlers, mutations, dumps or raw buffers.
    public static class Sampler
    {
        public static readonly Func<object, string> Read = Sample;
        static object? Field(object? o, string name)
        {
            if (o == null) return null;
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }
        static object? Property(object? o, string name) => o?.GetType().GetProperty(name)?.GetValue(o, null);
        static object? Size(object? o) => o?.GetType().GetMethod("Size", Type.EmptyTypes)?.Invoke(o, null);
        static object? Get(object? o, string method, string key)
        {
            if (o == null) return null;
            try { return o.GetType().GetMethod(method, new[] { typeof(string) })?.Invoke(o, new object[] { key }); }
            catch { return null; }
        }
        static object? At(object? o, int index)
        {
            try { return o?.GetType().GetMethod("GetSFSObject", new[] { typeof(int) })?.Invoke(o, new object[] { index }); }
            catch { return null; }
        }
        static List<object> Keys(object? o)
        {
            var keys = new List<object>();
            var values = o?.GetType().GetMethod("GetKeys", Type.EmptyTypes)?.Invoke(o, null) as IEnumerable;
            if (values != null) foreach (var key in values) { if (keys.Count == 40) break; if (key is string name) keys.Add(name); }
            return keys;
        }
        static object Segments(object? parameters)
        {
            var array = Get(parameters, "GetSFSArray", "serverPointArr");
            var count = Size(array);
            var result = new Dictionary<string, object?> { ["count"] = count };
            var rows = new List<object>();
            result["entries"] = rows;
            if (!(count is int n)) return result;
            result["truncated"] = n > 40;
            for (int i = 0; i < Math.Min(n, 40); i++)
            {
                var item = At(array, i);
                var keys = Keys(item);
                var numbers = new Dictionary<string, object?>();
                var arrays = new Dictionary<string, object?>();
                foreach (var key in keys)
                {
                    var name = (string)key;
                    // Region coordinates/context only; no player/account or payload values.
                    if (name == "server" || name == "serverId" || name == "world" || name == "worldId" ||
                        name == "index" || name == "aoiIndex" || name == "lod" || name == "viewIndex" ||
                        name == "viewLvl" || name == "x" || name == "y")
                        numbers[name] = Get(item, "GetInt", name) ?? Get(item, "GetLong", name);
                    var nested = Get(item, "GetSFSArray", name);
                    if (nested != null) arrays[name] = Size(nested);
                }
                rows.Add(new Dictionary<string, object?> { ["keys"] = keys, ["numbers"] = numbers, ["arrayCounts"] = arrays });
            }
            return result;
        }
        static string Sample(object manager)
        {
            var result = new Dictionary<string, object?>();
            var rows = new List<object>();
            int matchingRows = 0;
            result["packets"] = rows;
            try
            {
                var proxy = Field(manager, "m_proxy");
                var profiler = Field(proxy, "<profiler>k__BackingField");
                foreach (var direction in new[] { "send", "receive" })
                {
                    var table = Field(profiler, direction == "send" ? "_sendData" : "_receiveData");
                    result[direction + "Count"] = Property(table, "Count");
                    int scanned = 0, infoCount = 0;
                    if (table is IEnumerable entries)
                    {
                        foreach (var pair in entries)
                        {
                            if (++scanned > 512) break;
                            var packet = Property(pair, "Key");
                            if (packet == null) continue;
                            var info = Property(packet, "info");
                            if (info == null) continue;
                            infoCount++;
                            var content = info;
                            var command = Get(content, "GetUtfString", "c") as string;
                            if (command == null)
                            {
                                content = Get(info, "GetSFSObject", "p");
                                command = Get(content, "GetUtfString", "c") as string;
                            }
                            // Map-like command names only; no login/handshake payload.
                            if (command == null || command.Length > 128 ||
                                (command.IndexOf("world", StringComparison.OrdinalIgnoreCase) < 0 &&
                                 command.IndexOf("aoi", StringComparison.OrdinalIgnoreCase) < 0)) continue;
                            bool safe = true;
                            foreach (char ch in command) if (!(ch >= 'a' && ch <= 'z') &&
                                !(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9') &&
                                ch != '.' && ch != '_' && ch != '-') safe = false;
                            if (!safe) continue;
                            matchingRows++;
                            if (rows.Count >= 120) continue;
                            var parameters = Get(content, "GetSFSObject", "p");
                            var row = new Dictionary<string, object?> {
                                ["direction"] = direction, ["command"] = command,
                                ["packetHash"] = packet.GetHashCode(), ["keys"] = Keys(parameters),
                            };
                            if (command == "world.get.block") row["segments"] = Segments(parameters);
                            rows.Add(row);
                        }
                    }
                    result[direction + "Scanned"] = Math.Min(scanned, 512);
                    result[direction + "EnumerationTruncated"] = scanned > 512;
                    result[direction + "InfoCount"] = infoCount;
                }
            }
            catch (Exception e) { result["errorType"] = e.GetType().Name; }
            result["packetRowsTruncated"] = matchingRows > rows.Count;
            return Json(result);
        }
        static string Json(object? value)
        {
            if (value == null) return "null";
            if (value is bool flag) return flag ? "true" : "false";
            if (value is string s)
            {
                var quoted = new StringBuilder("\"");
                foreach (char ch in s)
                {
                    if (ch == '\\' || ch == '"') quoted.Append('\\').Append(ch);
                    else if (ch < 32) quoted.Append("\\u").Append(((int)ch).ToString("x4"));
                    else quoted.Append(ch);
                }
                return quoted.Append('"').ToString();
            }
            if (value is IDictionary map)
            {
                var b = new StringBuilder("{");
                foreach (DictionaryEntry pair in map) { if (b.Length > 1) b.Append(','); b.Append(Json(pair.Key.ToString())).Append(':').Append(Json(pair.Value)); }
                return b.Append('}').ToString();
            }
            if (value is IEnumerable array)
            {
                var b = new StringBuilder("[");
                foreach (var item in array) { if (b.Length > 1) b.Append(','); b.Append(Json(item)); }
                return b.Append(']').ToString();
            }
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
