using System.Diagnostics;
using DiscordRPC.Logging;

namespace MusicBeePlugin
{
    public class DebugLogger : ILogger
    {
        public LogLevel Level { get; set; }

        public DebugLogger(LogLevel level)
        {
            Level = level;
        }

        public void Error(string message, params object[] args)
        {
            if (Level > LogLevel.Error) return;
            Log(message, args);
        }

        public void Info(string message, params object[] args)
        {
            if (Level > LogLevel.Info) return;
            Log(message, args);
        }

        public void Trace(string message, params object[] args)
        {
            if (Level > LogLevel.Trace) return;
            Log(message, args);
        }

        public void Warning(string message, params object[] args)
        {
            if (Level > LogLevel.Warning) return;
            Log(message, args);
        }

        private void Log(string msg, params object[] args)
        {
            Debug.WriteLine("" + Level.ToString() + ": " + msg, args);
        }
    }
}
