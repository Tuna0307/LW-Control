using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LWBridge.MapTrace
{
    public static partial class Sampler
    {
        // Observation window, not a network request ID or a production provider.
        public static readonly Func<object, string, bool> Begin = BeginWindow;
        public static readonly Func<object, string, string> Match = MatchWindow;
        public static readonly Func<object, string, bool> BindWorld = BindWindowWorld;
        public static readonly Func<string, bool> Retire = RetireWindow;
        sealed class References : IEqualityComparer<object>
        {
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
        sealed class Window
        {
            public string Key = "";
            public object? Manager;
            public object? Scene;
            public long Started;
            public int Server, World, X, Y, Lod;
            public readonly HashSet<object> Baseline = new HashSet<object>(new References());
        }
        static readonly object WindowLock = new object();
        static Window? Active;
        static object? ReceiveTable(object manager) => Field(Field(Field(manager, "m_proxy"),
            "<profiler>k__BackingField"), "_receiveData");
        static bool Number(object? value, int expected) => (value is int i && i == expected) ||
            (value is long l && l == expected);
        static int? Integer(object? value) => value is int i ? i : value is long l && l >= int.MinValue && l <= int.MaxValue ? (int?)l : null;
        static object? Member(object? value, string name) => Property(value, name) ?? Field(value, name);
        static (int x, int y)? Tile(object world, int index)
        {
            var method = world.GetType().GetMethod("IndexToTilePos", new[] { typeof(int) });
            var tile = method?.Invoke(world, new object[] { index });
            var x = Integer(Member(tile, "x"));var y = Integer(Member(tile, "y"));
            return x.HasValue && y.HasValue ? ((int x,int y)?)(x.Value,y.Value) : null;
        }
        static bool BindWindowWorld(object world, string key)
        {
            lock (WindowLock)
            {
                if (Active == null || Active.Key != key || Expired(Active) || Member(world,"PointManager") == null) return false;
                Active.Scene = world;return true;
            }
        }
        static bool Expired(Window w) => (Stopwatch.GetTimestamp() - w.Started) /
            (double)Stopwatch.Frequency > 12;
        static Window? Parse(string key)
        {
            var p = key.Split('|');
            if (p.Length != 9 || key.Length > 512) return null;
            for (int n = 0; n < 3; n++)
            {
                if (p[n].Length < 1 || p[n].Length > 128) return null;
                foreach (char c in p[n]) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-') return null;
            }
            if (!int.TryParse(p[3], out int pid) || pid <= 0 ||
                !int.TryParse(p[4], out int server) || server <= 0 ||
                !int.TryParse(p[5], out int world) || world < 0 ||
                !int.TryParse(p[6], out int x) || x < 0 ||
                !int.TryParse(p[7], out int y) || y < 0 ||
                !int.TryParse(p[8], out int lod) || lod != 0) return null;
            return new Window { Key = key, Server = server, World = world, X = x, Y = y, Lod = lod };
        }
        static bool BeginWindow(object manager, string key)
        {
            lock (WindowLock)
            {
                if (Active != null && !Expired(Active)) return false;
                Active = null;
                var w = Parse(key);
                if (w == null) return false;
                try
                {
                    if (!(ReceiveTable(manager) is IEnumerable entries)) return false;
                    int count = 0;
                    foreach (var pair in entries)
                    {
                        if (++count > 512) return false;
                        var packet = Property(pair, "Key");
                        if (packet == null) return false;
                        w.Baseline.Add(packet);
                    }
                    w.Manager = manager;
                    w.Started = Stopwatch.GetTimestamp();
                    Active = w;
                    return true;
                }
                catch { return false; }
            }
        }
        static string MatchWindow(object manager, string key)
        {
            lock (WindowLock)
            {
                var w = Active;
                if (w == null || w.Key != key || !ReferenceEquals(manager, w.Manager) || w.Scene == null) return "unarmed";
                if (Expired(w)) { Active = null; return "expired"; }
                try
                {
                    if (!(ReceiveTable(manager) is IEnumerable entries)) return "unavailable";
                    bool matched = false;
                    int scanned = 0;
                    foreach (var pair in entries)
                    {
                        if (++scanned > 512) return "incomplete";
                        var packet = Property(pair, "Key");
                        if (packet == null || w.Baseline.Contains(packet)) continue;
                        var info = Property(packet, "info");
                        var content = info;
                        var cmd = Get(content, "GetUtfString", "c") as string;
                        if (cmd == null) { content = Get(info, "GetSFSObject", "p"); cmd = Get(content, "GetUtfString", "c") as string; }
                        if (cmd != "world.get.block") continue;
                        var parameters = Get(content, "GetSFSObject", "p");
                        var array = Get(parameters, "GetSFSArray", "serverPointArr");
                        if (!(Size(array) is int count) || count < 0 || count > 40) return "incomplete";
                        for (int i = 0; i < count; i++)
                        {
                            var segment = At(array, i);
                            object? Int(string name) => Get(segment, "GetInt", name) ?? Get(segment, "GetLong", name);
                            if (!Number(Int("serverId"), w.Server) || !Number(Int("worldId"), w.World) ||
                                !Number(Int("x"), w.X) || !Number(Int("y"), w.Y) || !Number(Int("viewLvl"), w.Lod)) continue;
                            // A genuinely empty points array is valid. Missing/malformed is not.
                            if (!(Size(Get(segment, "GetSFSArray", "points")) is int points) || points < 0) return "incomplete";
                            var lbIndex=Integer(Int("leftBottom"));var rtIndex=Integer(Int("rightTop"));
                            if (!lbIndex.HasValue || !rtIndex.HasValue) return "bounds_unavailable";
                            var lb=Tile(w.Scene,lbIndex.Value);var rt=Tile(w.Scene,rtIndex.Value);
                            if (!lb.HasValue || !rt.HasValue) return "bounds_unavailable";
                            if (lb.Value.x>w.X || rt.Value.x<w.X || lb.Value.y>w.Y || rt.Value.y<w.Y) continue;
                            var cache=Field(Member(w.Scene,"PointManager"),"_pointInfos") as IEnumerable;
                            if (cache==null) return "cache_unavailable";
                            int inBounds=0,visited=0;
                            foreach (var cached in cache)
                            {
                                if (++visited>100000) return "incomplete";
                                var infoValue=Property(cached,"Value") ?? cached;
                                var pointIndex=Integer(Member(infoValue,"pointIndex"));
                                if (!pointIndex.HasValue) return "cache_unavailable";
                                var pos=Tile(w.Scene,pointIndex.Value);
                                if (!pos.HasValue) return "cache_unavailable";
                                if (pos.Value.x>=lb.Value.x && pos.Value.x<=rt.Value.x &&
                                    pos.Value.y>=lb.Value.y && pos.Value.y<=rt.Value.y) inBounds++;
                            }
                            if (inBounds!=points) continue;
                            matched = true;
                        }
                    }
                    return matched ? "matched" : "waiting";
                }
                catch { return "unavailable"; }
            }
        }
        static bool RetireWindow(string key)
        {
            lock (WindowLock)
            {
                if (Active == null || Active.Key != key) return false;
                Active = null;
                return true;
            }
        }
    }
}
