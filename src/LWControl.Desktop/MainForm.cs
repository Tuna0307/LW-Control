using LWControl.Core;

namespace LWControl.Desktop;

public sealed class MainForm : Form
{
    private readonly string settingsPath;
    private readonly CheckBox enabled = new() { Text = "Enable daily-claim planning", AutoSize = true };
    private readonly CheckBox expiry = new() { Text = "Prefer expiring rewards (reference core only)", AutoSize = true };
    private readonly CheckBox chests = new() { Text = "Then prioritize task chests", AutoSize = true };
    private readonly NumericUpDown limit = new() { Minimum = 1, Maximum = 20, Width = 70 };
    private readonly CheckedListBox categories = new() { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly Label status = new() { AutoSize = true, Text = "No observations loaded." };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly Button export = new() { Text = "Export plan", AutoSize = true, Enabled = false };
    private ClaimObservation[] observations = [];
    private ClaimPlan? plan;
    private string source = "none";
    private int maxAge = 60;

    public MainForm(string settingsPath)
    {
        this.settingsPath = settingsPath;
        Text = "LW Control — Daily claims";
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
        var header = new Label { Dock = DockStyle.Fill, Text = "DAILY CLAIMS\nLocal planning preview · Game connection not implemented", AutoSize = false };
        root.Controls.Add(header, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        AddButton(actions, "Load observations…", LoadObservations);
        AddButton(actions, "Load sample", LoadSample);
        AddButton(actions, "Build plan", BuildPlan);
        AddButton(actions, "Inspect bridge (read-only)", InspectBridge);
        AddButton(actions, "Save settings", SaveSettings);
        export.Click += (_, _) => Guard(ExportPlan);
        actions.Controls.Add(export);
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
        limits.Controls.Add(new Label { Text = "Claims per run", AutoSize = true });
        limits.Controls.Add(limit);
        settings.Controls.Add(limits, 0, 3);
        settings.Controls.Add(new Label { Text = "Reward categories", AutoSize = true }, 0, 4);
        foreach (var kind in Enum.GetValues<ClaimKind>()) categories.Items.Add(kind);
        settings.Controls.Add(categories, 0, 5);
        body.Controls.Add(settings, 0, 0);
        body.Controls.Add(grid, 1, 0);
        root.Controls.Add(body, 0, 2);
        root.Controls.Add(status, 0, 3);
        root.Controls.Add(log, 0, 4);
        Controls.Add(root);
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
        AddLog("Ready. This application previews decisions; it sends no game actions.");
    }

    private void ApplySettings(DailyClaimSettings value)
    {
        enabled.Checked = value.Enabled;
        expiry.Checked = value.PreferExpiringRewards;
        chests.Checked = value.PreferTaskChests;
        limit.Value = value.MaximumClaimsPerRun;
        maxAge = value.MaxSnapshotAgeSeconds;
        for (int i = 0; i < categories.Items.Count; i++)
            categories.SetItemChecked(i, value.EnabledKinds.Contains((ClaimKind)categories.Items[i]));
    }

    private DailyClaimSettings CurrentSettings() => new()
    {
        Enabled = enabled.Checked, MaximumClaimsPerRun = (int)limit.Value,
        MaxSnapshotAgeSeconds = maxAge, PreferExpiringRewards = expiry.Checked,
        PreferTaskChests = chests.Checked, EnabledKinds = categories.CheckedItems.Cast<ClaimKind>().ToHashSet()
    };

    private void LoadObservations()
    {
        using var dialog = new OpenFileDialog { Filter = "Observation JSON (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var loaded = JsonFiles.Read<ClaimObservation[]>(dialog.FileName);
        _ = DailyClaimPlanner.Build(CurrentSettings(), loaded, DateTimeOffset.UtcNow);
        observations = loaded;
        source = "Imported observations";
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
        source = "SAMPLE DATA";
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
            Decision = d.Selected ? "Selected for preview" : "Skipped", d.Reason
        }).ToArray();
        status.Text = $"{source} · {plan.Decisions.Count(d => d.Selected)} selected · 0 actions sent";
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
        using var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "claim-plan.json", OverwritePrompt = true };
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

    private void InvalidatePlan()
    {
        plan = null;
        grid.DataSource = null;
        export.Enabled = false;
        status.Text = $"{source} · {observations.Length} observations · Build a plan to review";
    }

    private void AddLog(string message) => log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { AddLog(ex.Message); MessageBox.Show(this, ex.Message, "LW Control", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void AddButton(FlowLayoutPanel parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => Guard(action);
        parent.Controls.Add(button);
    }

    public void RunSmokeCheck()
    {
        LoadSample();
        enabled.Checked = true;
        categories.SetItemChecked(Array.IndexOf(Enum.GetValues<ClaimKind>(), ClaimKind.VipDailyReward), true);
        BuildPlan();
        if (plan?.Decisions.Count(d => d.Selected) != 1) throw new InvalidOperationException("UI plan smoke check failed.");
        SaveSettings();
        if (!JsonFiles.Read<DailyClaimSettings>(settingsPath).Enabled) throw new InvalidOperationException("UI settings smoke check failed.");
    }
}
