using System;
using System.IO;
using System.Text;

namespace Macro.Utils
{
    public static class DebugLogger
    {
        private static string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Debug_JumpTarget_Trace.txt");
        private static object _lock = new object();
        private const long MaxLogSize = 1024 * 1024; // 1MB

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    FileInfo fi = new FileInfo(LogFilePath);
                    if (fi.Exists && fi.Length > MaxLogSize)
                    {
                        File.Delete(LogFilePath);
                    }

                    string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DebugLogger failed: {ex.Message}");
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
                catch (Exception ex) 
                { 
                    System.Diagnostics.Debug.WriteLine($"DebugLogger Clear failed: {ex.Message}");
                }
            }
        }
    }
}
