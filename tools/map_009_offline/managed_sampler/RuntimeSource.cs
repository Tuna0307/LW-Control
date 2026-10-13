using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;

namespace LWBridge.MapTrace
{
    public static partial class Sampler
    {
        static object? RuntimeSource;
        static readonly Dictionary<short, OpCode> Opcodes = MakeOpcodes();
        static Dictionary<short, OpCode> MakeOpcodes()
        {
            var map = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.GetValue(null) is OpCode code) map[code.Value] = code;
            return map;
        }
        static object DescribeRuntimeSource()
        {
            var rows = new List<object>();
            Type? type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("WorldPointManager", false);
                if (type != null) break;
            }
            if (type == null) return rows;
            foreach (var name in new[] { "StartViewRequest", "UpdateViewRequest", "UpdateLWAoi",
                "UpdateLWAoi_Normal", "SendAoiRequest", "HandleViewPointsReply", "AddPointInfo" })
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.Name != name) continue;
                    var row = new Dictionary<string, object?> { ["method"] = name, ["metadataToken"] = method.MetadataToken };
                    rows.Add(row);
                    try
                    {
                        var bytes = method.GetMethodBody()?.GetILAsByteArray();
                        if (bytes == null || bytes.Length > 20000) { row["bodyUnavailable"] = true; continue; }
                        row["byteLength"] = bytes.Length;
                        row["ilBase64"] = Convert.ToBase64String(bytes);
                        using (var hash = SHA256.Create()) row["sha256"] = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                        var references = new List<object>();
                        row["references"] = references;
                        for (int pos = 0; pos < bytes.Length;)
                        {
                            int offset = pos;
                            short value = bytes[pos++];
                            if (value == 0xfe) value = (short)(0xfe00 | bytes[pos++]);
                            if (!Opcodes.TryGetValue(value, out var opcode)) throw new InvalidOperationException("unknown opcode");
                            int size;
                            switch (opcode.OperandType)
                            {
                                case OperandType.InlineNone: size = 0; break;
                                case OperandType.ShortInlineI: case OperandType.ShortInlineVar: case OperandType.ShortInlineBrTarget: size = 1; break;
                                case OperandType.InlineVar: size = 2; break;
                                case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                                case OperandType.InlineSwitch: size = 4 + checked(4 * BitConverter.ToInt32(bytes, pos)); break;
                                default: size = 4; break;
                            }
                            if (size < 0 || pos + size > bytes.Length) throw new InvalidOperationException("operand exceeds body");
                            if (opcode.OperandType == OperandType.InlineMethod || opcode.OperandType == OperandType.InlineField || opcode.OperandType == OperandType.InlineType)
                            {
                                var token = BitConverter.ToInt32(bytes, pos);
                                var reference = new Dictionary<string, object?> { ["offset"] = offset, ["opcode"] = opcode.Name, ["token"] = token };
                                references.Add(reference);
                                try
                                {
                                    var member = method.Module.ResolveMember(token);
                                    reference["name"] = member?.DeclaringType?.FullName + "::" + member?.Name;
                                }
                                catch (Exception e) { reference["resolveError"] = e.GetType().Name; }
                            }
                            pos += size;
                        }
                    }
                    catch (Exception e) { row["errorType"] = e.GetType().Name; }
                }
            }
            return rows;
        }
    }
}
