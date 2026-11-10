using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WindowsStats.Monitoring;
using WindowsStats.Server;
using WindowsStats.UI;

namespace WindowsStats
{
    static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Resolve embedded assemblies (LibreHardwareMonitorLib.dll, HidSharp.dll)
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string dllName = new AssemblyName(resolveArgs.Name).Name + ".dll";
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                using (Stream stream = executingAssembly.GetManifestResourceStream(dllName))
                {
                    if (stream == null)
                    {
                        // Check in subfolder lib if running from source
                        string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", dllName);
                        if (File.Exists(fallback))
                        {
                            return Assembly.LoadFrom(fallback);
                        }
                        return null;
                    }
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    return Assembly.Load(data);
                }
            };

            // 2. Single instance check
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "WindowsStats_SingleInstanceMutex_Global", out createdNew);
            if (!createdNew)
            {
                // Another instance is already running! Open browser and exit
                try
                {
                    Process.Start("http://localhost:8420");
                }
                catch {}
                return;
            }

            // 3. Parse arguments
            int port = 8420;
            bool headless = false;
            bool showControlOnStart = true;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "--tray" || arg == "-t")
                {
                    showControlOnStart = false;
                }
                else if (arg == "--headless" || arg == "-h")
                {
                    headless = true;
                }
                else if ((arg == "--port" || arg == "-p") && i + 1 < args.Length)
                {
                    int p;
                    if (int.TryParse(args[i + 1], out p) && p > 0 && p < 65536)
                    {
                        port = p;
                        i++;
                    }
                }
            }

            // 4. Initialize core components
            HardwareMonitor monitor = new HardwareMonitor(1000);
            monitor.Start();

            HttpServer server = new HttpServer(port, monitor);
            server.Start();

            if (headless)
            {
                Console.WriteLine("WindowsStats running in headless mode on port " + port);
                Console.WriteLine("Press Ctrl+C to exit.");
                ManualResetEvent waitHandle = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) => {
                    e.Cancel = true;
                    waitHandle.Set();
                };
                waitHandle.WaitOne();

                server.Dispose();
                monitor.Dispose();
                return;
            }

            // 5. Run as Windows Forms Tray Application
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TrayApplicationContext context = new TrayApplicationContext(monitor, server, port, showControlOnStart);
            Application.Run(context);
        }
    }
}
