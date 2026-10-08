"""Inert companion checks for the *actual-game-proven* direct Lua delegate path.

This is not a substitute for actual xLua or Last War authenticated pipe proof.
Run using the existing lupa-enabled Python environment.
"""
from __future__ import annotations
from pathlib import Path
import unittest
from lupa import LuaRuntime

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_overview_bridge.lua"


class PipeDelegateCurrentChecks(unittest.TestCase):
    def setUp(self):
        self.text = SOURCE.read_text(encoding="utf-8")
        self.lua = LuaRuntime(unpack_returned_tuples=True)

    def test_full_lua_syntax(self):
        self.lua.execute("assert(loadfile(" + repr(SOURCE.as_posix()) + "))")

    def test_source_invokes_delegate_exactly_once(self):
        start = self.text.index("    local connect = pipe_runtime.adapterConnect")
        end = self.text.index("    pipe_invocation_receipt(ok_connect", start)
        block = self.text[start:end]
        self.assertEqual(block.count("connect(control.controlPipePath, hello, root)"), 1)
        self.assertNotIn("invoke:Invoke", block)
        self.assertNotIn('type(connect) == "function"', block)
        self.assertIn('local invocation_route = "delegate_direct_once"', block)

    def test_userdata_callable_void_is_success_without_retry(self):
        invoked = []
        def delegate(a, b, c):
            invoked.append((a, b, c))
            return None
        self.lua.globals().target = delegate
        actual_type, ok, returned = self.lua.eval("""
            function()
                local t = type(target)
                local ok, value = pcall(function()
                    return target("path", "hello", "root")
                end)
                return t, ok, value
            end
        """)()
        self.assertEqual(actual_type, "userdata")
        self.assertTrue(ok)
        self.assertIsNone(returned)
        self.assertEqual(invoked, [("path", "hello", "root")])

    def test_userdata_callable_failure_is_not_retried(self):
        invoked = []
        def delegate(*args):
            invoked.append(args)
            raise RuntimeError("intentional-negative")
        self.lua.globals().target = delegate
        ok, err = self.lua.eval("""
            function()
                return pcall(function() return target("a", "b", "c") end)
            end
        """)()
        self.assertFalse(ok)
        self.assertEqual(len(invoked), 1)
        self.assertIn("intentional-negative", str(err))


if __name__ == "__main__":
    unittest.main()
