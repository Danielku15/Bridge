using Bridge.Contract;
using System;

namespace Bridge.Translator.Logging
{
    public class ConsoleLoggerWriter : ILogger
    {
        public bool AlwaysLogErrors => true;

        public bool BufferedMode { get; set; }

        public LoggerLevel LoggerLevel { get; set; }

        public void Flush()
        {
        }

        public void Error(string message)
        {
            this.OutputMessage(message, ConsoleColor.Red, LoggerLevel.Error);
        }

        public void Error(string message, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            this.Error(message);
        }

        public void Warn(string message)
        {
            this.OutputMessage(message, ConsoleColor.DarkYellow, LoggerLevel.Warning);
        }

        public void Info(string message)
        {
            this.OutputMessage(message, ConsoleColor.Gray, LoggerLevel.Info);
        }

        public void Trace(string message)
        {
            this.OutputMessage(message, ConsoleColor.Green, LoggerLevel.Trace);
        }

        private void OutputMessage(string message, ConsoleColor color, LoggerLevel level)
        {
            if (!this.CheckLoggerLevel(level))
            {
                return;
            }

            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private bool CheckLoggerLevel(LoggerLevel level)
        {
            return (level <= this.LoggerLevel) || (level == LoggerLevel.Error && this.AlwaysLogErrors);
        }
    }
}