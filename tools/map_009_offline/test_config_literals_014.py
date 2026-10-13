import unittest
import compact_lua53 as lua
from read_config_literals_014 import read_literals


def instruction(op,a=0,b=0,c=0):
    return lua.OPS.index(op) | a << 6 | c << 14 | b << 23


class ConfigLiteralTests(unittest.TestCase):
    def proto(self,code):return dict(children=[],constants=['name','real-game-label'],code=code)

    def test_exact_literal_key_value_and_trailing_void_return(self):
        source=self.proto([instruction('NEWTABLE'),instruction('SETTABLE',b=256,c=257),
                           instruction('RETURN',b=2),instruction('RETURN',b=1)])
        self.assertEqual(read_literals(source),{'name':'real-game-label'})

    def test_arbitrary_code_calls_globals_branches_and_tail_reject(self):
        for op in ['CALL','GETTABUP','CLOSURE','JMP','TAILCALL']:
            with self.assertRaises(ValueError):read_literals(self.proto([instruction(op)]))
        with self.assertRaises(ValueError):
            read_literals(self.proto([instruction('NEWTABLE'),instruction('RETURN',b=2),instruction('CALL')]))

    def test_array_offsets_preserve_config_column_number(self):
        source=self.proto([instruction('NEWTABLE'),instruction('LOADK',a=1,c=1),
                           instruction('SETLIST',b=1,c=2),instruction('RETURN',b=2)])
        self.assertEqual(read_literals(source),{51:'real-game-label'})


if __name__=='__main__':unittest.main()
