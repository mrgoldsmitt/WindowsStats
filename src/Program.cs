using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsStats.Monitoring;
using WindowsStats.Server;
using WindowsStats.UI;

namespace WindowsStats
{
    public static class Program
    {
        private static Mutex _singleInstanceMutex;

        public static void ReleaseSingleInstanceMutex()
        {
            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch {}
        }

        private static void LogCrash(string context, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsStats", "crash.log");
                string dir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string entry = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}\r\nStack: {3}\r\n\r\n",
                    DateTime.Now, context, ex != null ? ex.Message : "Unknown", ex != null ? ex.StackTrace : "");
                File.AppendAllText(logPath, entry);
            }
            catch {}
        }

        [STAThread]
        static void Main(string[] args)
        {
            Logger.Log("Program.Main started. Args: " + string.Join(" ", args));

            // 0. Global unhandled exception shields (prevent app from terminating unexpectedly)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => {
                Logger.Log("FATAL Application.ThreadException: " + e.Exception);
                LogCrash("Application.ThreadException", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Logger.Log("FATAL AppDomain.UnhandledException: " + e.ExceptionObject);
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logger.Log("FATAL TaskScheduler.UnobservedTaskException: " + e.Exception);
                LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

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

            // 2. Resilient Single instance check
            bool createdNew = false;
            try
            {
                _singleInstanceMutex = new Mutex(true, "WindowsStats_SingleInstanceMutex_Global", out createdNew);
                if (!createdNew)
                {
                    // Wait up to 1000ms in case previous instance is restarting/closing
                    if (_singleInstanceMutex.WaitOne(1000, false))
                    {
                        createdNew = true;
                    }
                }
            }
            catch
            {
                createdNew = true; // Fallback if mutex ownership check fails
            }

            if (!createdNew)
            {
                // Another active instance is running! Open browser and exit
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
            HardwareMonitor monitor = new HardwareMonitor(2000);
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

            Logger.Log("Creating TrayApplicationContext...");
            TrayApplicationContext context = new TrayApplicationContext(monitor, server, port, showControlOnStart);
            Logger.Log("Starting Application.Run(context)...");
            Application.Run(context);
            Logger.Log("Application.Run(context) has exited!");
        }
    }
}
