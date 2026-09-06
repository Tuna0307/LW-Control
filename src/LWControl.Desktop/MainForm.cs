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
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly Button loadObservationsButton = new() { AutoSize = true };
    private readonly Button loadSampleButton = new() { AutoSize = true };
    private readonly Button buildPlanButton = new() { AutoSize = true };
    private readonly Button inspectBridgeButton = new() { AutoSize = true };
    private readonly Button claimDailyTasksButton = new() { AutoSize = true };
    private readonly Button saveSettingsButton = new() { AutoSize = true };
    private readonly Button export = new() { AutoSize = true, Enabled = false };
    private ClaimObservation[] observations = [];
    private ClaimPlan? plan;
    private string source = "none";
    private int maxAge = 60;

    public MainForm(string settingsPath)
    {
        this.settingsPath = settingsPath;
        MinimumSize = new Size(950, 650);
        Size = new Size(1120, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new(SizeType.Absolute, 62));
        root.RowStyles.Add(new(SizeType.Absolute, 40));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 35));
        root.RowStyles.Add(new(SizeType.Absolute, 100));
        root.Controls.Add(header, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        AddButton(actions, loadObservationsButton, LoadObservations);
        AddButton(actions, loadSampleButton, LoadSample);
        AddButton(actions, buildPlanButton, BuildPlan);
        AddButton(actions, inspectBridgeButton, InspectBridge);
        claimDailyTasksButton.Click += async (_, _) => await GuardAsync(ClaimDailyTasksAsync);
        actions.Controls.Add(claimDailyTasksButton);
        AddButton(actions, saveSettingsButton, SaveSettings);
        export.Click += (_, _) => Guard(ExportPlan);
        actions.Controls.Add(export);
        actions.Controls.Add(languageLabel);
        languagePicker.Items.AddRange(["English", "简体中文"]);
        languagePicker.SelectedIndex = language == UiLanguage.SimplifiedChinese ? 1 : 0;
        languagePicker.SelectedIndexChanged += (_, _) =>
        {
            language = languagePicker.SelectedIndex == 1 ? UiLanguage.SimplifiedChinese : UiLanguage.English;
            ApplyLanguage();
        };
        actions.Controls.Add(languagePicker);
        root.Controls.Add(actions, 0, 1);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        body.ColumnStyles.Add(new(SizeType.Absolute, 265));
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(0, 0, 12, 0) };
        settings.RowStyles.Add(new(SizeType.Absolute, 38));
        settings.RowStyles.Add(new(SizeType.Absolute, 35));
        settings.RowStyles.Add(new(SizeType.Absolute, 35));
        settings.RowStyles.Add(new(SizeType.Absolute, 42));
        settings.RowStyles.Add(new(SizeType.Absolute, 30));
        settings.RowStyles.Add(new(SizeType.Percent, 100));
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
        body.Controls.Add(settings, 0, 0);
        body.Controls.Add(grid, 1, 0);
        root.Controls.Add(body, 0, 2);
        root.Controls.Add(status, 0, 3);
        root.Controls.Add(log, 0, 4);
        Controls.Add(root);
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
        AddLog(UiText.Get(language, "Ready"));
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
    private void AddButton(FlowLayoutPanel parent, Button button, Action action)
    {
        button.Click += (_, _) => Guard(action);
        parent.Controls.Add(button);
    }

    private void ApplyLanguage()
    {
        var checkedKinds = categories.CheckedItems.Cast<ClaimKindOption>().Select(item => item.Kind).ToHashSet();
        Text = UiText.Get(language, "Title");
        header.Text = UiText.Get(language, "Header");
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
        saveSettingsButton.Text = UiText.Get(language, "SaveSettings");
        export.Text = UiText.Get(language, "ExportPlan");
        RebuildCategoryItems(checkedKinds);
        if (observations.Length == 0)
            status.Text = UiText.Get(language, "NoObservations");
        else
            InvalidatePlan();
        ApplyGridHeaders();
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
        if (grid.Columns.Count < 4) return;
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
