using System.IO;
using System.Text;

namespace fffrontend
{
    public class Logger : IDisposable
    {
        private readonly string logPath;
        private readonly object lockObj = new object();
        private StreamWriter? writer;

        public Logger(string logPath)
        {
            this.logPath = logPath;
            writer = CreateWriter();
        }

        private StreamWriter CreateWriter()
        {
            var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            return new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        }

        public void Log(string message, string level = "INFO")
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
            lock (lockObj)
            {
                writer?.Write(logEntry);
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                writer?.Dispose();
                writer = null;
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
                writer = CreateWriter();
            }
        }

        public void Dispose()
        {
            lock (lockObj)
            {
                writer?.Dispose();
                writer = null;
            }
        }
    }
}