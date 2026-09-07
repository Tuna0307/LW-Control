namespace LWControl.Desktop;

// A native-control tab strip with the reference's dark segmented treatment.
// Page controls are retained when switching tabs, including unsaved selections.
internal sealed class ReferenceTabs : UserControl
{
    private readonly FlowLayoutPanel strip = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false, AutoScroll = true };
    private readonly Panel host = new() { Dock = DockStyle.Fill };
    private readonly List<(Button Button, Control Page)> pages = [];
    private DesktopPalette palette = DesktopPalette.Find("Indigo");
    public int SelectedIndex { get; private set; } = -1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<string, string> TranslateText { get; set; } = text => text;
    public IReadOnlyList<Control> Pages => pages.Select(item => item.Page).ToArray();

    public ReferenceTabs()
    {
        Dock = DockStyle.Fill;
        Controls.Add(host);
        Controls.Add(strip);
    }

    public void AddPage(string title, Control page)
    {
        int index = pages.Count;
        var button = new Button
        {
            Text = title, AutoSize = true, Height = 34, MinimumSize = new Size(86, 34),
            Margin = new Padding(0, 0, 8, 8), FlatStyle = FlatStyle.Flat, UseMnemonic = false,
        };
        button.Click += (_, _) => SelectPage(index);
        strip.Controls.Add(button);
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        host.Controls.Add(page);
        pages.Add((button, page));
        if (SelectedIndex < 0) SelectPage(0);
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
        for (int i = 0; i < pages.Count; i++) pages[i].Page.Visible = i == index;
        pages[index].Page.BringToFront();
        ApplyPalette(palette);
    }

    public void ApplyPalette(DesktopPalette value)
    {
        palette = value;
        for (int i = 0; i < pages.Count; i++)
        {
            var button = pages[i].Button;
            button.ForeColor = i == SelectedIndex ? palette.HighlightColor : Color.FromArgb(160, 168, 186);
            button.BackColor = i == SelectedIndex ? Color.FromArgb(25, 30, 44) : Color.FromArgb(11, 12, 16);
            button.FlatAppearance.BorderColor = i == SelectedIndex ? palette.PrimaryColor : Color.FromArgb(49, 54, 68);
            button.AccessibleDescription = TranslateText(i == SelectedIndex ? "Selected tab" : "Tab");
        }
    }
}
