using LWControl.Core;

namespace LWControl.Desktop;

public sealed partial class MainForm
{
    private static readonly Color ShellColor = ColorTranslator.FromHtml("#0b0c10");
    private static readonly Color TextColor = ColorTranslator.FromHtml("#dce2f2");
    // Opaque WinForms surfaces approximate the reference's translucent CSS panels.
    private static readonly Color SurfaceColor = Color.FromArgb(22, 25, 33);
    private static readonly Color BorderColor = Color.FromArgb(49, 54, 68);
    private DesktopAppearance appearance = new();
    private readonly ComboBox settingsLanguagePicker = new()
    {
        Width = 160, DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly Dictionary<string, Button> accentButtons = [];
    private string activePage = "home";
    private string AppearancePath => settingsPath + ".appearance.json";

    private void LoadAppearance()
    {
        try
        {
            var loaded = File.Exists(AppearancePath)
                ? JsonFiles.Read<DesktopAppearance>(AppearancePath) : new DesktopAppearance();
            loaded.Validate();
            appearance = loaded;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            appearance = new();
            AddLog($"Appearance could not be loaded; using defaults. {ex.Message}");
        }
        language = Enum.Parse<UiLanguage>(appearance.Language);
    }

    private void SaveAppearance()
    {
        appearance = appearance with { Language = language.ToString() };
        appearance.Validate();
        JsonFiles.Write(AppearancePath, appearance);
    }

    private void SetAccent(string name)
    {
        _ = DesktopPalette.Find(name);
        appearance = appearance with { Accent = name };
        ApplyAppearance();
        SaveAppearance();
    }

    private void ApplyAppearance()
    {
        var palette = DesktopPalette.Find(appearance.Accent);
        SuspendLayout();
        try
        {
            ApplyControlAppearance(this, palette);
            foreach (var pair in navigationButtons)
            {
                bool selected = pair.Key == activePage;
                pair.Value.BackColor = selected ? SurfaceColor : ShellColor;
                pair.Value.ForeColor = selected ? palette.HighlightColor : TextColor;
                pair.Value.FlatAppearance.BorderColor = selected ? palette.PrimaryColor : ShellColor;
            }
            foreach (var pair in accentButtons)
            {
                pair.Value.ForeColor = DesktopPalette.Find(pair.Key).HighlightColor;
                pair.Value.FlatAppearance.BorderColor = pair.Key == appearance.Accent
                    ? palette.PrimaryColor : BorderColor;
                pair.Value.Text = pair.Key == appearance.Accent ? $"✓ {T(pair.Key)}" : T(pair.Key);
                pair.Value.AccessibleDescription = T(pair.Key == appearance.Accent ? "Selected accent" : "Select accent");
            }
        }
        finally { ResumeLayout(performLayout: true); }
    }

    private static void ApplyControlAppearance(Control control, DesktopPalette palette)
    {
        control.BackColor = control is Form || control.Parent is null ? ShellColor : control.Parent.BackColor;
        control.ForeColor = TextColor;
        if (Equals(control.Tag, "feature-card") || Equals(control.Tag, "shortcut-card") || Equals(control.Tag, "settings-section") || Equals(control.Tag, "summary-card"))
            control.BackColor = SurfaceColor;
        if (Equals(control.Tag, "sidebar")) control.BackColor = Color.FromArgb(9, 11, 16);
        switch (control)
        {
            case TabPage tab:
                tab.UseVisualStyleBackColor = false;
                tab.BackColor = ShellColor;
                break;
            case Button button:
                button.UseMnemonic = false;
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = SurfaceColor;
                button.FlatAppearance.BorderColor = BorderColor;
                button.FlatAppearance.MouseOverBackColor = BorderColor;
                button.FlatAppearance.MouseDownBackColor = SurfaceColor;
                button.Paint -= PaintDisabledButton;
                button.Paint += PaintDisabledButton;
                break;
            case ComboBox combo:
                combo.BackColor = SurfaceColor;
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem -= DrawComboItem;
                combo.DrawItem += DrawComboItem;
                break;
            case TextBoxBase or ListBox or NumericUpDown:
                control.BackColor = SurfaceColor;
                break;
            case DataGridView table:
                table.EnableHeadersVisualStyles = false;
                table.BackgroundColor = ShellColor;
                table.GridColor = BorderColor;
                table.BorderStyle = BorderStyle.None;
                table.DefaultCellStyle.BackColor = SurfaceColor;
                table.DefaultCellStyle.ForeColor = TextColor;
                table.DefaultCellStyle.SelectionBackColor = BorderColor;
                table.DefaultCellStyle.SelectionForeColor = palette.HighlightColor;
                table.ColumnHeadersDefaultCellStyle.BackColor = SurfaceColor;
                table.ColumnHeadersDefaultCellStyle.ForeColor = palette.HighlightColor;
                table.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceColor;
                table.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.HighlightColor;
                break;
            case Label when control.Parent is not null:
                ((Label)control).UseMnemonic = false;
                control.BackColor = control.Parent.BackColor;
                break;
        }
        if (control.Tag is FeatureImplementationState state)
        {
            control.BackColor = SurfaceColor;
            control.ForeColor = state switch
            {
                FeatureImplementationState.Available => Color.FromArgb(110, 231, 183),
                FeatureImplementationState.Partial => Color.FromArgb(252, 211, 77),
                _ => Color.FromArgb(160, 168, 186),
            };
        }
        if (Equals(control.Tag, "summary-card")) control.BackColor = SurfaceColor;
        // DataGridView manages its editing/scrollbar child controls itself.
        if (control is DataGridView) return;
        foreach (Control child in control.Controls) ApplyControlAppearance(child, palette);
        if (control is ReferenceTabs tabs) tabs.ApplyPalette(palette);
    }

    private static void PaintDisabledButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button { Enabled: false } button) return;
        using var background = new SolidBrush(SurfaceColor);
        using var border = new Pen(BorderColor);
        e.Graphics.FillRectangle(background, button.ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, button.Width - 1, button.Height - 1);
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle,
            Color.FromArgb(139, 147, 164), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? BorderColor : SurfaceColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        string text = (e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text) ?? "";
        TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    private void ExportSessionLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = T("Text log|*.txt"), FileName = $"lwcontrol-session-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, log.Text);
        AddLog("Session log exported.");
    }

    public void RunAppearanceSmokeCheck(string? outputDirectory)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        foreach (string page in pages.Keys) ShowPage(page);
        foreach (var palette in DesktopPalette.All)
        {
            accentButtons[palette.Name].PerformClick();
            Check(appearance.Accent == palette.Name, "Accent button did not select its palette.");
            Check(JsonFiles.Read<DesktopAppearance>(AppearancePath).Accent == palette.Name,
                "Accent selection was not persisted.");
        }
        languagePicker.SelectedIndex = 1;
        Check(settingsLanguagePicker.SelectedIndex == 0, "Settings language did not follow top bar.");
        settingsLanguagePicker.SelectedIndex = 1;
        Check(languagePicker.SelectedIndex == 0, "Top bar language did not follow Settings.");
        using (var reloaded = new MainForm(settingsPath))
        {
            Check(reloaded.appearance.Accent == "Emerald" && reloaded.language == UiLanguage.English,
                "Appearance did not survive a new form instance.");
            Check(reloaded.enabled.Checked, "Appearance persistence changed claim settings.");
        }

        using (var card = BuildFeatureCard(ReferenceFeatureCatalog.All.Single(item => item.Id == "daily_free_claims")))
        {
            var actions = card.Controls.OfType<FlowLayoutPanel>().Last().Controls.OfType<Button>().ToArray();
            Check(actions.Single(item => item.Text == "View categories").Enabled,
                "Category details are unavailable.");
            Check(actions.Where(item => item.Text != "View categories").All(item => !item.Enabled),
                "Unimplemented claim actions became enabled.");
        }
        using (var page = BuildDailyClaimRecoveryPage())
        {
            var table = page.Controls.OfType<DataGridView>().Single();
            Check(((Array)table.DataSource!).Length == 7, "Category view is missing a reward category.");
        }

        // Invalid saved settings must not prevent startup or affect claim-policy settings.
        var saved = File.ReadAllText(AppearancePath);
        try
        {
            foreach (string invalid in new[] { "{", "null", "{\"accent\":\"Unknown\"}", "{\"language\":\"999\"}" })
            {
                File.WriteAllText(AppearancePath, invalid);
                using var fallback = new MainForm(settingsPath);
                Check(fallback.appearance.Accent == "Indigo", "Invalid appearance did not fall back.");
                Check(fallback.enabled.Checked, "Invalid appearance changed claim settings.");
            }
        }
        finally { File.WriteAllText(AppearancePath, saved); }

        SetAccent("Indigo");
        if (outputDirectory is not null)
        {
            Directory.CreateDirectory(outputDirectory);
            foreach (var entry in new[] { ("home", "home.png"), ("settings", "settings.png"), ("automation", "automation.png") })
            {
                ShowPage(entry.Item1);
                Application.DoEvents();
                using var bitmap = new Bitmap(Width, Height);
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(outputDirectory, entry.Item2));
            }
            using var dialog = new Form { Size = new Size(1040, 540), Font = Font, ShowInTaskbar = false };
            dialog.Controls.Add(BuildDailyClaimRecoveryPage());
            ApplyControlAppearance(dialog, DesktopPalette.Find(appearance.Accent));
            dialog.Show();
            Application.DoEvents();
            using var categoriesBitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(categoriesBitmap, new Rectangle(Point.Empty, categoriesBitmap.Size));
            categoriesBitmap.Save(Path.Combine(outputDirectory, "claim-categories.png"));
            dialog.Close();
        }
    }
}
