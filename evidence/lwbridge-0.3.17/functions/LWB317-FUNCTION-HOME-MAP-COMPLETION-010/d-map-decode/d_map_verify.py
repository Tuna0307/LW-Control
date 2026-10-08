"""Static verification for HOME-MAP-COMPLETION-010 d-map-decode.

Hash-gated: d_disasm asserts the reference SHA-256
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Read-only. Run: python -I d_map_verify.py
Every check re-reads exact bytes from the reference EXE and raises on mismatch.
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import d_disasm as d  # noqa: E402  (asserts SHA-256 on import)

checks = []


def rd(rva, n):
    return d.getdata(rva, n)


def check(name, cond):
    assert cond, "FAILED: " + name
    checks.append(name)


def hx(rva, n):
    return rd(rva, n).hex()


def at(rva, hexs):
    return hx(rva, len(hexs) // 2) == hexs


def table(rva):
    t = rd(rva, 0x80)
    out = []
    for i in range(8):
        p, l = struct.unpack_from("<QQ", t, i * 16)
        out.append(rd(p - d.base, l).decode())
    return out


# ---- 1. map_scan_start service 0xFA09E-0xFC643 ---------------------------
check("start service range", d.locate(0xFA09E) == (0xFA09E, 0xFC643))
check("public handler range (no mode/types validation in handler)", d.locate(0x143D67) == (0x143D67, 0x144C41))
check("GAME_CONNECTION_UNAVAILABLE+msg", rd(0x82EF48, 54) == b"GAME_CONNECTION_UNAVAILABLE" + b"game connection unavailable")
check("SCAN_RUNNING+msg", rd(0x82EF7E, 36) == b"SCAN_RUNNING" + b"map scan already running")
check("WORLD_MAP_FAILED+msg", rd(0x82F000, 41) == b"WORLD_MAP_FAILED" + b"failed to enter world map")
check("SERVER_UNAVAILABLE+msg", rd(0x82AD90, 47) == b"SERVER_UNAVAILABLE" + b"current server id unavailable")
check("INVALID_SCAN_TYPES+msg", rd(0x82F244, 50) == b"INVALID_SCAN_TYPES" + b"no valid map scan types selected")
check("INVALID_SCAN_MODE+msg", rd(0x82F03B, 53) == b"INVALID_SCAN_MODE" + b"map scan mode must be normal or fast")
check("MAP_SIZE_UNAVAILABLE+msg", rd(0x82F20C, 56) == b"MAP_SIZE_UNAVAILABLE" + b"world map dimensions are unavailable")
check("serverIdSource literal 'live'", rd(0x82EEEE, 4) == b"live" and struct.unpack("<QQ", rd(0x82EEF8, 16))[1] == 4)
# order evidence (instruction bytes)
check("0xfa105 game-conn predicate; 0xfa10c je->0xfa146 GAME_CONNECTION_UNAVAILABLE", hx(0xFA105, 1) == "e8" and hx(0xFA10C, 2) == "7438")
check("0xfa112 active-scan predicate; je 0xfa1a7 else SCAN_RUNNING", hx(0xFA112, 1) == "e8" and hx(0xFA119, 2) == "0f84")
check("0xfa1d2 calls status service 0xf8cb2 AFTER SCAN_RUNNING", hx(0xFA1D2, 1) == "e8" and d.locate(0xF8CB2) == (0xF8CB2, 0xFA09E))
check("isInWorld must be bool true else enterWorldMap (cmp tag==1; cmp val!=0)", at(0xFA307, "803901") and at(0xFA310, "80790100"))
check("enterWorldMap timeout 0x1388 (5000 ms)", at(0xFA35D, "49c786d800000088130000"))
check("world poll deadline now+0x2710 (0xfa440)", hx(0xFA440, 6) == "480510270000")
check("poll interval 0x1dcd6500 ns (500 ms) at 0xfa4af", hx(0xFA4AF, 6) == "41b80065cd1d")
check("serverId<=0 -> SERVER_UNAVAILABLE (jle 0xfaaa4 at 0xfa8c8)", hx(0xFA8C8, 6) == "0f8ed6010000")
check("serverIdSource!='live' -> SERVER_UNAVAILABLE (0xfa909)", hx(0xFA909, 6) == "0f8495010000")
check("selectedTypes: only JSON array (tag 4) honoured; else default", hx(0xFA931, 3) == "803804" and hx(0xFA934, 2) == "7522")
check("empty normalized types -> INVALID_SCAN_TYPES", at(0xFA9FB, "4983beb8000000000f84fc000000"))
check("scanMode only honoured when JSON string (tag 3), else default normal", at(0xFAA38, "803803") and at(0xFAA3B, "488d0def457300"))
check("'fast' dword compare (0xfc49c) ; fast=20 (0xfc4a8) ; normal=8 (0xfab2f)", hx(0xFC49C, 6) == "813966617374" and hx(0xFC4A8, 5) == "b814000000" and hx(0xFAB2F, 5) == "b808000000")
check("tileWidth<=0 / tileHeight<=0 -> MAP_SIZE_UNAVAILABLE (0xfabac/0xfabbc)", hx(0xFABAC, 6) == "0f8e4c170000" and hx(0xFABBC, 6) == "0f8e3c170000")
default_types = table(0x82DE50)
check("default selectedTypes order", default_types == ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"])
check("valid-type vocabulary table (exact compare, closure 0x396f04)", table(0xD56ED8) == default_types)
check("closure drops non-string elements (cmp tag 3, 0x396f5f)", hx(0x396F5F, 5) == "807c243003")

# ---- 2. 'current server changed during map scan' guard -----------------------
check("guard string literal", rd(0x82EEC1, 38) == b"current server changed during map scan")
refs = sorted(x for x in d.xrefs_to(0x82EEC1))
check("guard string referenced only at 0xf9783 (status service)", refs == [0xF9783])
check("prev isReading must be bool true", at(0xF9751, "803801") and at(0xF9754, "7554") and at(0xF9756, "80780100") and at(0xF975A, "744e"))
check("prevServerId>0 && prevServerId != liveServerId", at(0xF975C, "4d85e40f9fc04d39ec0f95c184c8"[:-4]) and at(0xF9762, "4d39ec0f95c184c8"))
check("guard on in-world/unknown branch only (je 0xf96e3 when isInWorld!=false)", at(0xF9559, "80bc24e800000000") and at(0xF9561, "0f847c010000"))
check("live serverId must be >0 for guard (jle 0xf971d)", hx(0xF96E3, 5) == "4d85ed7e35")
check("fail-ctor args: stop=1, preserve=0, msg len 0x26", at(0xF9770, "c644243000c644242801") and at(0xF977A, "48c744242026000000"))
check("fail ctor 0x426d67 sets isReading=false", hx(0x426E59, 6) == "6641c7070100")
idle_p, idle_l = struct.unpack("<QQ", rd(0xD69130, 16))
check("fail ctor phase literal 'idle'", rd(idle_p - d.base, idle_l) == b"idle")
check("fail ctor resumeAvailable=false (0x426f7d)", hx(0x426F7D, 6) == "6641c7060100")
check("fail ctor provider stopMapScan then emit 0x428a92", rd(0xD69178, 11) == b"stopMapScan" and d.locate(0x428A92) == (0x428A92, 0x428D90))
check("fail tx (0x3e79f8) SQL status='failed' + staging delete", b"UPDATE scan_runs SET status='failed',error=?1,updated_at=?2 WHERE id=?3 AND status='running'" in rd(0xD5DD7A, 100) and rd(0xD57215, 40).startswith(b"DELETE FROM scan_records WHERE run_id=?1"))
check("guard returns Ok state (ctor MIN=Ok -> falls to 0xf97aa and rewrites serverId=live)", hx(0x426FEC, 10) == "48b80000000000000080" and hx(0xF97A4, 6) == "0f8138030000")

# ---- 3. query coercion ------------------------------------------------------
check("search service range", d.locate(0x3E10E7) == (0x3E10E7, 0x3E6952))
check("3eb60e (loose i64) range", d.locate(0x3EB60E) == (0x3EB60E, 0x3EB80C))
check("3eb56c (loose f64) range", d.locate(0x3EB56C)[0] == 0x3EB56C)
check("page: value<2 or absent -> 1", at(0x3E1144, "4883fa02be01000000480f4cd6") and at(0x3E1151, "a801480f44d6"))
check("pageSize: <2->1, cap 0xc8, absent->0x32", hx(0x3E1174, 4) == "4883fa02" and hx(0x3E117C, 5) == "b9c8000000" and hx(0x3E118A, 6) == "41be32000000")
check("serverId absent -> 0", hx(0x3E11AE, 6) == "a8014c0f44ee")
check("kind Err -> 0x3e125f (swallowed) ; serverId<=0 -> 0x3e126c empty page", hx(0x3E120C, 2) == "7151" and hx(0x3E1211, 2) == "7e59")
check("empty page literals total/page/pageSize/rows", at(0x3E1299, "c640046c") and at(0x3E1311, "c70070616765") and at(0x3E140C, "c700726f7773"))
check("INVALID_MAP_KIND literal (only emitted by 0x3c3bed callers)", rd(0xD56FA5, 16) == b"INVALID_MAP_KIND" and d.locate(0x3C3BED) == (0x3C3BED, 0x3C3C9E))
check("treasureType/suppliesType via loose i64, used only when >0", hx(0x3E36EF, 6) == "4885d20f9fc1" and hx(0x3E37D1, 6) == "4885d20f9fc1")
check("min/max Level/Power via loose f64 (0x3eb56c); swap when min>max", at(0x3E2797, "e8d08d0000") and at(0x3E27F6, "66440f2ec6"))
check("wrapper 0x42b787: strict serverId<=0 -> shared-state serverId (jg 0x42b8cf)", d.locate(0x42B787) == (0x42B787, 0x42B988) and at(0x42B7C2, "4885c00f8f04010000"))
check("options 0x427069: requested serverId<=0 -> strict shared-state serverId", hx(0x427087, 5) == "4885ff7f1a")
check("map_search handler: default query page=1,pageSize=50", d.locate(0x1761C1) == (0x1761C1, 0x176ED4) and at(0x1766B4, "41c60102") and at(0x1766C0, "49c7411001000000") and at(0x176741, "49c7411032000000"))
# ---- 4. city export ---------------------------------------------------------
check("export handler range", d.locate(0x129FA7) == (0x129FA7, 0x12BC48))
check("headers len must equal 12 (cmp [rbx+0x1370],0xc ; jne error)", at(0x12A52E, "4883bb701300000c7557"))
check("headers error text", rd(0x82AC1A, 31) == b"city export headers are invalid" and rd(0x82ABE8, 17) == b"MAP_EXPORT_FAILED")
check("server error text", rd(0x82ABF9, 33) == b"city export server is unavailable")
check("export serverId loose i64 (0x39f992); <=0 -> shared-state serverId", d.locate(0x39F992)[0] == 0x39F992 and at(0x12A8B6, "4885c00f8fe7000000"))
check("final server <=0 -> error (jle 0x12ad42)", at(0x12A9C5, "0f8e77030000"))
check("sheetName default 'Cities'; labels 'Yes'/'No'", rd(0x82AB10, 6) == b"Cities" and rd(0x82AB1E, 3) == b"Yes" and rd(0x82AB28, 2) == b"No")
check("save dialog 'Excel workbook' after server check (0x12ab26 > 0x12a9c5)", rd(0x82AB80, 14) == b"Excel workbook")

out = {"sha256": d.EXPECTED.upper(), "checkCount": len(checks), "checks": checks}
Path(__file__).with_name("map-handlers-verify.json").write_text(json.dumps(out, indent=1), encoding="utf-8")
print("OK", len(checks), "checks")
