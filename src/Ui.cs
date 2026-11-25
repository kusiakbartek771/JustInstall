// JustInstall – kontrolki rysowane ręcznie w stylu JustMusic (ciemne tło, akcent fioletowo-różowy, zaokrąglenia).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace JustInstall
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0x0D, 0x0E, 0x12);
        public static readonly Color Surface = Color.FromArgb(0x15, 0x17, 0x1D);
        public static readonly Color Surface2 = Color.FromArgb(0x1C, 0x1F, 0x27);
        public static readonly Color Elevated = Color.FromArgb(0x25, 0x29, 0x35);
        public static readonly Color Border = Color.FromArgb(0x26, 0x2A, 0x35);
        public static readonly Color Text = Color.FromArgb(0xEE, 0xF0, 0xF6);
        public static readonly Color Dim = Color.FromArgb(0x8B, 0x91, 0xA3);
        public static readonly Color Accent = Color.FromArgb(0x7C, 0x5C, 0xFF);
        public static readonly Color Pink = Color.FromArgb(0xF0, 0x50, 0x9F);
        public static readonly Color Ok = Color.FromArgb(0x4A, 0xDE, 0x80);
        public static readonly Color Warn = Color.FromArgb(0xFA, 0xCC, 0x15);
        public static readonly Color Danger = Color.FromArgb(0xFF, 0x3D, 0x5E);

        static string family;
        public static Font F(float size, FontStyle st = FontStyle.Regular)
        {
            if (family == null)
            {
                var installed = new InstalledFontCollection().Families.Select(f => f.Name).ToList();
                family = new[] { "Segoe UI Variable Text", "Segoe UI", "Inter", "Noto Sans", "DejaVu Sans" }.FirstOrDefault(installed.Contains) ?? FontFamily.GenericSansSerif.Name;
            }
            return new Font(family, size, st, GraphicsUnit.Point);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Brush Gradient(RectangleF r)
        {
            return new LinearGradientBrush(new PointF(r.Left, r.Top), new PointF(r.Right, r.Bottom), Accent, Pink);
        }

        /// <summary>cubic-bezier(.22,1,.36,1) z JustMusic</summary>
        public static float EaseOut(float t) { return 1f - (float)Math.Pow(1f - Math.Max(0, Math.Min(1, t)), 5); }
    }

    /// <summary>Kontrolka z płynnym „podświetleniem” po najechaniu (animacja jak w JustMusic).</summary>
    public class AnimControl : Control
    {
        protected float hover, press;
        bool over, down;
        readonly Timer timer = new Timer { Interval = 15 };

        public AnimControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            timer.Tick += (s, e) =>
            {
                float th = over && Enabled ? 1 : 0, tp = down && Enabled ? 1 : 0;
                hover += (th - hover) * 0.25f;
                press += (tp - press) * 0.4f;
                if (Math.Abs(th - hover) < 0.01f && Math.Abs(tp - press) < 0.01f) { hover = th; press = tp; timer.Stop(); }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { over = true; timer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; down = false; timer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; timer.Start(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; timer.Start(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    public enum BtnKind { Primary, Normal, Danger, Ghost }

    public class Btn : AnimControl
    {
        public BtnKind Kind = BtnKind.Normal;
        public string Glyph; // prosty symbol przed tekstem

        public Btn(string text, BtnKind kind = BtnKind.Normal)
        {
            Text = text; Kind = kind;
            Font = Theme.F(9.5f, FontStyle.Bold);
            Height = 36;
        }

        public int PreferredWidth()
        {
            using (var g = CreateGraphics())
                return (int)Math.Ceiling(g.MeasureString((Glyph != null ? Glyph + "  " : "") + Text, Font).Width) + 32;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            float s = 1 - press * 0.04f;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.ScaleTransform(s, s);
            g.TranslateTransform(-Width / 2f, -Height / 2f);
            using (var path = Theme.Round(r, 9))
            {
                Color fg = Theme.Text;
                if (!Enabled)
                {
                    using (var b = new SolidBrush(Theme.Surface2)) g.FillPath(b, path);
                    fg = Color.FromArgb(110, Theme.Dim);
                }
                else if (Kind == BtnKind.Primary)
                {
                    using (var b = Theme.Gradient(r)) g.FillPath(b, path);
                    using (var b = new SolidBrush(Color.FromArgb((int)(hover * 40), Color.White))) g.FillPath(b, path);
                    fg = Color.White;
                }
                else if (Kind == BtnKind.Danger)
                {
                    using (var b = new SolidBrush(Theme.Mix(Color.FromArgb(0x2A, 0x16, 0x1B), Color.FromArgb(0x3A, 0x18, 0x20), hover))) g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb((int)(90 + hover * 100), Theme.Danger))) g.DrawPath(pen, path);
                    fg = Color.FromArgb(0xFF, 0x9A, 0xAB);
                }
                else if (Kind == BtnKind.Ghost)
                {
                    using (var b = new SolidBrush(Color.FromArgb((int)(hover * 255), Theme.Elevated))) g.FillPath(b, path);
                    fg = Theme.Mix(Theme.Dim, Theme.Text, hover);
                }
                else
                {
                    using (var b = new SolidBrush(Theme.Mix(Theme.Surface2, Theme.Elevated, hover))) g.FillPath(b, path);
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
                }
                string t = (Glyph != null ? Glyph + "  " : "") + Text;
                using (var b = new SolidBrush(fg))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(t, Font, b, r, sf);
            }
        }
    }

    /// <summary>Przełącznik jak w ustawieniach JustMusic.</summary>
    public class Toggle : AnimControl
    {
        bool on;
        float knob;
        readonly Timer anim = new Timer { Interval = 15 };
        public event EventHandler Changed;
        public string Label;

        public Toggle(string label, bool value)
        {
            Label = label; on = value; knob = value ? 1 : 0;
            Font = Theme.F(9.5f);
            Height = 26;
            anim.Tick += (s, e) => { float t = on ? 1 : 0; knob += (t - knob) * 0.3f; if (Math.Abs(t - knob) < 0.01f) { knob = t; anim.Stop(); } Invalidate(); };
            Click += (s, e) => { Checked = !on; };
        }

        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; anim.Start(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }

        public int PreferredWidth()
        {
            using (var g = CreateGraphics()) return 46 + (int)Math.Ceiling(g.MeasureString(Label, Font).Width) + 4;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            var track = new RectangleF(0, (Height - 20) / 2f, 36, 20);
            using (var path = Theme.Round(track, 10))
            {
                using (var b = new SolidBrush(Theme.Mix(Theme.Elevated, Theme.Accent, knob))) g.FillPath(b, path);
            }
            float kx = track.X + 3 + knob * 16;
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, track.Y + 3, 14, 14);
            using (var b = new SolidBrush(Theme.Mix(Theme.Dim, Theme.Text, Math.Max(hover, 0.6f))))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(Label, Font, b, new RectangleF(46, 0, Width - 46, Height), sf);
        }
    }

    /// <summary>Okno dialogowe w stylu aplikacji (zamiast szarego MessageBox).</summary>
    public class Dialog : Form
    {
        public bool Result;
        readonly string title, message;
        readonly Color tone;

        Dialog(string title, string message, string ok, string cancel, bool danger)
        {
            this.title = title; this.message = message;
            tone = danger ? Theme.Danger : Theme.Accent;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Font = Theme.F(10f);
            int w = 440;
            int textH;
            using (var g = CreateGraphics()) textH = (int)Math.Ceiling(g.MeasureString(message, Font, w - 48).Height);
            ClientSize = new Size(w, 70 + textH + 70);
            var okB = new Btn(ok, danger ? BtnKind.Danger : BtnKind.Primary);
            okB.Width = Math.Max(110, okB.PreferredWidth());
            okB.Location = new Point(w - 24 - okB.Width, ClientSize.Height - 24 - okB.Height);
            okB.Click += (s, e) => { Result = true; Close(); };
            Controls.Add(okB);
            if (cancel != null)
            {
                var cb = new Btn(cancel, BtnKind.Ghost);
                cb.Width = Math.Max(90, cb.PreferredWidth());
                cb.Location = new Point(okB.Left - 8 - cb.Width, okB.Top);
                cb.Click += (s, e) => Close();
                Controls.Add(cb);
            }
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); if (e.KeyCode == Keys.Enter) { Result = true; Close(); } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(tone)) g.FillRectangle(b, 0, 0, Width, 3);
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(title, Theme.F(13f, FontStyle.Bold), b, 22, 20);
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(message, Font, b, new RectangleF(24, 58, Width - 48, Height - 120));
        }

        public static bool Ask(IWin32Window owner, string title, string message, string ok, string cancel = "Anuluj", bool danger = false)
        {
            using (var d = new Dialog(title, message, ok, cancel, danger)) { d.ShowDialog(owner); return d.Result; }
        }

        public static void Info(IWin32Window owner, string title, string message, bool error = false)
        {
            using (var d = new Dialog(title, message, "OK", null, error)) d.ShowDialog(owner);
        }
    }
}
