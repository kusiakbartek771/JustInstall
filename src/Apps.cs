// JustInstall – logika: wydania z GitHuba, pobieranie z weryfikacją SHA-512, instalacja, odinstalowanie, naprawa.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace JustInstall
{
    public enum OS { Windows, Linux, Mac }

    public class AppInfo
    {
        public string Id, Name, Repo, Description, Product;
        public string[] DisplayNames;   // nazwy w „Programy i funkcje” (także dawne)
        public string[] ExeNames;       // pliki .exe w folderze instalacji
        public string[] DataDirs;       // foldery danych w %APPDATA% / ~/.config
        public string[] UpdaterCaches;  // pamięć podręczna aktualizacji (%LOCALAPPDATA%)
        public Regex WinAsset, LinuxAsset;
        public string DataWarning;
    }

    public class Release
    {
        public string Version, Tag, AssetName, AssetUrl, Sha512, PageUrl;
        public long Size;
    }

    public class Installed
    {
        public string Version, Location, Exe, Uninstall, RegKey;
    }

    public static class Apps
    {
        public const string Owner = "kusiakbartek771";

        public static readonly AppInfo[] All = new[]
        {
            new AppInfo {
                Id = "justmusic", Name = "JustMusic", Repo = "JustMusic", Product = "JustMusic",
                Description = "Odtwarzacz muzyki – YouTube, Spotify i pliki lokalne, wtyczki i motywy.",
                DisplayNames = new[] { "JustMusic" }, ExeNames = new[] { "JustMusic.exe" },
                DataDirs = new[] { "JustMusic" }, UpdaterCaches = new[] { "justmusic-updater" },
                WinAsset = new Regex(@"^JustMusic-Installer\.exe$", RegexOptions.IgnoreCase),
                LinuxAsset = new Regex(@"^JustMusic-x86_64\.AppImage$", RegexOptions.IgnoreCase),
                DataWarning = "playlisty, ulubione, ustawienia i wtyczki",
            },
            new AppInfo {
                Id = "justminecraft", Name = "JustMinecraft", Repo = "JustMinecraft", Product = "JustMinecraft",
                Description = "Launcher Minecrafta do modów – Fabric, Forge, NeoForge, Quilt, Modrinth i CurseForge.",
                DisplayNames = new[] { "JustMinecraft", "JustLauncher" }, ExeNames = new[] { "JustMinecraft.exe", "JustLauncher.exe" },
                DataDirs = new[] { "JustMinecraft", "JustLauncher" }, UpdaterCaches = new[] { "justminecraft-updater", "justlauncher-updater" },
                WinAsset = new Regex(@"^JustMinecraft-Setup-[\d.]+\.exe$", RegexOptions.IgnoreCase),
                LinuxAsset = new Regex(@"^JustMinecraft-x86_64\.AppImage$", RegexOptions.IgnoreCase),
                DataWarning = "instancje Minecrafta (w tym światy!), mody, konta i ustawienia",
            },
        };

        public static OS Os
        {
            get
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) return OS.Windows;
                if (Directory.Exists("/Applications") && Directory.Exists("/System/Library")) return OS.Mac;
                return OS.Linux;
            }
        }

        static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }

        public static string WorkDir
        {
            get
            {
                string d = Path.Combine(Path.GetTempPath(), "JustInstall");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        // ---------------- sieć ----------------
        static HttpWebRequest Req(string url)
        {
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "JustInstall/" + Program.Version + " (+https://github.com/" + Owner + ")";
            r.Timeout = 30000;
            r.ReadWriteTimeout = 60000;
            r.AllowAutoRedirect = true;
            r.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return r;
        }

        public static string GetText(string url)
        {
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var r = Req(url);
                    r.Accept = "application/vnd.github+json, */*";
                    using (var resp = (HttpWebResponse)r.GetResponse())
                    using (var s = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        return s.ReadToEnd();
                }
                catch (WebException e)
                {
                    last = e;
                    var hr = e.Response as HttpWebResponse;
                    if (hr != null && (int)hr.StatusCode < 500 && (int)hr.StatusCode != 429) break;
                    Thread.Sleep(600 * (i + 1));
                }
            }
            throw new Exception("Nie udało się połączyć z GitHubem: " + (last != null ? last.Message : "?"));
        }

        /// <summary>Najnowsze wydanie i plik instalatora dla tego systemu (+ SHA-512 z latest*.yml).</summary>
        public static Release Latest(AppInfo a)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            var rel = js.Deserialize<Dictionary<string, object>>(GetText("https://api.github.com/repos/" + Owner + "/" + a.Repo + "/releases/latest"));
            var r = new Release();
            r.Tag = (string)rel["tag_name"];
            r.Version = r.Tag.TrimStart('v', 'V');
            r.PageUrl = (string)rel["html_url"];
            Regex want = Os == OS.Windows ? a.WinAsset : a.LinuxAsset;
            foreach (var o in (System.Collections.ArrayList)rel["assets"])
            {
                var asset = (Dictionary<string, object>)o;
                string name = (string)asset["name"];
                if (!want.IsMatch(name)) continue;
                r.AssetName = name;
                r.AssetUrl = (string)asset["browser_download_url"];
                r.Size = Convert.ToInt64(asset["size"]);
            }
            if (r.AssetUrl == null) throw new Exception("Wydanie " + r.Tag + " nie ma pliku dla tego systemu.");
            try
            {
                string yml = GetText("https://github.com/" + Owner + "/" + a.Repo + "/releases/download/" + r.Tag + "/" + (Os == OS.Windows ? "latest.yml" : "latest-linux.yml"));
                var m = Regex.Match(yml, @"-\s*url:\s*" + Regex.Escape(r.AssetName) + @"\s*\r?\n\s*sha512:\s*(\S+)");
                if (m.Success) r.Sha512 = m.Groups[1].Value;
            }
            catch { /* brak sumy – pobierzemy bez weryfikacji, sprawdzimy rozmiar */ }
            return r;
        }

        /// <summary>Pobiera plik z postępem; sprawdza rozmiar i SHA-512. Wznawia z pamięci podręcznej, jeśli plik jest już poprawny.</summary>
        public static string Download(Release r, Action<double, string> progress, CancellationToken ct)
        {
            string file = Path.Combine(WorkDir, r.AssetName);
            if (File.Exists(file) && new FileInfo(file).Length == r.Size && (r.Sha512 == null || Sha512(file) == r.Sha512))
            {
                progress(1, "Plik już pobrany");
                return file;
            }
            string tmp = file + ".part";
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (var resp = (HttpWebResponse)Req(r.AssetUrl).GetResponse())
                    using (var src = resp.GetResponseStream())
                    using (var dst = File.Create(tmp))
                    {
                        long total = resp.ContentLength > 0 ? resp.ContentLength : r.Size, done = 0;
                        var buf = new byte[1 << 16];
                        var sw = Stopwatch.StartNew();
                        long lastTick = 0;
                        int n;
                        while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            dst.Write(buf, 0, n);
                            done += n;
                            if (sw.ElapsedMilliseconds - lastTick > 120)
                            {
                                lastTick = sw.ElapsedMilliseconds;
                                double speed = done / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                                progress(total > 0 ? (double)done / total : -1, string.Format("Pobieranie {0} / {1} · {2}/s", Mb(done), Mb(total), Mb((long)speed)));
                            }
                        }
                    }
                    if (r.Size > 0 && new FileInfo(tmp).Length != r.Size) throw new Exception("Niepełny plik – spróbuj ponownie.");
                    progress(-1, "Sprawdzanie pliku (SHA-512)…");
                    if (r.Sha512 != null && Sha512(tmp) != r.Sha512) throw new Exception("Suma kontrolna się nie zgadza – plik mógł zostać uszkodzony podczas pobierania.");
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(tmp, file);
                    return file;
                }
                catch (OperationCanceledException) { TryDelete(tmp); throw; }
                catch (Exception e) { last = e; TryDelete(tmp); Thread.Sleep(800); }
            }
            throw new Exception("Pobieranie nie powiodło się: " + (last != null ? last.Message : "?"));
        }

        static string Sha512(string file)
        {
            using (var h = SHA512.Create())
            using (var s = File.OpenRead(file))
                return Convert.ToBase64String(h.ComputeHash(s));
        }

        public static string Mb(long b)
        {
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0") + " KB";
            return (b / 1048576.0).ToString("0.0") + " MB";
        }

        static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }

        // ---------------- wykrywanie ----------------
        public static Installed Find(AppInfo a)
        {
            return Os == OS.Windows ? FindWindows(a) : FindLinux(a);
        }

        static Installed FindWindows(AppInfo a)
        {
            var hives = new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (var hive in hives)
                foreach (var view in views)
                {
                    try
                    {
                        using (var bk = RegistryKey.OpenBaseKey(hive, view))
                        using (var k = bk.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (k == null) continue;
                            foreach (string sub in k.GetSubKeyNames())
                                using (var s = k.OpenSubKey(sub))
                                {
                                    string dn = s == null ? null : s.GetValue("DisplayName") as string;
                                    if (dn == null || !a.DisplayNames.Any(d => dn.Equals(d, StringComparison.OrdinalIgnoreCase) || dn.StartsWith(d + " ", StringComparison.OrdinalIgnoreCase))) continue;
                                    var i = new Installed
                                    {
                                        Version = s.GetValue("DisplayVersion") as string,
                                        Location = s.GetValue("InstallLocation") as string,
                                        Uninstall = (s.GetValue("QuietUninstallString") as string) ?? (s.GetValue("UninstallString") as string),
                                        RegKey = (hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM") + "\\" + sub,
                                    };
                                    if (i.Location != null)
                                        i.Exe = a.ExeNames.Select(x => Path.Combine(i.Location, x)).FirstOrDefault(File.Exists);
                                    if (i.Exe == null)
                                    {
                                        string icon = s.GetValue("DisplayIcon") as string;
                                        if (icon != null) { icon = icon.Split(',')[0].Trim('"'); if (File.Exists(icon)) i.Exe = icon; }
                                    }
                                    if (i.Location == null && i.Exe != null) i.Location = Path.GetDirectoryName(i.Exe);
                                    return i;
                                }
                        }
                    }
                    catch { }
                }
            return null;
        }

        // Linux: AppImage w ~/Applications + skrót w menu; stan w ~/.local/share/JustInstall
        static string LinuxState(AppInfo a) { return Path.Combine(Home, ".local/share/JustInstall/" + a.Id + ".version"); }
        static string LinuxApp(AppInfo a) { return Path.Combine(Home, "Applications/" + a.Product + ".AppImage"); }
        static string LinuxDesktop(AppInfo a) { return Path.Combine(Home, ".local/share/applications/justinstall-" + a.Id + ".desktop"); }
        static string LinuxIcon(AppInfo a) { return Path.Combine(Home, ".local/share/icons/hicolor/512x512/apps/justinstall-" + a.Id + ".png"); }

        static Installed FindLinux(AppInfo a)
        {
            string app = LinuxApp(a);
            if (!File.Exists(app)) return null;
            return new Installed { Version = File.Exists(LinuxState(a)) ? File.ReadAllText(LinuxState(a)).Trim() : "?", Location = Path.GetDirectoryName(app), Exe = app };
        }

        // ---------------- procesy ----------------
        public static Process[] Running(AppInfo a)
        {
            var list = new List<Process>();
            foreach (string exe in a.ExeNames)
                try { list.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe))); } catch { }
            return list.ToArray();
        }

        public static void CloseRunning(AppInfo a)
        {
            foreach (var p in Running(a))
            {
                try { p.CloseMainWindow(); if (!p.WaitForExit(4000)) { p.Kill(); p.WaitForExit(5000); } } catch { }
            }
        }

        // ---------------- instalacja / naprawa ----------------
        /// <param name="dir">folder instalacji (null = domyślny)</param>
        public static void Install(AppInfo a, string file, Release r, string dir, Action<string> status)
        {
            CloseRunning(a);
            if (Os == OS.Windows)
            {
                // instalatory NSIS z electron-buildera: /S = cicho, /D= folder (musi być ostatni i bez cudzysłowów)
                string args = "/S" + (string.IsNullOrEmpty(dir) ? "" : " /D=" + dir);
                status(a.Name == "JustMusic" ? "Instalowanie (instalator dociąga pakiet aplikacji)…" : "Instalowanie…");
                var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, WorkingDirectory = WorkDir });
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception("Instalator zakończył się błędem (kod " + p.ExitCode + ").");
                // instalator mógł jeszcze kończyć – poczekaj na wpis w rejestrze
                for (int i = 0; i < 40 && Find(a) == null; i++) Thread.Sleep(500);
                if (Find(a) == null) throw new Exception("Instalacja nie została wykryta – spróbuj „Napraw”.");
            }
            else
            {
                status("Instalowanie…");
                string app = LinuxApp(a);
                Directory.CreateDirectory(Path.GetDirectoryName(app));
                File.Copy(file, app, true);
                Chmod(app);
                Directory.CreateDirectory(Path.GetDirectoryName(LinuxState(a)));
                File.WriteAllText(LinuxState(a), r.Version);
                WriteDesktop(a, app);
            }
        }

        static void Chmod(string f)
        {
            try { Process.Start(new ProcessStartInfo("chmod", "+x \"" + f + "\"") { UseShellExecute = false, CreateNoWindow = true }).WaitForExit(); } catch { }
        }

        static bool HasFuse2()
        {
            foreach (string d in new[] { "/usr/lib", "/usr/lib64", "/lib", "/lib64", "/usr/lib/x86_64-linux-gnu", "/lib/x86_64-linux-gnu" })
                try { if (Directory.Exists(d) && Directory.GetFiles(d, "libfuse.so.2*").Length > 0) return true; } catch { }
            return false;
        }

        static void WriteDesktop(AppInfo a, string app)
        {
            string icon = LinuxIcon(a);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(icon));
                using (var s = typeof(Apps).Assembly.GetManifestResourceStream(a.Id + "-128.png"))
                using (var f = File.Create(icon)) s.CopyTo(f);
            }
            catch { }
            string env = HasFuse2() ? "" : "env APPIMAGE_EXTRACT_AND_RUN=1 ";
            string desktop = "[Desktop Entry]\nType=Application\nName=" + a.Name + "\nComment=" + a.Description + "\nExec=" + env + "\"" + app + "\" %U\nIcon=" + icon +
                             "\nTerminal=false\nCategories=" + (a.Id == "justmusic" ? "AudioVideo;Audio;Player;" : "Game;") + "\nStartupWMClass=" + a.Product + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(LinuxDesktop(a)));
            File.WriteAllText(LinuxDesktop(a), desktop);
        }

        /// <summary>Naprawa: czyści zepsutą pamięć aktualizacji i instaluje najnowszą wersję na istniejącą.</summary>
        public static void ClearUpdaterCache(AppInfo a)
        {
            string local = Os == OS.Windows ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : Path.Combine(Home, ".cache");
            foreach (string c in a.UpdaterCaches)
                try { string d = Path.Combine(local, c); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }

        // ---------------- odinstalowanie ----------------
        public static void Uninstall(AppInfo a, bool removeData, Action<string> status)
        {
            CloseRunning(a);
            var inst = Find(a);
            if (Os == OS.Windows)
            {
                if (inst != null && inst.Uninstall != null)
                {
                    status("Odinstalowywanie…");
                    string exe, args;
                    SplitCommand(inst.Uninstall, out exe, out args);
                    if (!args.Contains("/S")) args = (args + " /S").Trim();
                    var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    p.WaitForExit();
                    // deinstalator NSIS kopiuje się do TEMP i działa dalej – czekamy, aż zniknie wpis
                    for (int i = 0; i < 120 && Find(a) != null; i++) Thread.Sleep(500);
                    if (Find(a) != null) throw new Exception("Deinstalator nie usunął aplikacji. Zamknij ją i spróbuj ponownie.");
                }
            }
            else
            {
                status("Usuwanie plików…");
                foreach (string f in new[] { LinuxApp(a), LinuxDesktop(a), LinuxIcon(a), LinuxState(a) })
                    TryDelete(f);
            }
            ClearUpdaterCache(a);
            if (removeData)
            {
                status("Usuwanie danych…");
                string roaming = Os == OS.Windows ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : Path.Combine(Home, ".config");
                foreach (string d in a.DataDirs)
                    try { string p = Path.Combine(roaming, d); if (Directory.Exists(p)) Directory.Delete(p, true); } catch { }
            }
        }

        static void SplitCommand(string cmd, out string exe, out string args)
        {
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                exe = cmd.Substring(1, end - 1);
                args = cmd.Substring(end + 1).Trim();
            }
            else
            {
                int i = cmd.ToLowerInvariant().IndexOf(".exe");
                exe = i > 0 ? cmd.Substring(0, i + 4) : cmd;
                args = i > 0 ? cmd.Substring(i + 4).Trim() : "";
            }
        }

        // ---------------- uruchamianie ----------------
        public static void Launch(AppInfo a)
        {
            var i = Find(a);
            if (i == null || i.Exe == null) throw new Exception(a.Name + " nie jest zainstalowany.");
            if (Os == OS.Windows) Process.Start(new ProcessStartInfo(i.Exe) { UseShellExecute = true, WorkingDirectory = i.Location });
            else
            {
                var psi = new ProcessStartInfo(i.Exe) { UseShellExecute = false };
                if (!HasFuse2()) psi.EnvironmentVariables["APPIMAGE_EXTRACT_AND_RUN"] = "1";
                Process.Start(psi);
            }
        }

        public static string DefaultDir(AppInfo a)
        {
            if (Os == OS.Windows) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", a.Product);
            return Path.GetDirectoryName(LinuxApp(a));
        }

        /// <summary>Porównanie wersji 1.2.10 &gt; 1.2.9.</summary>
        public static int Compare(string x, string y)
        {
            var a = Regex.Matches(x ?? "", @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            var b = Regex.Matches(y ?? "", @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int d = (i < a.Length ? a[i] : 0) - (i < b.Length ? b[i] : 0);
                if (d != 0) return d;
            }
            return 0;
        }
    }
}
