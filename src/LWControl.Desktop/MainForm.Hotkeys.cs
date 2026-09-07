namespace LWControl.Desktop;

public sealed partial class MainForm
{
    private Control BuildRecoveredHotkeyCards()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        root.RowStyles.Add(new(SizeType.Absolute, 58));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.Controls.Add(new Label
        {
            Text = "Recovered bindings from the reference feature actions are shown below. The actions remain disabled until their feature is implemented.",
            Dock = DockStyle.Fill,
        }, 0, 0);
        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, Padding = new Padding(8), Tag = "feature-grid",
        };
        var groups = new[]
        {
            ("Attack Targets", "Q / W / E / R", "Send squads 1 through 4 to the target under the pointer.", "Foreground game + world target"),
            ("Recall Squads", "A / S / D / F", "Recall squads 1 through 4 to the base.", "Foreground game + active march"),
            ("Shield Countdown", "Space", "Hold Space to show remaining shield time over protected cities.", "Foreground game + shielded city"),
            ("Use Shield", "F6 / F7 / F8", "Use 8-hour, 12-hour, or 24-hour shield items.", "Foreground game + matching shield item"),
            ("Equipment Schemes", "Alt+1 / 2 / 3 / 4", "Apply equipment schemes 1 through 4 to configured squads.", "Foreground game + saved scheme"),
            ("Random Teleport", "F9", "Use a random teleport item and wait for position-change evidence.", "Foreground game + home ready"),
        };
        foreach (var group in groups)
        {
            var card = new ReferenceCard
            {
                Width = 470, Height = 204, RowCount = 4, ColumnCount = 1,
                Padding = new Padding(16), Margin = new Padding(0, 0, 12, 12), Tag = "shortcut-card",
            };
            card.RowStyles.Add(new(SizeType.Absolute, 32));
            card.RowStyles.Add(new(SizeType.Absolute, 34));
            card.RowStyles.Add(new(SizeType.Absolute, 58));
            card.RowStyles.Add(new(SizeType.Percent, 100));
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            heading.ColumnStyles.Add(new(SizeType.Percent, 100));
            heading.ColumnStyles.Add(new(SizeType.AutoSize));
            heading.Controls.Add(new Label { Text = group.Item1, Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 10, FontStyle.Bold) });
            heading.Controls.Add(new Label { Text = "PENDING", AutoSize = true, Tag = FeatureImplementationState.Pending });
            card.Controls.Add(heading, 0, 0);
            card.Controls.Add(new Label { Text = group.Item2, Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 12, FontStyle.Bold) }, 0, 1);
            card.Controls.Add(new Label { Text = group.Item3, AutoSize = true, MaximumSize = new Size(430, 0) }, 0, 2);
            card.Controls.Add(new Label { Text = group.Item4, AutoSize = true, MaximumSize = new Size(430, 0) }, 0, 3);
            cards.Controls.Add(card);
        }
        cards.SizeChanged += (_, _) => ResizeFeatureCards(cards);
        root.Controls.Add(cards, 0, 1);
        return root;
    }
}
