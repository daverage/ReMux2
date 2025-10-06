using System;
using System.IO;

namespace ReMux2
{
    public class FileLogger
    {
        private readonly string _logFilePath;

        public FileLogger(string logFileName)
        {
            _logFilePath = Path.Combine(AppContext.BaseDirectory, logFileName);
        }

        public void Log(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"[{DateTime.Now:G}] {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Ignore exceptions to prevent logging from crashing the app
            }
        }
    }
}