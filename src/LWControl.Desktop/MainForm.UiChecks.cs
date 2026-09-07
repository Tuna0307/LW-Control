using System.Text.Json;
using System.Text.RegularExpressions;
using LWControl.Core;

namespace LWControl.Desktop;

public sealed partial class MainForm
{
    public void RunLocalizationSmokeCheck(string? outputDirectory)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        static bool Neutral(string text) => Regex.IsMatch(text,
            @"^(?:[A-Z]|F\d+|Alt\+\d+|Space)(?: / (?:[A-Z]|F\d+|\d+))*$");
        var missing = staticUiText.Values.Where(text => !UiText.HasTranslation(text) && !Neutral(text)).Distinct().Order().ToArray();
        if (outputDirectory is not null)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "missing-translations.json"), JsonSerializer.Serialize(missing));
        }
        Check(missing.Length == 0, "Untranslated controls: " + string.Join(" | ", missing));
        foreach (var feature in ReferenceFeatureCatalog.All)
        {
            Check(UiText.HasFeature(feature.Id, "name", feature.Name), $"Missing scoped name: {feature.Id}");
            Check(UiText.HasFeature(feature.Id, "description", feature.Description), $"Missing scoped description: {feature.Id}");
            foreach (string action in feature.Actions)
                Check(UiText.HasFeature(feature.Id, "action", action), $"Missing scoped action: {feature.Id}: {action}");
            foreach (string text in new[] { feature.Name, feature.Description }.Concat(feature.Actions))
                Check(UiText.HasTranslation(text), $"Missing feature translation: {feature.Id}: {text}");
        }
        Check(UiText.Feature(UiLanguage.SimplifiedChinese, "secret_mobile_squad", "action", "Dispatch") == "派遣",
            "Dispatch lost its mobile-squad context.");
        Check(UiText.Feature(UiLanguage.SimplifiedChinese, "mining_dispatch", "action", "Dispatch") == "派遣采集",
            "Dispatch lost its gathering context.");

        languagePicker.SelectedIndex = 0;
        BuildPlan();
        var originalPlan = plan;
        var originalKinds = CurrentSettings().EnabledKinds;
        ShowPage("automation");
        var automation = (ReferenceTabs)pages["automation"];
        automation.SelectPage(2);
        worldRecords = Enumerable.Range(1, 2).Select(id => new CurrentWorldMapScanRecord
        {
            Id = id, PointId = id, Kind = "player_base", Name = "Alliance", ServerId = 1,
            SrcServerId = 1, WorldId = 1, X = id, Y = id, Source = "offline-ui-test",
        }).ToArray();
        worldSearch.Text = "Alliance";
        ApplyWorldFilter();
        worldGrid.CurrentCell = worldGrid.Rows[1].Cells[0];
        foreach (int languageIndex in new[] { 1, 0, 1 })
        {
            languagePicker.SelectedIndex = languageIndex;
            Check(ReferenceEquals(originalPlan, plan) && export.Enabled, "Language change discarded preview plan.");
            Check(source == UiText.Get(language, "SampleData"), "Preview source label did not change language.");
            Check(originalKinds.SetEquals(CurrentSettings().EnabledKinds), "Language change altered categories.");
            Check(activePage == "automation" && automation.SelectedIndex == 2, "Language change reset navigation.");
            Check(worldSearch.Text == "Alliance" && worldGrid.CurrentCell?.RowIndex == 1, "Language change reset map selection.");
            Check((string)worldGrid.Rows[1].Cells["Name"].Value! == "Alliance", "Localization altered game data.");
            Check(worldGrid.Columns.Cast<DataGridViewColumn>().All(column => column.Width >= TextRenderer.MeasureText(column.HeaderText, worldGrid.Font).Width + 20),
                "Map column headers are squeezed below their text width.");
            foreach (var control in Descendants(this).Where(item => item.Tag is FeatureImplementationState))
                Check(control.Text == T(control.Tag switch
                {
                    FeatureImplementationState.Partial => "PARTIAL",
                    FeatureImplementationState.Available => "AVAILABLE",
                    _ => "PENDING",
                }), "Availability badge did not follow language.");
            foreach (var card in Descendants(this).OfType<TableLayoutPanel>().Where(item => Equals(item.Tag, "feature-card")))
            {
                var actions = (FlowLayoutPanel)card.GetControlFromPosition(0, 2)!;
                Check(actions.Controls.OfType<Button>().Where(button => button.Text != T("View categories")).All(button => !button.Enabled),
                    "Localization enabled an unfinished feature.");
            }
        }
        worldRecords = [];
        worldSearch.Clear();
        ApplyWorldFilter();
        automation.SelectPage(0);

        // Render every page in both languages, including the narrower supported window.
        if (outputDirectory is not null)
        {
            foreach (int languageIndex in new[] { 0, 1 })
            {
                languagePicker.SelectedIndex = languageIndex;
                string suffix = languageIndex == 0 ? "en" : "zh";
                foreach (var size in new[] { new Size(1320, 840), MinimumSize })
                {
                    Size = size;
                    foreach (string key in pages.Keys)
                    {
                        ShowPage(key);
                        Application.DoEvents();
                        using var bitmap = new Bitmap(Width, Height);
                        DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(outputDirectory, $"{key}-{suffix}-{size.Width}.png"));
                    }
                }
                using var dialog = new Form { Size = new Size(1040, 540), Font = Font, ShowInTaskbar = false };
                dialog.Controls.Add(BuildDailyClaimRecoveryPage());
                RegisterStaticText(dialog);
                ApplyStaticLocalization();
                ApplyControlAppearance(dialog, DesktopPalette.Find(appearance.Accent));
                dialog.Show();
                Application.DoEvents();
                using var image = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(outputDirectory, $"claim-categories-{suffix}.png"));
                UnregisterText(dialog);
                dialog.Close();
            }
        }
        Console.WriteLine("PASS UI localization: 42 features, all static controls, availability gates, preserved plan/map/navigation, both languages.");
    }
}
