using System.Drawing.Drawing2D;

namespace LWControl.Desktop;

internal sealed class ReferenceCard : TableLayoutPanel
{
    public ReferenceCard()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var backdrop = new SolidBrush(Parent?.BackColor ?? Color.FromArgb(11, 12, 16));
        e.Graphics.FillRectangle(backdrop, ClientRectangle);
        if (Width < 18 || Height < 18) return;
        using var path = new GraphicsPath();
        const int diameter = 16;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter - 1, Height - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(BackColor);
        using var border = new Pen(Color.FromArgb(40, 44, 57));
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);
    }
}
