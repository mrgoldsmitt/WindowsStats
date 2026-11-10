using System;
using System.IO;

namespace WindowsStats
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logPath;

        static Logger()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsStats");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "debug.log");
            }
            catch {}
        }

        public static void Log(string msg)
        {
            try
            {
                lock (_lock)
                {
                    if (_logPath != null)
                    {
                        File.AppendAllText(_logPath, string.Format("[{0:HH:mm:ss.fff}] {1}\r\n", DateTime.Now, msg));
                    }
                }
            }
            catch {}
        }
    }
}
