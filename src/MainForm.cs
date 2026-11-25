// JustInstall – główne okno: karty JustMusic i JustMinecraft, opcje, „zainstaluj wszystko”.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JustInstall
{
    public class AppCard : Control
    {
        public readonly AppInfo App;
        public Installed Inst;
        public Release Rel;
        public string Status = "Sprawdzanie…";
        public Color StatusColor = Theme.Dim;
        public float Progress = float.NaN; // NaN = brak paska, <0 = nieokreślony
        public bool Busy;
        public readonly Btn Main, Repair, Remove;
        readonly Image icon;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 16 };
        float shown, shimmer, appear;
        readonly DateTime born = DateTime.Now;

        public AppCard(AppInfo app)
        {
            App = app;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            try { icon = Image.FromStream(typeof(AppCard).Assembly.GetManifestResourceStream(app.Id + "-128.png")); } catch { }
            Main = new Btn("Zainstaluj", BtnKind.Primary);
            Repair = new Btn("Napraw", BtnKind.Normal);
            Remove = new Btn("Odinstaluj", BtnKind.Danger);
            Controls.AddRange(new Control[] { Main, Repair, Remove });
            anim.Tick += (s, e) =>
            {
                float target = float.IsNaN(Progress) ? 0 : Progress < 0 ? 0 : Progress;
                shown += (target - shown) * 0.18f;
                shimmer = (shimmer + 0.012f) % 1f;
                appear = Theme.EaseOut((float)(DateTime.Now - born).TotalMilliseconds / 420f);
                Invalidate();
            };
            anim.Start();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int y = Height - 22 - 36, x = 22;
            Main.Width = Math.Max(130, Main.PreferredWidth()); Main.Location = new Point(x, y);
            Repair.Width = Math.Max(96, Repair.PreferredWidth()); Repair.Location = new Point(Main.Right + 8, y);
            Remove.Width = Math.Max(110, Remove.PreferredWidth()); Remove.Location = new Point(Width - 22 - Remove.Width, y);
        }

        public void Refresh2() { PerformLayout(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            g.Clear(Theme.Bg);
            float lift = (1 - appear) * 14;
            g.TranslateTransform(0, lift);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f - lift);
            using (var path = Theme.Round(r, 16))
            {
                using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                // poświata akcentu w rogu karty
                var glow = new RectangleF(-80, -120, 320, 260);
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(glow);
                    using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(46, Theme.Accent), SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) } })
                    {
                        var clip = g.Clip; g.SetClip(path); g.FillEllipse(pb, glow); g.Clip = clip;
                    }
                }
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }
            // ikona
            var ir = new RectangleF(22, 22, 64, 64);
            if (icon != null) g.DrawImage(icon, ir);
            // nazwa i wersje
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(App.Name, Theme.F(16f, FontStyle.Bold), b, 100, 22);
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(App.Description, Theme.F(9.5f), b, new RectangleF(101, 54, Width - 124, 40));
            // stan
            float sy = 106;
            using (var b = new SolidBrush(StatusColor)) g.FillEllipse(b, 24, sy + 6, 8, 8);
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Status, Theme.F(9.5f, FontStyle.Bold), b, new RectangleF(38, sy, Width - 60, 20));
            string where = Inst != null && Inst.Location != null ? Inst.Location : (Rel != null ? "Najnowsza wersja: " + Rel.Version + " · " + Apps.Mb(Rel.Size) : "");
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(where, Theme.F(8.5f), b, new RectangleF(38, sy + 20, Width - 60, 18), new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap });
            // pasek postępu
            if (!float.IsNaN(Progress))
            {
                var pr = new RectangleF(22, sy + 46, Width - 44, 6);
                using (var path = Theme.Round(pr, 3)) using (var b = new SolidBrush(Theme.Elevated)) g.FillPath(b, path);
                RectangleF fill;
                if (Progress < 0) fill = new RectangleF(pr.X + (pr.Width * 1.35f) * shimmer - pr.Width * 0.35f, pr.Y, pr.Width * 0.35f, pr.Height);
                else fill = new RectangleF(pr.X, pr.Y, Math.Max(6, pr.Width * shown), pr.Height);
                var clip = g.Clip;
                using (var cp = Theme.Round(pr, 3)) g.SetClip(cp);
                using (var path = Theme.Round(fill, 3)) using (var b = Theme.Gradient(pr)) g.FillPath(b, path);
                g.Clip = clip;
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
    }

    public class MainForm : Form
    {
        readonly List<AppCard> cards = new List<AppCard>();
        readonly Btn closeB, minB, allB, refreshB, dirB;
        readonly Toggle launchAfter, removeData;
        string baseDir;
        bool busy;
        string footer = "";
        Point dragFrom;
        bool dragging;
        readonly Image logo;
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public MainForm()
        {
            Text = "JustInstall";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            Font = Theme.F(9.5f);
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(820, 560);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            try { logo = Image.FromStream(typeof(MainForm).Assembly.GetManifestResourceStream("justinstall-128.png")); } catch { }
            baseDir = Apps.Os == OS.Windows ? Path.GetDirectoryName(Apps.DefaultDir(Apps.All[0])) : Apps.DefaultDir(Apps.All[0]);

            closeB = new Btn("×", BtnKind.Ghost) { Size = new Size(40, 32), Font = Theme.F(14f) };
            minB = new Btn("–", BtnKind.Ghost) { Size = new Size(40, 32), Font = Theme.F(12f) };
            closeB.Click += (s, e) => Close();
            minB.Click += (s, e) => WindowState = FormWindowState.Minimized;
            Controls.Add(closeB); Controls.Add(minB);

            foreach (var a in Apps.All)
            {
                var c = new AppCard(a);
                c.Main.Click += (s, e) => MainAction(c);
                c.Repair.Click += (s, e) => RunOp(c, "repair");
                c.Remove.Click += (s, e) => RunOp(c, "uninstall");
                cards.Add(c);
                Controls.Add(c);
            }

            launchAfter = new Toggle("Uruchom po instalacji", true);
            removeData = new Toggle("Przy odinstalowaniu usuń też dane", false);
            dirB = new Btn("Zmień folder", BtnKind.Ghost) { Height = 30, Font = Theme.F(9f, FontStyle.Bold) };
            dirB.Click += (s, e) => ChooseDir();
            allB = new Btn("Zainstaluj wszystko", BtnKind.Primary) { Height = 40, Font = Theme.F(10.5f, FontStyle.Bold) };
            allB.Click += (s, e) => InstallAll();
            refreshB = new Btn("Odśwież", BtnKind.Ghost) { Height = 40 };
            refreshB.Click += (s, e) => CheckAll();
            Controls.AddRange(new Control[] { launchAfter, removeData, allB, refreshB });
            if (Apps.Os == OS.Windows) Controls.Add(dirB);

            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 56) { dragging = true; dragFrom = e.Location; } };
            MouseMove += (s, e) => { if (dragging) Location = new Point(Location.X + e.X - dragFrom.X, Location.Y + e.Y - dragFrom.Y); };
            MouseUp += (s, e) => dragging = false;
            Shown += (s, e) => { RoundCorners(); CheckAll(); Automation(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (Apps.Os == OS.Windows) { cp.ClassStyle |= 0x20000; cp.Style |= 0x20000; } // cień + minimalizacja z paska zadań
                return cp;
            }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        void RoundCorners()
        {
            if (Apps.Os != OS.Windows) return;
            try { int pref = 2; DwmSetWindowAttribute(Handle, 33, ref pref, 4); } catch { } // Windows 11: zaokrąglone rogi
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch { }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (closeB == null || allB == null || launchAfter == null) return;
            closeB.Location = new Point(ClientSize.Width - 8 - closeB.Width, 10);
            minB.Location = new Point(closeB.Left - minB.Width, 10);
            int pad = 28, gap = 18, top = 150;
            int cw = (ClientSize.Width - pad * 2 - gap) / 2;
            for (int i = 0; i < cards.Count; i++) cards[i].Bounds = new Rectangle(pad + i * (cw + gap), top, cw, 262);
            int oy = top + 262 + 22;
            launchAfter.Location = new Point(pad, oy); launchAfter.Width = launchAfter.PreferredWidth();
            removeData.Location = new Point(launchAfter.Right + 28, oy); removeData.Width = removeData.PreferredWidth();
            dirB.Width = dirB.PreferredWidth();
            dirB.Location = new Point(ClientSize.Width - pad - dirB.Width, oy - 2);
            allB.Width = Math.Max(200, allB.PreferredWidth());
            allB.Location = new Point(ClientSize.Width - pad - allB.Width, ClientSize.Height - 24 - allB.Height);
            refreshB.Width = refreshB.PreferredWidth();
            refreshB.Location = new Point(allB.Left - 8 - refreshB.Width, allB.Top);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            // poświata w tle (jak gradient JustMusic)
            using (var gp = new GraphicsPath())
            {
                var rr = new RectangleF(-160, -260, 760, 520);
                gp.AddEllipse(rr);
                using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(60, Theme.Accent), SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) } }) g.FillEllipse(pb, rr);
            }
            using (var gp = new GraphicsPath())
            {
                var rr = new RectangleF(ClientSize.Width - 520, ClientSize.Height - 300, 760, 520);
                gp.AddEllipse(rr);
                using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(40, Theme.Pink), SurroundColors = new[] { Color.FromArgb(0, Theme.Pink) } }) g.FillEllipse(pb, rr);
            }
            if (Apps.Os == OS.Windows) using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            // pasek tytułu
            if (logo != null) g.DrawImage(logo, 18, 14, 26, 26);
            using (var b = new SolidBrush(Theme.Text)) g.DrawString("JustInstall", Theme.F(10.5f, FontStyle.Bold), b, 52, 17);
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString("v" + Program.Version, Theme.F(8.5f), b, 140, 19);
            // nagłówek
            using (var b = new SolidBrush(Theme.Text)) g.DrawString("Aplikacje Just", Theme.F(22f, FontStyle.Bold), b, 26, 66);
            using (var b = new SolidBrush(Theme.Dim))
                g.DrawString("Instalacja, aktualizacja, naprawa i odinstalowanie jednym kliknięciem. Pliki pobierane prosto z GitHuba i sprawdzane sumą SHA-512.", Theme.F(10f), b, new RectangleF(28, 108, ClientSize.Width - 56, 22));
            // folder
            int oy = 150 + 262 + 22;
            string dirText = Apps.Os == OS.Windows ? "Folder: " + baseDir + "\\…" : "Folder: " + baseDir;
            using (var b = new SolidBrush(Theme.Dim))
                g.DrawString(dirText, Theme.F(8.5f), b, new RectangleF(28, oy + 34, ClientSize.Width - 56, 18), new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap });
            // stopka
            using (var b = new SolidBrush(Theme.Dim))
                g.DrawString(footer, Theme.F(9f), b, new RectangleF(28, ClientSize.Height - 24 - 40, refreshB.Left - 40, 40), new StringFormat { LineAlignment = StringAlignment.Center });
        }

        void SetFooter(string s) { footer = s; Invalidate(new Rectangle(0, ClientSize.Height - 80, ClientSize.Width, 80)); }

        // ---------------- stan ----------------
        void CheckAll()
        {
            if (busy) return;
            SetFooter("Sprawdzam najnowsze wersje…");
            int pending = cards.Count;
            foreach (var c in cards)
            {
                var card = c;
                card.Status = "Sprawdzanie…"; card.StatusColor = Theme.Dim; card.Progress = -1; Lock(card, true); card.Invalidate();
                Task.Run(() =>
                {
                    Installed inst = null; Release rel = null; string err = null;
                    try { inst = Apps.Find(card.App); } catch { }
                    try { rel = Apps.Latest(card.App); } catch (Exception ex) { err = ex.Message; }
                    BeginInvoke((Action)(() =>
                    {
                        card.Inst = inst; card.Rel = rel; card.Progress = float.NaN;
                        Describe(card, err);
                        Lock(card, false);
                        if (--pending == 0) { SetFooter(Apps.Os == OS.Mac ? "macOS: pobierz .dmg ze strony wydań – JustInstall obsługuje Windows i Linux." : "Gotowe."); UpdateAllButton(); }
                    }));
                });
            }
        }

        void Describe(AppCard c, string err)
        {
            if (c.Inst != null)
            {
                bool old = c.Rel != null && c.Inst.Version != "?" && Apps.Compare(c.Rel.Version, c.Inst.Version) > 0;
                c.Status = "Zainstalowano " + c.Inst.Version + (old ? " · dostępna " + c.Rel.Version : c.Rel != null ? " · aktualna" : "");
                c.StatusColor = old ? Theme.Warn : Theme.Ok;
                c.Main.Text = old ? "Aktualizuj" : "Uruchom";
                c.Main.Glyph = null;
            }
            else
            {
                c.Status = err != null ? "Brak połączenia z GitHubem" : "Nie zainstalowano";
                c.StatusColor = err != null ? Theme.Danger : Theme.Dim;
                c.Main.Text = "Zainstaluj";
            }
            if (err != null && c.Inst != null) { c.Status += " · " + "brak połączenia"; }
            c.Repair.Enabled = c.Inst != null && c.Rel != null;
            c.Remove.Enabled = c.Inst != null;
            c.Main.Enabled = c.Inst != null || c.Rel != null;
            c.Refresh2();
        }

        void Lock(AppCard c, bool on)
        {
            c.Busy = on;
            c.Main.Enabled = !on; c.Repair.Enabled = !on && c.Inst != null; c.Remove.Enabled = !on && c.Inst != null;
        }

        void UpdateAllButton()
        {
            int todo = cards.Count(c => c.Rel != null && (c.Inst == null || (c.Inst.Version != "?" && Apps.Compare(c.Rel.Version, c.Inst.Version) > 0)));
            allB.Text = todo == 0 ? "Wszystko aktualne" : cards.Any(c => c.Inst != null) ? "Zainstaluj / zaktualizuj (" + todo + ")" : "Zainstaluj wszystko";
            allB.Enabled = todo > 0 && !busy;
            PerformLayout();
        }

        // ---------------- akcje ----------------
        void MainAction(AppCard c)
        {
            if (c.Inst != null && c.Main.Text == "Uruchom")
            {
                try { Apps.Launch(c.App); SetFooter("Uruchomiono " + c.App.Name + "."); } catch (Exception ex) { Dialog.Info(this, "Nie udało się uruchomić", ex.Message, true); }
                return;
            }
            RunOp(c, c.Inst != null ? "update" : "install");
        }

        async void InstallAll()
        {
            foreach (var c in cards.Where(c => c.Rel != null && (c.Inst == null || Apps.Compare(c.Rel.Version, c.Inst.Version) > 0)).ToList())
                await Op(c, c.Inst != null ? "update" : "install", true);
        }

        void RunOp(AppCard c, string op) { Op(c, op, false).ContinueWith(t => { }); }

        async Task Op(AppCard c, string op, bool quiet)
        {
            if (busy) return;
            var a = c.App;
            bool auto = autoMode;
            if (op == "uninstall" && !auto)
            {
                string msg = "Aplikacja zostanie usunięta z komputera." + (removeData.Checked ? "\n\nUsunięte zostaną też dane: " + a.DataWarning + ". Tego nie da się cofnąć." : "\n\nTwoje dane (" + a.DataWarning + ") zostaną – po ponownej instalacji wszystko wróci.");
                if (!Dialog.Ask(this, "Odinstalować " + a.Name + "?", msg, "Odinstaluj", "Anuluj", true)) return;
            }
            if (Apps.Running(a).Length > 0 && !auto && !quiet)
            {
                if (!Dialog.Ask(this, a.Name + " jest uruchomiony", "Żeby kontynuować, JustInstall zamknie " + a.Name + ". Niezapisane zmiany mogą przepaść.", "Zamknij i kontynuuj")) return;
            }
            busy = true; allB.Enabled = false; refreshB.Enabled = false; dirB.Enabled = false;
            Lock(c, true);
            foreach (var other in cards) if (other != c) Lock(other, true);
            string title = op == "install" ? "Instalacja" : op == "update" ? "Aktualizacja" : op == "repair" ? "Naprawa" : "Odinstalowanie";
            Action<string> status = s => BeginInvoke((Action)(() => { c.Status = s; c.StatusColor = Theme.Accent; c.Invalidate(); }));
            Action<double, string> progress = (p, s) => BeginInvoke((Action)(() => { c.Progress = (float)p; c.Status = s; c.StatusColor = Theme.Accent; c.Invalidate(); }));
            c.Progress = -1;
            SetFooter(title + ": " + a.Name + "…");
            string error = null;
            try
            {
                await Task.Run(() =>
                {
                    if (op == "uninstall") { Apps.Uninstall(a, removeData.Checked, status); return; }
                    var rel = c.Rel ?? Apps.Latest(a);
                    if (op == "repair") { status("Czyszczenie pamięci aktualizacji…"); Apps.ClearUpdaterCache(a); }
                    string file = Apps.Download(rel, progress, cts.Token);
                    BeginInvoke((Action)(() => c.Progress = -1));
                    string dir = c.Inst != null ? c.Inst.Location : (Apps.Os == OS.Windows ? Path.Combine(baseDir, a.Product) : null);
                    Apps.Install(a, file, rel, dir, status);
                });
            }
            catch (Exception ex) { error = ex.Message; }
            c.Progress = float.NaN;
            try { c.Inst = Apps.Find(a); } catch { }
            Describe(c, null);
            busy = false; refreshB.Enabled = true; dirB.Enabled = true;
            foreach (var x in cards) { Lock(x, false); Describe(x, null); }
            UpdateAllButton();
            if (error != null)
            {
                c.Status = title + " nie powiodła się"; c.StatusColor = Theme.Danger; c.Invalidate();
                SetFooter(a.Name + ": " + error);
                if (!auto) Dialog.Info(this, title + " nie powiodła się", error, true);
                return;
            }
            SetFooter(op == "uninstall" ? a.Name + " został odinstalowany." : a.Name + " " + (c.Inst != null ? c.Inst.Version : "") + " – gotowe.");
            if (op != "uninstall" && launchAfter.Checked && !auto && !quiet)
                try { Apps.Launch(a); } catch { }
        }

        void ChooseDir()
        {
            using (var d = new FolderBrowserDialog { Description = "Folder, w którym powstaną podfoldery JustMusic i JustMinecraft", SelectedPath = baseDir, ShowNewFolderButton = true })
                if (d.ShowDialog(this) == DialogResult.OK) { baseDir = d.SelectedPath; Invalidate(); }
        }

        // ---------------- automatyzacja (testy): JUSTINSTALL_AUTO="install:justmusic,uninstall:justmusic", JUSTINSTALL_SHOT=plik.png ----------------
        bool autoMode;
        async void Automation()
        {
            string auto = Environment.GetEnvironmentVariable("JUSTINSTALL_AUTO");
            string shot = Environment.GetEnvironmentVariable("JUSTINSTALL_SHOT");
            if (auto == null && shot == null) return;
            autoMode = true;
            while (cards.Any(c => c.Busy)) await Task.Delay(200);
            int n = 0;
            foreach (string step in (auto ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = step.Split(':');
                var c = cards.FirstOrDefault(x => x.App.Id == p[1]);
                if (p[0] == "shot") { await Task.Delay(800); Shot(shot.Replace(".png", "-" + (n++) + ".png")); continue; }
                Console.WriteLine("[auto] " + step);
                await Op(c, p[0], true);
                Console.WriteLine("[auto] " + step + " → " + c.Status + " | " + footer);
            }
            await Task.Delay(1500);
            if (shot != null) Shot(shot);
            Close();
        }

        void Shot(string file)
        {
            using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                bmp.Save(file, ImageFormat.Png);
            }
            Console.WriteLine("[auto] zrzut " + file);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && !autoMode && !Dialog.Ask(this, "Przerwać?", "Trwa instalacja albo odinstalowanie. Zamknięcie teraz może zostawić aplikację w połowie – wtedy użyj „Napraw”.", "Zamknij mimo to", "Zostań", true))
            { e.Cancel = true; return; }
            cts.Cancel();
            base.OnFormClosing(e);
        }
    }
}
