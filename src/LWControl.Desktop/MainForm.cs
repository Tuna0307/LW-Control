using LWControl.Core;

namespace LWControl.Desktop;

public sealed class MainForm : Form
{
    private readonly string settingsPath;
    private UiLanguage language = UiText.DetectDefault();
    private readonly CheckBox enabled = new() { AutoSize = true };
    private readonly CheckBox expiry = new() { AutoSize = true };
    private readonly CheckBox chests = new() { AutoSize = true };
    private readonly NumericUpDown limit = new() { Minimum = 1, Maximum = 20, Width = 70 };
    private readonly CheckedListBox categories = new() { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly Label status = new() { AutoSize = true };
    private readonly Label header = new() { Dock = DockStyle.Fill, AutoSize = false };
    private readonly Label claimsPerRunLabel = new() { AutoSize = true };
    private readonly Label categoriesLabel = new() { AutoSize = true };
    private readonly Label languageLabel = new() { AutoSize = true };
    private readonly ComboBox languagePicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly DataGridView worldGrid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };
    private readonly ComboBox worldTypeFilter = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 170
    };
    private readonly TextBox worldSearch = new() { Width = 240 };
    private readonly Button locateWorldButton = new() { AutoSize = true, Enabled = false };
    private readonly Label worldCountLabel = new() { AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
    private readonly TextBox worldDetails = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly Button loadObservationsButton = new() { AutoSize = true };
    private readonly Button loadSampleButton = new() { AutoSize = true };
    private readonly Button buildPlanButton = new() { AutoSize = true };
    private readonly Button inspectBridgeButton = new() { AutoSize = true };
    private readonly Button claimDailyTasksButton = new() { AutoSize = true };
    private readonly Button worldScanButton = new() { AutoSize = true };
    private readonly Button saveSettingsButton = new() { AutoSize = true };
    private readonly Button export = new() { AutoSize = true, Enabled = false };
    private readonly Button startGameButton = new() { AutoSize = true };
    private readonly Button refreshButton = new() { AutoSize = true };
    private readonly Button regionChip = new() { AutoSize = true, Enabled = false };
    private readonly Button evidenceButton = new() { AutoSize = true, Enabled = false };
    private readonly Label runtimeSummaryLabel = new() { AutoSize = true };
    private readonly Panel pageHost = new() { Dock = DockStyle.Fill, Padding = new Padding(14) };
    private readonly Dictionary<string, Control> pages = [];
    private readonly Dictionary<string, Button> navigationButtons = [];
    private ClaimObservation[] observations = [];
    private ClaimPlan? plan;
    private CurrentWorldMapScanRecord[] worldRecords = [];
    private CurrentWorldMapScanRecord[] worldVisibleRecords = [];
    private string source = "none";
    private int maxAge = 60;

    public MainForm(string settingsPath)
    {
        this.settingsPath = settingsPath;
        MinimumSize = new Size(1050, 700);
        Size = new Size(1320, 840);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        Controls.Add(BuildApplicationShell());

        loadObservationsButton.Click += (_, _) => Guard(LoadObservations);
        loadSampleButton.Click += (_, _) => Guard(LoadSample);
        buildPlanButton.Click += (_, _) => Guard(BuildPlan);
        inspectBridgeButton.Click += (_, _) => Guard(InspectBridge);
        claimDailyTasksButton.Click += async (_, _) => await GuardAsync(ClaimDailyTasksAsync);
        saveSettingsButton.Click += (_, _) => Guard(SaveSettings);
        export.Click += (_, _) => Guard(ExportPlan);
        startGameButton.Click += async (_, _) => await GuardAsync(StartGameAsync);
        refreshButton.Click += (_, _) => Guard(() => RefreshRuntimeStatus(logResult: true));
        worldScanButton.Click += async (_, _) => await GuardAsync(RunWorldScanAsync);
        locateWorldButton.Click += async (_, _) => await GuardAsync(FocusSelectedWorldRecordAsync);

        languagePicker.Items.AddRange(["English", "简体中文"]);
        languagePicker.SelectedIndex = language == UiLanguage.SimplifiedChinese ? 1 : 0;
        languagePicker.SelectedIndexChanged += (_, _) =>
        {
            language = languagePicker.SelectedIndex == 1 ? UiLanguage.SimplifiedChinese : UiLanguage.English;
            ApplyLanguage();
            RefreshRuntimeStatus(logResult: false);
        };

        ApplyLanguage();
        var initial = new DailyClaimSettings();
        try
        {
            if (File.Exists(settingsPath)) initial = JsonFiles.Read<DailyClaimSettings>(settingsPath);
            initial.Validate();
        }
        catch (Exception ex)
        {
            initial = new();
            AddLog($"Settings could not be loaded; using disabled defaults. {ex.Message}");
        }
        ApplySettings(initial);
        enabled.CheckedChanged += (_, _) => InvalidatePlan();
        expiry.CheckedChanged += (_, _) => InvalidatePlan();
        chests.CheckedChanged += (_, _) => InvalidatePlan();
        limit.ValueChanged += (_, _) => InvalidatePlan();
        categories.ItemCheck += (_, _) => InvalidatePlan();
        worldTypeFilter.SelectedIndexChanged += (_, _) => ApplyWorldFilter();
        worldSearch.TextChanged += (_, _) => ApplyWorldFilter();
        worldGrid.SelectionChanged += (_, _) => UpdateWorldSelection();
        worldGrid.CellDoubleClick += async (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0) await GuardAsync(FocusSelectedWorldRecordAsync);
        };
        ShowPage("home");
        RefreshRuntimeStatus(logResult: false);
        AddLog(UiText.Get(language, "Ready"));
    }

    private Control BuildApplicationShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new(SizeType.Absolute, 62));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 30));
        root.RowStyles.Add(new(SizeType.Absolute, 112));
        root.Controls.Add(BuildTopBar(), 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        body.Controls.Add(BuildNavigation(), 0, 0);
        body.Controls.Add(pageHost, 1, 0);
        root.Controls.Add(body, 0, 1);

        status.Dock = DockStyle.Fill;
        status.Padding = new Padding(12, 5, 0, 0);
        root.Controls.Add(status, 0, 2);
        log.Margin = new Padding(12, 0, 12, 10);
        root.Controls.Add(log, 0, 3);

        pages["home"] = BuildHomePage();
        pages["map"] = BuildWorldMapPage();
        pages["squads"] = BuildFeatureListPage(ReferenceFeatureGroup.SquadsAfk);
        pages["automation"] = BuildAutomationPage();
        pages["hotkeys"] = BuildHotkeysPage();
        pages["settings"] = BuildSettingsPage();
        foreach (Control page in pages.Values)
        {
            page.Visible = false;
            page.Dock = DockStyle.Fill;
            pageHost.Controls.Add(page);
        }
        return root;
    }

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14, 8, 14, 6) };
        top.ColumnStyles.Add(new(SizeType.Percent, 100));
        top.ColumnStyles.Add(new(SizeType.AutoSize));
        header.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        top.Controls.Add(header, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.Add(startGameButton);
        actions.Controls.Add(refreshButton);
        actions.Controls.Add(regionChip);
        actions.Controls.Add(evidenceButton);
        actions.Controls.Add(languageLabel);
        actions.Controls.Add(languagePicker);
        top.Controls.Add(actions, 1, 0);
        return top;
    }

    private Control BuildNavigation()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 16, 8, 12),
            AutoScroll = true,
        };
        AddNavigationButton(panel, "home", "Home");
        AddNavigationButton(panel, "map", "Map & Data");
        AddNavigationButton(panel, "squads", "Squads & AFK");
        AddNavigationButton(panel, "automation", "Automation");
        AddNavigationButton(panel, "hotkeys", "Hotkeys");
        AddNavigationButton(panel, "settings", "Settings");
        return panel;
    }

    private void AddNavigationButton(FlowLayoutPanel panel, string key, string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 158,
            Height = 42,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 8),
        };
        button.Click += (_, _) => ShowPage(key);
        navigationButtons[key] = button;
        panel.Controls.Add(button);
    }

    private Control BuildHomePage()
    {
        int available = ReferenceFeatureCatalog.All.Count(item => item.State == FeatureImplementationState.Available);
        int partial = ReferenceFeatureCatalog.All.Count(item => item.State == FeatureImplementationState.Partial);
        int pending = ReferenceFeatureCatalog.All.Count - available - partial;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(18) };
        root.RowStyles.Add(new(SizeType.Absolute, 48));
        root.RowStyles.Add(new(SizeType.Absolute, 62));
        root.RowStyles.Add(new(SizeType.Absolute, 55));
        root.RowStyles.Add(new(SizeType.Absolute, 85));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "LW Control", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Reference UI recovered from LWControl.zip. Features remain visible while unfinished actions stay disabled.",
            AutoSize = true,
            MaximumSize = new Size(900, 0),
        }, 0, 1);
        runtimeSummaryLabel.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
        root.Controls.Add(runtimeSummaryLabel, 0, 2);
        root.Controls.Add(new Label
        {
            Text = $"Recovered feature catalog: 42 total · {available} available · {partial} partial · {pending} pending",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
        }, 0, 3);
        root.Controls.Add(new Label
        {
            Text = "World Scan is the currently available reference feature. Daily Free Claims is marked partial because only the recovered Daily Task path is live; its reference actions remain disabled until the full feature is recovered.",
            AutoSize = true,
            MaximumSize = new Size(900, 0),
        }, 0, 4);
        return root;
    }

    private Control BuildWorldMapPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
        root.RowStyles.Add(new(SizeType.Absolute, 60));
        root.RowStyles.Add(new(SizeType.Absolute, 44));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        var feature = ReferenceFeatureCatalog.InGroup(ReferenceFeatureGroup.MapData).Single();
        root.Controls.Add(new Label
        {
            Text = $"{feature.Name}\r\n{feature.Description}",
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            AutoSize = true,
        }, 0, 0);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        toolbar.Controls.Add(worldScanButton);
        toolbar.Controls.Add(new Label { Text = "Type", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
        toolbar.Controls.Add(worldTypeFilter);
        toolbar.Controls.Add(new Label { Text = "Search", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
        toolbar.Controls.Add(worldSearch);
        toolbar.Controls.Add(locateWorldButton);
        toolbar.Controls.Add(worldCountLabel);
        root.Controls.Add(toolbar, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        split.SizeChanged += (_, _) =>
        {
            int available = split.ClientSize.Width - split.SplitterWidth;
            if (available <= 10) return;
            int desired = (int)(available * 0.72);
            split.SplitterDistance = Math.Clamp(desired, 1, available - 1);
        };
        split.Panel1.Controls.Add(worldGrid);
        split.Panel2.Controls.Add(worldDetails);
        root.Controls.Add(split, 0, 2);
        return root;
    }

    private Control BuildFeatureListPage(ReferenceFeatureGroup group)
    {
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8),
        };
        foreach (ReferenceFeature feature in ReferenceFeatureCatalog.InGroup(group))
            scroll.Controls.Add(BuildFeatureCard(feature));
        scroll.SizeChanged += (_, _) => ResizeFeatureCards(scroll);
        return scroll;
    }

    private Control BuildFeatureCard(ReferenceFeature feature)
    {
        var card = new TableLayoutPanel
        {
            Width = 900,
            Height = Math.Max(132, 104 + ((feature.Actions.Count + 4) / 5) * 38),
            ColumnCount = 1,
            RowCount = 3,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12),
            Margin = new Padding(4, 4, 4, 10),
            Tag = "feature-card",
        };
        card.RowStyles.Add(new(SizeType.Absolute, 28));
        card.RowStyles.Add(new(SizeType.Absolute, 42));
        card.RowStyles.Add(new(SizeType.Percent, 100));
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        heading.Controls.Add(new Label { Text = feature.Name, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold) });
        heading.Controls.Add(new Label
        {
            Text = feature.State switch
            {
                FeatureImplementationState.Available => "AVAILABLE",
                FeatureImplementationState.Partial => "PARTIAL",
                _ => "PENDING",
            },
            AutoSize = true,
            Padding = new Padding(10, 2, 0, 0),
        });
        card.Controls.Add(heading, 0, 0);
        card.Controls.Add(new Label { Text = feature.Description, AutoSize = true, MaximumSize = new Size(850, 38) }, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        foreach (string action in feature.Actions)
            actions.Controls.Add(new Button { Text = action, AutoSize = true, Enabled = false });
        card.Controls.Add(actions, 0, 2);
        return card;
    }

    private static void ResizeFeatureCards(FlowLayoutPanel panel)
    {
        int width = Math.Max(520, panel.ClientSize.Width - 36);
        foreach (Control control in panel.Controls)
            if (Equals(control.Tag, "feature-card")) control.Width = width;
    }

    private Control BuildAutomationPage()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var daily = new TabPage("Daily");
        var events = new TabPage("Event");
        var alliance = new TabPage("Alliance");
        daily.Controls.Add(BuildFeatureListPage(ReferenceFeatureGroup.AutomationDaily));
        events.Controls.Add(BuildFeatureListPage(ReferenceFeatureGroup.AutomationEvent));
        alliance.Controls.Add(BuildFeatureListPage(ReferenceFeatureGroup.AutomationAlliance));
        tabs.TabPages.Add(daily);
        tabs.TabPages.Add(events);
        tabs.TabPages.Add(alliance);
        return tabs;
    }

    private Control BuildHotkeysPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(18) };
        root.RowStyles.Add(new(SizeType.Absolute, 48));
        root.RowStyles.Add(new(SizeType.Absolute, 48));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "Hotkeys", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), AutoSize = true }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Recovered bindings from the reference feature actions are shown below. The actions remain disabled until their feature is implemented.",
            AutoSize = true,
        }, 0, 1);
        var table = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            DataSource = new[]
            {
                new { Key = "Q / W / E / R", Action = "Attack Targets", Detail = "Send squads 1 through 4 to the target under the pointer.", Gate = "Foreground game + world target", State = "Pending" },
                new { Key = "A / S / D / F", Action = "Recall Squads", Detail = "Recall squads 1 through 4 to the base.", Gate = "Foreground game + active march", State = "Pending" },
                new { Key = "Space", Action = "Shield Countdown", Detail = "Hold Space to show remaining shield time over protected cities.", Gate = "Foreground game + shielded city", State = "Pending" },
                new { Key = "F6 / F7 / F8", Action = "Use Shield", Detail = "Use 8-hour, 12-hour, or 24-hour shield items.", Gate = "Foreground game + matching shield item", State = "Pending" },
                new { Key = "Alt + 1 / 2 / 3 / 4", Action = "Equipment Schemes", Detail = "Apply equipment schemes 1 through 4 to configured squads.", Gate = "Foreground game + saved scheme", State = "Pending" },
                new { Key = "F9", Action = "Random Teleport", Detail = "Use a random teleport item and wait for position-change evidence.", Gate = "Foreground game + home ready", State = "Pending" },
            },
        };
        root.Controls.Add(table, 0, 2);
        return root;
    }

    private Control BuildSettingsPage()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var referenceTab = new TabPage("Reference Settings");
        referenceTab.Controls.Add(BuildReferenceSettingsPage());
        var toolsTab = new TabPage("Recovered Tools");
        toolsTab.Controls.Add(BuildRecoveredToolsPage());
        tabs.TabPages.Add(referenceTab);
        tabs.TabPages.Add(toolsTab);
        return tabs;
    }

    private Control BuildReferenceSettingsPage()
    {
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };
        scroll.Controls.Add(BuildSettingsSection("DISPLAY", "Display & Behavior",
        [
            SettingsRow("Windows DPI scaling", $"Current monitor · {(int)Math.Round(DeviceDpi / 96d * 100)}%", null),
            SettingsRow("Hide in background", "The reference hides its menu whenever LastWar is not foreground.", new CheckBox { Checked = true, Enabled = false, AutoSize = true }),
        ]));

        var languageChoice = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        languageChoice.Items.AddRange(["Chinese (Simplified)", "English"]);
        languageChoice.SelectedIndex = language == UiLanguage.SimplifiedChinese ? 0 : 1;
        languageChoice.SelectedIndexChanged += (_, _) =>
            languagePicker.SelectedIndex = languageChoice.SelectedIndex == 0 ? 1 : 0;
        var accent = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        foreach (string name in new[] { "Cyan", "Gold", "Indigo", "Rose", "Emerald" })
            accent.Controls.Add(new Button { Text = name, Enabled = false, AutoSize = true });
        scroll.Controls.Add(BuildSettingsSection("APPEARANCE", "Appearance & Language",
        [
            SettingsRow("Interface language", "Chinese / English", languageChoice),
            SettingsRow("Accent color", "Reference choices recovered; styling switch is not implemented yet.", accent),
        ]));

        var menuToggle = new ComboBox { Width = 160, Enabled = false, DropDownStyle = ComboBoxStyle.DropDownList };
        menuToggle.Items.Add("Right mouse");
        menuToggle.SelectedIndex = 0;
        var hotkeyPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(680, 0) };
        foreach (string key in new[] { "Q", "W", "E", "R", "A", "S", "D", "F", "Space", "F6", "F7", "F8", "Alt+1", "Alt+2", "Alt+3", "Alt+4", "F9" })
            hotkeyPanel.Controls.Add(new CheckBox { Text = key, Checked = key != "F9", Enabled = false, AutoSize = true });
        scroll.Controls.Add(BuildSettingsSection("HOTKEYS", "Hotkeys & Foreground Gate",
        [
            SettingsRow("Menu toggle", "Right mouse while LastWar or the menu is foreground.", menuToggle),
            SettingsRow("Gameplay hotkeys", "Recovered reference bindings; actions remain disabled until their features are implemented.", hotkeyPanel),
            SettingsRow("Save gameplay hotkeys", "Reference action placeholder.", new Button { Text = "Save gameplay hotkeys", Enabled = false, AutoSize = true }),
        ]));

        var refresh = new Button { Text = "Refresh runtime state", AutoSize = true };
        refresh.Click += (_, _) => Guard(() => RefreshRuntimeStatus(logResult: true));
        scroll.Controls.Add(BuildSettingsSection("SYSTEM", "System & Diagnostics",
        [
            SettingsRow("Export runtime logs", "Reference action placeholder.", new Button { Text = "Export runtime logs", Enabled = false, AutoSize = true }),
            SettingsRow("Refresh runtime state", "Refresh the current game and persistent-runtime status.", refresh),
        ]));
        scroll.SizeChanged += (_, _) =>
        {
            int width = Math.Max(560, scroll.ClientSize.Width - 40);
            foreach (Control control in scroll.Controls)
                if (Equals(control.Tag, "settings-section")) control.Width = width;
        };
        return scroll;
    }

    private Control BuildRecoveredToolsPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        root.RowStyles.Add(new(SizeType.Absolute, 44));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        toolbar.Controls.Add(loadObservationsButton);
        toolbar.Controls.Add(loadSampleButton);
        toolbar.Controls.Add(buildPlanButton);
        toolbar.Controls.Add(inspectBridgeButton);
        toolbar.Controls.Add(claimDailyTasksButton);
        toolbar.Controls.Add(saveSettingsButton);
        toolbar.Controls.Add(export);
        root.Controls.Add(toolbar, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        body.ColumnStyles.Add(new(SizeType.Absolute, 285));
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, Padding = new Padding(0, 0, 12, 0) };
        settings.RowStyles.Add(new(SizeType.Absolute, 34));
        settings.RowStyles.Add(new(SizeType.Absolute, 34));
        settings.RowStyles.Add(new(SizeType.Absolute, 34));
        settings.RowStyles.Add(new(SizeType.Absolute, 42));
        settings.RowStyles.Add(new(SizeType.Absolute, 30));
        settings.RowStyles.Add(new(SizeType.Percent, 100));
        settings.RowStyles.Add(new(SizeType.Absolute, 45));
        settings.Controls.Add(enabled, 0, 0);
        settings.Controls.Add(expiry, 0, 1);
        settings.Controls.Add(chests, 0, 2);
        var limits = new FlowLayoutPanel { Dock = DockStyle.Fill };
        limits.Controls.Add(claimsPerRunLabel);
        limits.Controls.Add(limit);
        settings.Controls.Add(limits, 0, 3);
        settings.Controls.Add(categoriesLabel, 0, 4);
        RebuildCategoryItems(new HashSet<ClaimKind>());
        settings.Controls.Add(categories, 0, 5);
        settings.Controls.Add(new Label
        {
            Text = "Recovered Daily Task Claim is live here. Full reference Daily Free Claims remains partial.",
            AutoSize = true,
            MaximumSize = new Size(270, 0),
        }, 0, 6);
        body.Controls.Add(settings, 0, 0);
        body.Controls.Add(grid, 1, 0);
        root.Controls.Add(body, 0, 1);
        return root;
    }

    private Control BuildSettingsSection(string eyebrow, string title, IReadOnlyList<Control> rows)
    {
        var section = new TableLayoutPanel
        {
            Width = 900,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = rows.Count + 1,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12),
            Margin = new Padding(4, 4, 4, 12),
            Tag = "settings-section",
        };
        section.Controls.Add(new Label
        {
            Text = $"{eyebrow}\r\n{title}",
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        }, 0, 0);
        for (int i = 0; i < rows.Count; i++) section.Controls.Add(rows[i], 0, i + 1);
        return section;
    }

    private static Control SettingsRow(string title, string description, Control? action)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 8) };
        row.ColumnStyles.Add(new(SizeType.Percent, 100));
        row.ColumnStyles.Add(new(SizeType.AutoSize));
        var copy = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 2, ColumnCount = 1 };
        copy.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true }, 0, 0);
        copy.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(630, 0) }, 0, 1);
        row.Controls.Add(copy, 0, 0);
        if (action is not null)
        {
            action.Anchor = AnchorStyles.Right;
            row.Controls.Add(action, 1, 0);
        }
        return row;
    }

    private void ShowPage(string key)
    {
        if (!pages.TryGetValue(key, out Control? selected)) return;
        foreach (var pair in pages) pair.Value.Visible = pair.Key == key;
        selected.BringToFront();
        foreach (var pair in navigationButtons)
            pair.Value.Font = new Font(Font.FontFamily, 10, pair.Key == key ? FontStyle.Bold : FontStyle.Regular);
    }

    private void ApplySettings(DailyClaimSettings value)
    {
        enabled.Checked = value.Enabled;
        expiry.Checked = value.PreferExpiringRewards;
        chests.Checked = value.PreferTaskChests;
        limit.Value = value.MaximumClaimsPerRun;
        maxAge = value.MaxSnapshotAgeSeconds;
        for (int i = 0; i < categories.Items.Count; i++)
            categories.SetItemChecked(i, value.EnabledKinds.Contains(((ClaimKindOption)categories.Items[i]).Kind));
    }

    private DailyClaimSettings CurrentSettings() => new()
    {
        Enabled = enabled.Checked, MaximumClaimsPerRun = (int)limit.Value,
        MaxSnapshotAgeSeconds = maxAge, PreferExpiringRewards = expiry.Checked,
        PreferTaskChests = chests.Checked,
        EnabledKinds = categories.CheckedItems.Cast<ClaimKindOption>().Select(item => item.Kind).ToHashSet()
    };

    private void LoadObservations()
    {
        using var dialog = new OpenFileDialog { Filter = UiText.Get(language, "ObservationJson"), CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var loaded = JsonFiles.Read<ClaimObservation[]>(dialog.FileName);
        _ = DailyClaimPlanner.Build(CurrentSettings(), loaded, DateTimeOffset.UtcNow);
        observations = loaded;
        source = UiText.Get(language, "Imported");
        InvalidatePlan();
        AddLog($"Loaded {observations.Length} observations. Authenticity has not been verified.");
    }

    private void LoadSample()
    {
        var now = DateTimeOffset.UtcNow;
        var example = new ClaimObservation
        {
            SourceKey = "sample-vip", RewardName = "Sample VIP reward", Kind = ClaimKind.VipDailyReward,
            CapturedAt = now, Status = ClaimSourceStatus.Claimable, FreeConfirmed = true,
            CurrencyCost = 0, RemainingFreeClaims = 1, ClaimButtonVisible = true, ClaimButtonSemantic = "free"
        };
        observations = [example, example with
        {
            SourceKey = "sample-unknown", RewardName = "Sample unknown cost",
            CurrencyCost = null, RemainingFreeClaims = null, FreeConfirmed = null, ClaimButtonSemantic = null
        }];
        source = UiText.Get(language, "SampleData");
        InvalidatePlan();
        AddLog("Loaded sample observations. Enable planning and select VIP daily rewards to preview.");
    }

    private void BuildPlan()
    {
        if (observations.Length == 0) throw new InvalidOperationException("Load observations first.");
        plan = DailyClaimPlanner.Build(CurrentSettings(), observations, DateTimeOffset.UtcNow);
        grid.DataSource = plan.Decisions.Select(d => new
        {
            Reward = d.Observation.RewardName, Category = d.Observation.Kind,
            Decision = d.Selected ? UiText.Get(language, "Selected") : UiText.Get(language, "Skipped"), d.Reason
        }).ToArray();
        status.Text = $"{source} · {plan.Decisions.Count(d => d.Selected)} {UiText.Get(language, "Selected")} · 0 {UiText.Get(language, "ActionsSent")}";
        ApplyGridHeaders();
        export.Enabled = true;
        AddLog("Plan built. No reward has been claimed.");
    }

    private void ExportPlan()
    {
        if (plan is null) return;
        if (DateTimeOffset.UtcNow - plan.CreatedAt > TimeSpan.FromSeconds(maxAge))
        {
            InvalidatePlan();
            throw new InvalidOperationException("Plan is old. Build a fresh plan before exporting.");
        }
        using var dialog = new SaveFileDialog { Filter = UiText.Get(language, "Json"), FileName = "claim-plan.json", OverwritePrompt = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            JsonFiles.Write(dialog.FileName, plan);
            AddLog("Exported preview plan.");
        }
    }

    private void SaveSettings()
    {
        var value = CurrentSettings();
        value.Validate();
        JsonFiles.Write(settingsPath, value);
        AddLog("Settings saved.");
    }

    private void InspectBridge()
    {
        var inspection = LocalBridgeInspector.Inspect();
        string heartbeat = inspection.HeartbeatAt?.ToString("u") ?? "missing";
        string age = inspection.HeartbeatAgeSeconds.HasValue
            ? $"{inspection.HeartbeatAgeSeconds.Value:F1}s old" : "age unknown";
        string version = inspection.DailyFreeClaimsVersion ?? "missing";
        AddLog($"Bridge inspection: {inspection.StatusCode}; game={inspection.GameRunning}; " +
            $"heartbeat={heartbeat} ({age}); daily-free={version}; pending={inspection.PendingCommandCount}. " +
            "Read-only inspection sent no command.");
    }

    private async Task StartGameAsync()
    {
        startGameButton.Enabled = false;
        try
        {
            status.Text = "Start Game · checking persistent runtime";
            AddLog("Start Game requested. The persistent runtime is verified or installed while Last War is closed, then the official game is launched.");
            var client = new LastWarStartupClient();
            var result = await client.StartAsync(PostLog);
            status.Text = "Last War · running · World Scan runtime ready";
            AddLog($"Start Game ready: game already running={result.GameWasAlreadyRunning}; runtime changed={result.RuntimeChanged}; "
                + $"world={result.WorldScanRuntime.StatusCode}; daily={result.DailyTaskRuntime.StatusCode}; launch={result.LaunchPath}");
            RefreshRuntimeStatus(logResult: false);
        }
        finally
        {
            startGameButton.Enabled = true;
        }
    }

    private void RefreshRuntimeStatus(bool logResult)
    {
        var inspection = new LastWarStartupClient().Inspect();
        string game = inspection.GameRunning ? "running" : "stopped";
        string world = inspection.World.StatusCode;
        string daily = inspection.Daily.StatusCode;
        runtimeSummaryLabel.Text = $"Game: {game} · World Scan runtime: {world} · Daily Task runtime: {daily}";
        status.Text = $"Last War · {game} · World {world} · Daily {daily}";
        if (logResult)
            AddLog($"Runtime refresh: game={game}; world={world}; daily={daily}; "
                + $"world version={inspection.World.RuntimeVersion ?? "missing"}; daily version={inspection.Daily.RuntimeVersion ?? "missing"}.");
    }

    private void PostLog(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => AddLog(message));
            return;
        }
        AddLog(message);
    }

    private async Task ClaimDailyTasksAsync()
    {
        var settings = CurrentSettings();
        settings.Validate();
        if (!settings.Enabled)
            throw new InvalidOperationException("Enable daily claims before running Daily Task Claim.");
        if (!settings.EnabledKinds.Contains(ClaimKind.DailyTaskChest))
            throw new InvalidOperationException("Enable the Daily Task Chest category before running Daily Task Claim.");

        claimDailyTasksButton.Enabled = false;
        try
        {
            var client = new CurrentDailyTaskRuntimeClient();
            var inspection = client.Inspect();
            if (inspection.StatusCode != "ready")
                throw new InvalidOperationException($"Daily Task runtime is not ready ({inspection.StatusCode}).");
            AddLog($"Daily Task run started; maximum {(int)limit.Value} confirmed claims.");
            status.Text = $"Daily Task · running · 0 {UiText.Get(language, "ActionsSent")}";
            var result = await client.RunOnceAsync((int)limit.Value);
            status.Text = $"Daily Task · {result.State} · {result.ConfirmedClaims} confirmed";
            AddLog($"Daily Task run {result.State}: {result.ConfirmedClaims} confirmed claims, " +
                $"{result.RewardSendCount} reward sends, {result.RefreshSendCount} state refreshes. {result.Message}");
            if (result.State != "completed")
                throw new InvalidOperationException($"Daily Task run ended in {result.State}: {result.Message}");
        }
        finally
        {
            claimDailyTasksButton.Enabled = true;
        }
    }

    private async Task RunWorldScanAsync()
    {
        worldScanButton.Enabled = false;
        try
        {
            AddLog("World Scan started through the persistent in-game runtime. Last War stays open during and after the scan.");
            status.Text = "World Scan · running";
            var client = new CurrentWorldMapScanClient();
            var run = await client.RunAsync();
            var result = run.Result;
            worldRecords = result.PointRecords.ToArray();
            ApplyWorldFilter();
            int players = result.PointRecords.Count(record => record.Kind == "player_base");
            int resources = result.PointRecords.Count(record => record.Kind == "resource_point");
            int monsters = result.PointRecords.Count(record => record.Kind == "monster");
            int allianceBuildings = result.PointRecords.Count(record => record.Kind == "alliance_building");
            status.Text = $"World Scan · {result.AccumulatedRecordCount:N0} records · 10,000/10,000 blocks";
            AddLog($"World Scan proven: {result.AccumulatedRecordCount:N0} records; "
                + $"players={players:N0}, resources={resources:N0}, monsters={monsters:N0}, alliance buildings={allianceBuildings:N0}. "
                + $"camera moves={result.CameraMoveCount:N0}; runtime={run.RestoreMode}; game left running={run.GameLeftRunning}. Evidence: {run.LiveResultPath}");
            if (!run.GameLeftRunning)
                AddLog("Last War exited during the scan unexpectedly. The persistent scanner did not request a game shutdown.");
        }
        finally
        {
            worldScanButton.Enabled = true;
        }
    }

    private void RebuildWorldTypeFilter()
    {
        string? selectedKind = (worldTypeFilter.SelectedItem as WorldTypeOption)?.Kind;
        worldTypeFilter.Items.Clear();
        worldTypeFilter.Items.Add(new WorldTypeOption(null,
            language == UiLanguage.SimplifiedChinese ? "全部目标" : "All targets"));
        worldTypeFilter.Items.Add(new WorldTypeOption("player_base",
            language == UiLanguage.SimplifiedChinese ? "玩家基地" : "Player bases"));
        worldTypeFilter.Items.Add(new WorldTypeOption("resource_point",
            language == UiLanguage.SimplifiedChinese ? "资源点" : "Resources"));
        worldTypeFilter.Items.Add(new WorldTypeOption("monster",
            language == UiLanguage.SimplifiedChinese ? "怪物 / Boss" : "Monsters / Bosses"));
        worldTypeFilter.Items.Add(new WorldTypeOption("alliance_building",
            language == UiLanguage.SimplifiedChinese ? "联盟建筑" : "Alliance buildings"));
        worldTypeFilter.Items.Add(new WorldTypeOption("world_point",
            language == UiLanguage.SimplifiedChinese ? "特殊 / 雷达 / 活动" : "Special / Radar / Event"));
        int selected = worldTypeFilter.Items.Cast<WorldTypeOption>()
            .ToList().FindIndex(item => item.Kind == selectedKind);
        worldTypeFilter.SelectedIndex = selected >= 0 ? selected : 0;
    }

    private void ApplyWorldFilter()
    {
        string? kind = (worldTypeFilter.SelectedItem as WorldTypeOption)?.Kind;
        string query = worldSearch.Text.Trim();
        worldVisibleRecords = worldRecords.Where(record =>
        {
            if (kind is not null && record.Kind != kind) return false;
            if (query.Length == 0) return true;
            string searchable = string.Join(" ", new[]
            {
                record.DisplayName, record.PlayerName, record.Alliance, record.PlayerId,
                record.ResourceType, record.ResourceTypeId, record.MonsterId,
                record.MonsterSpecialType, record.PointType?.ToString(), record.PointId.ToString(),
                record.X.ToString(), record.Y.ToString(), CurrentPointTypeName(record.PointType)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return searchable.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        worldGrid.DataSource = worldVisibleRecords.Select(record => new
        {
            Category = WorldKindName(record.Kind),
            Type = CurrentPointTypeName(record.PointType),
            Name = record.DisplayName,
            Level = record.Level,
            Power = record.Kind == "monster" ? record.RecommendedPower : record.Power,
            Alliance = record.Alliance ?? "",
            Resource = record.ResourceType ?? record.ResourceTypeId ?? "",
            X = record.X,
            Y = record.Y,
            Server = record.ServerId,
            PointId = record.PointId,
        }).ToArray();
        worldCountLabel.Text = language == UiLanguage.SimplifiedChinese
            ? $"显示 {worldVisibleRecords.Length:N0} / {worldRecords.Length:N0}"
            : $"Showing {worldVisibleRecords.Length:N0} / {worldRecords.Length:N0}";
        if (worldVisibleRecords.Length > 0)
        {
            worldGrid.ClearSelection();
            worldGrid.Rows[0].Selected = true;
            worldGrid.CurrentCell = worldGrid.Rows[0].Cells[0];
        }
        else
        {
            locateWorldButton.Enabled = false;
            worldDetails.Clear();
        }
        UpdateWorldSelection();
    }

    private void UpdateWorldSelection()
    {
        int rowIndex = worldGrid.CurrentCell?.RowIndex ?? -1;
        if (rowIndex < 0 || rowIndex >= worldVisibleRecords.Length)
        {
            locateWorldButton.Enabled = false;
            worldDetails.Clear();
            return;
        }
        var record = worldVisibleRecords[rowIndex];
        locateWorldButton.Enabled = true;
        string shield = record.Shield is null || !record.Shield.Known
            ? "unknown" : record.Shield.Active ? "protected" : "unprotected";
        worldDetails.Text = string.Join(Environment.NewLine,
        [
            record.DisplayName,
            $"Category: {WorldKindName(record.Kind)}",
            $"Point type: {CurrentPointTypeName(record.PointType)}",
            $"Coordinate: X {record.X} / Y {record.Y}",
            $"Server: {record.ServerId}",
            $"Point ID: {record.PointId}",
            $"UUID: {record.Uuid ?? "--"}",
            $"Player: {record.PlayerName ?? record.PlayerId ?? "--"}",
            $"Alliance: {record.Alliance ?? record.AllianceId ?? "--"}",
            $"Level: {record.Level?.ToString() ?? "--"}",
            $"Power: {record.Power?.ToString("N0") ?? "--"}",
            $"Shield: {shield}",
            $"Resource: {record.ResourceType ?? record.ResourceTypeId ?? "--"}",
            $"Monster ID: {record.MonsterId ?? "--"}",
            $"Recommended power: {record.RecommendedPower?.ToString("N0") ?? "--"}",
            $"Source: {record.Source}",
        ]);
    }

    private async Task FocusSelectedWorldRecordAsync()
    {
        int rowIndex = worldGrid.CurrentCell?.RowIndex ?? -1;
        if (rowIndex < 0 || rowIndex >= worldVisibleRecords.Length)
            throw new InvalidOperationException("Select a World Scan target first.");
        var record = worldVisibleRecords[rowIndex];
        locateWorldButton.Enabled = false;
        try
        {
            status.Text = $"World Map · locating X {record.X} Y {record.Y}";
            var result = await new CurrentWorldMapFocusClient().FocusAsync(record);
            status.Text = $"World Map · located X {result.X} Y {result.Y}";
            AddLog($"Located {record.DisplayName} at X {result.X} Y {result.Y}; verified camera X {result.ObservedX:F0} Y {result.ObservedY:F0} via {result.Route}.");
        }
        finally
        {
            locateWorldButton.Enabled = true;
        }
    }

    private string WorldKindName(string kind) => kind switch
    {
        "player_base" => language == UiLanguage.SimplifiedChinese ? "玩家基地" : "Player Base",
        "resource_point" => language == UiLanguage.SimplifiedChinese ? "资源点" : "Resource",
        "monster" => language == UiLanguage.SimplifiedChinese ? "怪物 / Boss" : "Monster / Boss",
        "alliance_building" => language == UiLanguage.SimplifiedChinese ? "联盟建筑" : "Alliance Building",
        "world_point" => language == UiLanguage.SimplifiedChinese ? "特殊 / 雷达 / 活动" : "Special / Radar / Event",
        _ => kind,
    };

    private static string CurrentPointTypeName(int? pointType) => pointType switch
    {
        null => "--",
        4 => "4 · WorldMonster",
        5 => "5 · WorldBoss",
        12 => "12 · MONSTER_REWARD",
        14 => "14 · DETECT_EVENT_PVE",
        17 => "17 · HERO_DISPATCH",
        21 => "21 · TREASURE",
        22 => "22 · INVASION_WORLD_MONSTER",
        28 => "28 · RadarSeasonSnowSurvivor",
        29 => "29 · GHOSTRECON_POINT",
        33 => "33 · RADAR_DOMINATOR_GUIDE",
        34 => "34 · RADAR_DOMINATOR_CURE",
        39 => "39 · METEORITE_POINT",
        42 => "42 · MONSETER_CHALLENGE_NEW_TREASURE",
        43 => "43 · ACTIVITY_WORLD_TREASURE",
        44 => "44 · DETECT_RETRY_TASK",
        45 => "45 · DETECT_DIG_GAME",
        46 => "46 · TreasureChest",
        47 => "47 · RADAR_DOMINATOR_COCKATRICE_UNLOCK_1",
        48 => "48 · RADAR_DOMINATOR_COCKATRICE_UNLOCK_2",
        49 => "49 · DETECT_SUPPLIES_SEARCH",
        50 => "50 · DETECT_ALLIANCE_CITY_SCOUT_MONSTER",
        54 => "54 · DETECT_LAST_STAND",
        59 => "59 · ALLIANCE_BOSS_S0",
        1001 => "1001 · SiegeTreasure",
        1003 => "1003 · SIMPLE_WORLD_MONSTER",
        _ => pointType.Value.ToString(),
    };

    private void InvalidatePlan()
    {
        plan = null;
        grid.DataSource = null;
        export.Enabled = false;
        status.Text = $"{source} · {observations.Length} · {UiText.Get(language, "BuildReview")}";
    }

    private void AddLog(string message) => log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { AddLog(ex.Message); MessageBox.Show(this, ex.Message, UiText.Get(language, "WarningTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            AddLog(ex.Message);
            MessageBox.Show(this, ex.Message, UiText.Get(language, "WarningTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void ApplyLanguage()
    {
        var checkedKinds = categories.CheckedItems.Cast<ClaimKindOption>().Select(item => item.Kind).ToHashSet();
        Text = UiText.Get(language, "Title");
        header.Text = UiText.Get(language, "Header");
        startGameButton.Text = language == UiLanguage.SimplifiedChinese ? "启动游戏" : "Start game";
        refreshButton.Text = language == UiLanguage.SimplifiedChinese ? "刷新" : "Refresh";
        regionChip.Text = language == UiLanguage.SimplifiedChinese ? "区域 --" : "Region --";
        evidenceButton.Text = language == UiLanguage.SimplifiedChinese ? "证据" : "Evidence";
        if (navigationButtons.Count > 0)
        {
            navigationButtons["home"].Text = language == UiLanguage.SimplifiedChinese ? "主页" : "Home";
            navigationButtons["map"].Text = language == UiLanguage.SimplifiedChinese ? "地图与数据" : "Map & Data";
            navigationButtons["squads"].Text = language == UiLanguage.SimplifiedChinese ? "队伍与挂机" : "Squads & AFK";
            navigationButtons["automation"].Text = language == UiLanguage.SimplifiedChinese ? "自动化" : "Automation";
            navigationButtons["hotkeys"].Text = language == UiLanguage.SimplifiedChinese ? "快捷键" : "Hotkeys";
            navigationButtons["settings"].Text = language == UiLanguage.SimplifiedChinese ? "设置" : "Settings";
        }
        enabled.Text = UiText.Get(language, "Enable");
        expiry.Text = UiText.Get(language, "Expiry");
        chests.Text = UiText.Get(language, "Chests");
        claimsPerRunLabel.Text = UiText.Get(language, "ClaimsPerRun");
        categoriesLabel.Text = UiText.Get(language, "Categories");
        languageLabel.Text = UiText.Get(language, "Language");
        loadObservationsButton.Text = UiText.Get(language, "LoadObservations");
        loadSampleButton.Text = UiText.Get(language, "LoadSample");
        buildPlanButton.Text = UiText.Get(language, "BuildPlan");
        inspectBridgeButton.Text = UiText.Get(language, "InspectBridge");
        claimDailyTasksButton.Text = UiText.Get(language, "ClaimDailyTasks");
        worldScanButton.Text = UiText.Get(language, "WorldScan");
        locateWorldButton.Text = language == UiLanguage.SimplifiedChinese ? "在游戏中定位" : "Locate in Game";
        worldSearch.PlaceholderText = language == UiLanguage.SimplifiedChinese
            ? "名称、联盟、ID、坐标…" : "Name, alliance, ID, coordinate…";
        saveSettingsButton.Text = UiText.Get(language, "SaveSettings");
        export.Text = UiText.Get(language, "ExportPlan");
        RebuildCategoryItems(checkedKinds);
        RebuildWorldTypeFilter();
        if (observations.Length > 0) InvalidatePlan();
        ApplyGridHeaders();
        if (worldRecords.Length > 0) ApplyWorldFilter();
    }

    private void RebuildCategoryItems(IReadOnlySet<ClaimKind> checkedKinds)
    {
        categories.Items.Clear();
        foreach (var kind in Enum.GetValues<ClaimKind>())
        {
            int index = categories.Items.Add(new ClaimKindOption(kind, UiText.ClaimKindName(language, kind)));
            if (checkedKinds.Contains(kind)) categories.SetItemChecked(index, true);
        }
    }

    private void ApplyGridHeaders()
    {
        if (grid.Columns.Count < 4 || !grid.Columns.Contains("Reward")) return;
        if (language == UiLanguage.SimplifiedChinese)
        {
            grid.Columns["Reward"]!.HeaderText = "奖励";
            grid.Columns["Category"]!.HeaderText = "类别";
            grid.Columns["Decision"]!.HeaderText = "决策";
            grid.Columns["Reason"]!.HeaderText = "原因";
        }
        else
        {
            grid.Columns["Reward"]!.HeaderText = "Reward";
            grid.Columns["Category"]!.HeaderText = "Category";
            grid.Columns["Decision"]!.HeaderText = "Decision";
            grid.Columns["Reason"]!.HeaderText = "Reason";
        }
    }

    public void RunSmokeCheck()
    {
        LoadSample();
        enabled.Checked = true;
        int vipIndex = categories.Items.Cast<ClaimKindOption>().ToList().FindIndex(item => item.Kind == ClaimKind.VipDailyReward);
        categories.SetItemChecked(vipIndex, true);
        BuildPlan();
        if (plan?.Decisions.Count(d => d.Selected) != 1) throw new InvalidOperationException("UI plan smoke check failed.");
        SaveSettings();
        if (!JsonFiles.Read<DailyClaimSettings>(settingsPath).Enabled) throw new InvalidOperationException("UI settings smoke check failed.");
    }
}

internal sealed record WorldTypeOption(string? Kind, string Text)
{
    public override string ToString() => Text;
}
