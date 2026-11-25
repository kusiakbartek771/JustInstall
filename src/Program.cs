// JustInstall – instalator aplikacji Just (JustMusic, JustMinecraft).
using System;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("JustInstall")]
[assembly: AssemblyDescription("Instalator aplikacji JustMusic i JustMinecraft")]
[assembly: AssemblyCompany("Just")]
[assembly: AssemblyProduct("JustInstall")]
[assembly: AssemblyCopyright("MIT")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: ComVisible(false)]

namespace JustInstall
{
    static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        static void Main()
        {
            // GitHub wymaga TLS 1.2+
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
