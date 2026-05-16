using System;
using System.IO;

namespace fflauncher
{
    public class Logger
    {
        private readonly string logPath;
        private readonly object lockObj = new object();

        public Logger(string logPath)
        {
            this.logPath = logPath;
        }

        public void Log(string message, string level = "INFO")
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
            lock (lockObj)
            {
                File.AppendAllText(logPath, logEntry);
            }
        }

        public void Clear()
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }
}