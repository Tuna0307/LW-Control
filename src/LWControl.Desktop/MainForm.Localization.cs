namespace LWControl.Desktop;

public sealed partial class MainForm
{
    private readonly Dictionary<Control, string> staticUiText = [];
    private readonly Dictionary<Control, Func<string>> textBindings = [];
    private readonly Dictionary<ComboBox, string[]> staticComboItems = [];
    private bool applyingLanguage;
    private string T(string english) => UiText.Translate(language, english);
    private string F(string english, params object?[] args) => UiText.Format(language, english, args);
    private string RuntimeLabel(string code) => T(code switch
    {
        "ready" => "Ready",
        "heartbeat_missing" => "Not connected",
        "heartbeat_stale" => "Stale connection",
        "heartbeat_invalid" => "Invalid connection state",
        "runtime_version_mismatch" => "Runtime version mismatch",
        _ => code,
    });

    private TControl Bind<TControl>(TControl control, Func<string> text) where TControl : Control
    {
        textBindings[control] = text;
        control.Text = text();
        return control;
    }

    private void RegisterStaticText(Control root)
    {
        // These fields have live state or existing explicit localization bindings.
        Control[] dynamic = [status, header, runtimeSummaryLabel, worldCountLabel, enabled,
            expiry, chests, claimsPerRunLabel, categoriesLabel, languageLabel,
            loadObservationsButton, loadSampleButton, buildPlanButton, inspectBridgeButton,
            claimDailyTasksButton, worldScanButton, saveSettingsButton, export,
            startGameButton, refreshButton, regionChip, evidenceButton, locateWorldButton];
        if (root is Label or Button or CheckBox or GroupBox or Form)
        {
            if (root.Text.Length > 0 && !dynamic.Contains(root) && !textBindings.ContainsKey(root)
                && !(root is Button button && (navigationButtons.ContainsValue(button) || accentButtons.ContainsValue(button))))
                staticUiText.TryAdd(root, root.Text);
        }
        if (root is ComboBox combo && combo != worldTypeFilter && combo != languagePicker && combo != settingsLanguagePicker
            && combo.Items.Count > 0 && combo.Items.Cast<object>().All(item => item is string))
            staticComboItems.TryAdd(combo, combo.Items.Cast<string>().ToArray());
        if (root is DataGridView table)
        {
            table.DataBindingComplete -= LocalizeTableHeaders;
            table.DataBindingComplete += LocalizeTableHeaders;
            if (table != worldGrid && table != grid)
            {
                table.CellFormatting -= LocalizeStaticCell;
                table.CellFormatting += LocalizeStaticCell;
            }
            return;
        }
        foreach (Control child in root.Controls) RegisterStaticText(child);
    }

    private void LocalizeTableHeaders(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        if (sender is DataGridView table)
            foreach (DataGridViewColumn column in table.Columns)
            {
                column.HeaderText = T(column.Name == "PointId" ? "Point ID" : column.Name);
                if (table == worldGrid)
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    int width = column.Name switch { "Name" => 160, "Category" => 130, "Type" => 160, "X" or "Y" => 60, _ => 95 };
                    column.Width = Math.Max(width, TextRenderer.MeasureText(column.HeaderText, table.Font).Width + 28);
                }
            }
    }

    private void LocalizeStaticCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is string text) e.Value = T(text);
    }

    private void ApplyStaticLocalization()
    {
        foreach (var pair in staticUiText)
            if (!pair.Key.IsDisposed) pair.Key.Text = T(pair.Value);
        foreach (var pair in textBindings)
            if (!pair.Key.IsDisposed) pair.Key.Text = pair.Value();
        foreach (var pair in staticComboItems)
        {
            if (pair.Key.IsDisposed) continue;
            int selected = pair.Key.SelectedIndex;
            pair.Key.Items.Clear();
            pair.Key.Items.AddRange(pair.Value.Select(T).Cast<object>().ToArray());
            pair.Key.SelectedIndex = selected;
        }
        UpdateNavigationLabels();
        foreach (var table in Descendants(this).OfType<DataGridView>())
        {
            LocalizeTableHeaders(table, new DataGridViewBindingCompleteEventArgs(System.ComponentModel.ListChangedType.Reset));
            table.Invalidate();
        }
        foreach (var panel in Descendants(this).OfType<FlowLayoutPanel>().Where(item => Equals(item.Tag, "feature-grid")))
            ResizeFeatureCards(panel);
        foreach (var tabs in Descendants(this).OfType<ReferenceTabs>()) tabs.TranslateText = T;
        UpdatePageHeader();
        ApplyAppearance();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private void UnregisterText(Control root)
    {
        foreach (var control in Descendants(root).Prepend(root))
        {
            staticUiText.Remove(control);
            textBindings.Remove(control);
            if (control is ComboBox combo) staticComboItems.Remove(combo);
        }
    }

    private void UpdateNavigationLabels()
    {
        int index = 0;
        foreach (var entry in Navigation)
        {
            var button = navigationButtons[entry.Key];
            button.Text = $"{++index:00}   {T(entry.Title)}";
            button.AccessibleName = T(entry.Title);
        }
    }

    private void UpdatePageHeader()
    {
        var page = Navigation.Single(item => item.Key == activePage);
        header.Text = T(page.Title);
    }

    private static readonly (string Key, string Title)[] Navigation =
    [
        ("home", "Home"), ("automation", "Automation"), ("map", "Map & Data"),
        ("squads", "Squads & AFK"), ("hotkeys", "Hotkeys"), ("settings", "Settings"),
    ];
}
