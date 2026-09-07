import unittest

from tools.extract_reference_ui_text import extract


class ReferenceUiTextTests(unittest.TestCase):
    def test_preserves_context_for_identical_english_actions(self):
        source = 'one:[{mode:"go",zh:"派遣",en:"Dispatch"}],two:[{mode:"go",zh:"派遣采集",en:"Dispatch"}]'
        result = extract(source)
        self.assertEqual(result["feature.one.action.Dispatch"], "派遣")
        self.assertEqual(result["feature.two.action.Dispatch"], "派遣采集")

    def test_recovers_feature_name_description_and_escaped_text(self):
        source = r'example:{zh:"名称",en:"Name",zhDescription:"点击\"查看\"",enDescription:"Click \"View\""}'
        result = extract(source)
        self.assertEqual(result["feature.example.name.Name"], "名称")
        self.assertEqual(result['feature.example.description.Click "View"'], '点击"查看"')

    def test_ignores_executable_expressions(self):
        source = 'window.callHost();x==="zh"?getSecret():fetch("endpoint");y==="zh"?"确定":"OK"'
        self.assertEqual(extract(source), {"OK": "确定"})


if __name__ == "__main__":
    unittest.main()
