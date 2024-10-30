using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WeldingService
{
    public static class Logger
    {
        private class LoggerMessage
        {
            public DateTime Timestamp;
            public LogLevel LogLevel;
            public string Message;
            public int ThreadId;
        }

        private static List<LoggerMessage> m_queue;
        private static EventWaitHandle m_waitHandle;
        private static Thread m_logThread;
        private static bool m_stop;
        private static bool m_log_console;
        private static LogLevel m_log_level = LogLevel.Debug;


        private static void InternalLog(LogLevel level, string msg)
        {
            if (m_queue == null)
                throw new InvalidOperationException("Logger not initialized.");

            // Check level
            switch (m_log_level)
            {
                case LogLevel.Debug:
                    // log everything
                    break;
                case LogLevel.Notice:
                    // Don't log Debug
                    if (level == LogLevel.Debug)
                        return;
                    break;
                case LogLevel.Warning:
                    // Don't log Debug, Notice
                    if (level == LogLevel.Debug || level == LogLevel.Notice)
                        return;
                    break;
                case LogLevel.Error:
                    // Don't log Debug, Notice, Warning
                    if (level == LogLevel.Debug || level == LogLevel.Notice || level == LogLevel.Warning)
                        return;
                    break;
            }

            var message = new LoggerMessage
            {
                Timestamp = DateTime.Now,
                LogLevel = level,
                Message = msg,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
            };

            lock (m_queue)
            {
                m_queue.Add(message);
                m_waitHandle.Set();
            }
        }

        private static void LoggerWorker(string logDir)
        {
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            while (!m_stop)
            {
                if (!m_waitHandle.WaitOne(1000))
                    continue;

                Thread.Sleep(1000);

                LoggerMessage[] copy;
                lock (m_queue)
                {
                    copy = new LoggerMessage[m_queue.Count];
                    m_queue.CopyTo(copy, 0);
                    m_queue.Clear();
                    m_waitHandle.Reset();
                }

                if (copy.Length == 0)
                    continue;

                var sb = new StringBuilder();
                foreach (var msg in copy.OrderBy(m => m.Timestamp))
                {
                    sb.AppendLine(String.Format("{0:G} {1:X2} {2} {3}", msg.Timestamp, msg.ThreadId, msg.LogLevel, msg.Message));
                }

                var file = Path.Combine(logDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                using (var stream = File.Open(file, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    stream.Write(bytes, 0, bytes.Length);
                }

                // Console?
                if (m_log_console)
                {
                    Console.WriteLine(sb.ToString());
                }
            }
        }

        private static void FormatException(Exception ex, StringBuilder msg, int depth = 0)
        {
            var prefix = "\t";
            for (var i = 0; i < depth; ++i)
                prefix += "\t";

            msg.AppendFormat(prefix + "Type: {0}\n", ex.GetType().FullName);
            msg.AppendFormat(prefix + "Source: {0}\n", ex.Source);
            msg.AppendFormat(prefix + "Message: {0}\n", ex.Message);

            if (!String.IsNullOrEmpty(ex.StackTrace))
            {
                msg.AppendLine(prefix + "Stack trace:");
                using (var reader = new StringReader(ex.StackTrace))
                {
                    string s;
                    while ((s = reader.ReadLine()) != null)
                        msg.AppendLine(prefix + s);
                }
                msg.AppendLine(prefix + "-- Stack trace end");
            }

            if (ex.InnerException == null) return;

            msg.AppendLine();
            msg.AppendLine(prefix + "Inner exception information");
            FormatException(ex.InnerException, msg, depth + 1);
        }



        public static void Start(string logDir, string logLevel, bool logConsole)
        {
            // Notice by default
            m_log_level = logLevel == "Error" ? LogLevel.Error
                : logLevel == "Warning" ? LogLevel.Warning
                : logLevel == "Notice" ? LogLevel.Notice
                : logLevel == "Debug" ? LogLevel.Debug
                : LogLevel.Notice;

            m_log_console = logConsole;

            m_waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            m_queue = new List<LoggerMessage>();
            m_stop = false;
            m_logThread = new Thread(() => LoggerWorker(logDir)) { Name = "Logger worker" };
            m_logThread.Start();
            Log(LogLevel.Notice, "Logger started");
        }

        public static void Stop()
        {
            Log(LogLevel.Notice, "Logger stopped");
            m_stop = true;
            m_logThread.Join();
        }

        public static void Log(LogLevel level, string msg)
        {
            InternalLog(level, msg);
        }

        public static void Log(LogLevel level, string fmt, params object[] args)
        {
            InternalLog(level, String.Format(fmt, args));
        }

        public static void LogException(Exception e, string str)
        {
            Log(LogLevel.Error, "{2}: Exception of type {0} thrown: {1}", e.GetType().FullName, e.Message, str);
            var msg = new StringBuilder();
            msg.AppendLine(str);
            FormatException(e, msg);

            Log(LogLevel.Error, msg.ToString());
        }
    }

    public enum LogLevel
    {
        Debug,
        Notice,
        Warning,
        Error
    }
}
