using System;
using System.IO;
using System.Text;

namespace Macro.Utils
{
    public static class DebugLogger
    {
        private static string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Debug_JumpTarget_Trace.txt");
        private static object _lock = new object();

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(LogFilePath))
                        File.Delete(LogFilePath);
                }
                catch { }
            }
        }
    }
}
