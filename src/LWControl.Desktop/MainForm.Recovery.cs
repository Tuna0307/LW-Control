namespace LWControl.Desktop;

public sealed partial class MainForm
{
    private static Control BuildDailyClaimRecoveryPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 3, ColumnCount = 1,
        };
        page.RowStyles.Add(new(SizeType.Absolute, 60));
        page.RowStyles.Add(new(SizeType.Percent, 100));
        page.RowStyles.Add(new(SizeType.Absolute, 75));
        page.Controls.Add(new Label
        {
            Text = "Daily Free Claims · partial\nOnly Daily Task has an implemented runtime. Other categories are unavailable.",
            Dock = DockStyle.Fill, AutoSize = false,
        }, 0, 0);
        var table = new DataGridView
        {
            Name = "DailyClaimRecoveryTable", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = new[]
            {
                new { Category = "Daily Task Chest", Original = "Recovered", Rebuild = "Implemented", Detail = "Existing Daily Task tool; historical live proof is recorded in the repository." },
                new { Category = "Weekly Task Chest", Original = "Route unconfirmed", Rebuild = "Unavailable", Detail = "The supplied original explicitly leaves this category unresolved." },
                new { Category = "VIP Daily Reward", Original = "Recovered", Rebuild = "Unavailable", Detail = "Original collector identified; no current adapter or live acceptance." },
                new { Category = "Store Daily Free Pack", Original = "Recovered", Rebuild = "Unavailable", Detail = "Original collector identified; no current adapter or live acceptance." },
                new { Category = "Login Reward", Original = "Route unconfirmed", Rebuild = "Unavailable", Detail = "The supplied original explicitly leaves this category unresolved." },
                new { Category = "Tavern Free Recruit", Original = "Recovered", Rebuild = "Unavailable", Detail = "The interrupted handoff reports a selector mismatch; current compatibility remains unverified." },
                new { Category = "Campaign Idle Reward", Original = "Recovered", Rebuild = "Unavailable", Detail = "Original collector identified; no current adapter or live acceptance." },
            },
        };
        table.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        table.DataBindingComplete += (_, _) =>
        {
            table.Columns["Category"]!.FillWeight = 24;
            table.Columns["Original"]!.FillWeight = 20;
            table.Columns["Rebuild"]!.FillWeight = 16;
            table.Columns["Detail"]!.FillWeight = 50;
        };
        page.Controls.Add(table, 0, 1);
        page.Controls.Add(new Label
        {
            Text = "These are implementation statuses, not a live reward check.\nRecovered policy: disabled by default, at most 20 free claims per run; no advertisements, tickets, or premium currency.",
            Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(0, 10, 0, 0),
        }, 0, 2);
        return page;
    }

    private void ShowDailyClaimRecovery()
    {
        using var dialog = new Form
        {
            Text = "Daily Free Claims · category status", Size = new Size(1040, 540),
            MinimumSize = new Size(860, 480), StartPosition = FormStartPosition.CenterParent,
            Font = Font, ShowInTaskbar = false, MinimizeBox = false,
        };
        dialog.Controls.Add(BuildDailyClaimRecoveryPage());
        ApplyControlAppearance(dialog, DesktopPalette.Find(appearance.Accent));
        dialog.ShowDialog(this);
    }
}
