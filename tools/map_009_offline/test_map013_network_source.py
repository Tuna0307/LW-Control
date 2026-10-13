"""Real v24 managed sender/receive/registration source boundary checks.

These checks deliberately do not invent a server-echoed request generation,
and do not claim local manager flag changes prove native response receipt.
"""
from __future__ import annotations

import hashlib
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_managed import DEFAULT, SHA256
from rgmd_il import MetadataImage


class Map013NetworkSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with DEFAULT.open("rb") as stream:
            cls.digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if cls.digest != SHA256:
            raise ValueError("current game managed assembly hash changed")
        cls.image = MetadataImage.load(DEFAULT)
        cls.methods = {}
        for index, type_row in enumerate(cls.image.tables.TypeDef.rows, 1):
            if cls.image.type_name("TypeDef", index) != "WorldPointManager":
                continue
            for ref in type_row.MethodList:
                cls.methods[str(ref.row.Name)] = cls.image.disassemble(ref.row)
            break

    def test_exact_current_game_subscription_and_unsubscription_route(self):
        init = "\n".join(self.methods["Init"])
        uninit = "\n".join(self.methods["UnInit"])
        for command in (1433, 661, 2821011, 2821014):
            opcode = f"ldc.i4 {command}"
            self.assertIn(opcode, init)
            self.assertIn(opcode, uninit)
        # Native managed delegates subscribe and unsubscribe symmetrically.
        for token in ("0xA7913422", "0xA7913222", "0xA79134E2", "0xA7913882"):
            self.assertIn(f"ldftn token({token})", init)
            self.assertIn(f"ldftn token({token})", uninit)
        self.assertIn("callvirt token(0xA78120C2)", init)
        self.assertIn("callvirt token(0xA7812362)", uninit)

    def test_source_send_route_and_response_cache_independent(self):
        send = "\n".join(self.methods["SendAoiRequest"])
        receive = "\n".join(self.methods["HandleViewPointsReply"])
        insert = "\n".join(self.methods["AddPointInfo"])
        self.assertIn("IL_00d8: callvirt token(0xA79C9342)", send)
        self.assertIn("IL_0150: callvirt token(0xA79CB6E2)", send)
        self.assertIn("IL_00fb: ldarg.0", receive)
        self.assertIn("IL_0104: call token(0xA7912A42)", receive)
        self.assertIn("IL_0082: ldfld token(0x67929982)", insert)
        self.assertIn("IL_0090: callvirt token(0x279C8983)", insert)
        # These are separate call boundaries: send invocation alone is
        # not proof that the receive handler or cache insert executed.
        self.assertNotIn("IL_0104: call token(0xA7912A42)", send)


if __name__ == "__main__":
    unittest.main()
