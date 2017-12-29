using Bridge.Contract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bridge.Translator.Logging
{
    public class Logger : ILogger
    {
        public bool AlwaysLogErrors => false;
        public string Name { get; set; }
        public List<ILogger> LoggerWriters { get; }
        public bool UseTimeStamp { get; set; }

        private bool _bufferedMode;

        public bool BufferedMode
        {
            get => this._bufferedMode;
            set
            {
                this.LoggerWriters.ForEach(x => x.BufferedMode = value);
                this._bufferedMode = value;
            }
        }

        private LoggerLevel _loggerLevel;

        public LoggerLevel LoggerLevel
        {
            get => this._loggerLevel;
            set
            {
                if (value < LoggerLevel.None)
                {
                    value = LoggerLevel.None;
                }

                this._loggerLevel = value;

                this.LoggerWriters.ForEach(x => x.LoggerLevel = value);
            }
        }

        public Logger(string name, bool useTimeStamp, LoggerLevel loggerLevel, bool bufferedMode, params ILogger[] loggerWriters)
        {
            this.Name = name ?? string.Empty;

            this.LoggerWriters = loggerWriters.Where(x => x != null).ToList();

            this.UseTimeStamp = useTimeStamp;
            this.LoggerLevel = loggerLevel;
            this.BufferedMode = bufferedMode;
        }

        public Logger(string name, bool useTimeStamp, params ILogger[] loggers)
            : this(name, useTimeStamp, LoggerLevel.None, false, loggers)
        {
        }

        public FileLoggerWriter GetFileLogger()
        {
            return this.LoggerWriters.OfType<FileLoggerWriter>().FirstOrDefault();
        }

        public void Flush()
        {
            this.LoggerWriters.ForEach(x => x.Flush());
        }

        public void Error(string message)
        {
            foreach (var logger in this.LoggerWriters)
            {
                var alwaysLogErrors = logger.AlwaysLogErrors;

                string wrappedMessage;
                if ((wrappedMessage = this.CheckIfCanLog(message, LoggerLevel.Error, alwaysLogErrors)) != null)
                {
                    logger.Error(wrappedMessage);
                }
            }

        }

        public void Error(string message, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            foreach (var logger in this.LoggerWriters)
            {
                var alwaysLogErrors = logger.AlwaysLogErrors;

                string wrappedMessage;
                if ((wrappedMessage = this.CheckIfCanLog(message, LoggerLevel.Error, alwaysLogErrors)) != null)
                {
                    logger.Error(wrappedMessage, file, lineNumber, columnNumber, endLineNumber, endColumnNumber);
                }
            }
        }

        public void Warn(string message)
        {
            string wrappedMessage;

            if ((wrappedMessage = this.CheckIfCanLog(message, LoggerLevel.Warning)) != null)
            {
                foreach (var logger in this.LoggerWriters)
                {
                    logger.Warn(wrappedMessage);
                }
            }
        }

        public void Info(string message)
        {
            string wrappedMessage;

            if ((wrappedMessage = this.CheckIfCanLog(message, LoggerLevel.Info)) != null)
            {
                foreach (var logger in this.LoggerWriters)
                {
                    logger.Info(wrappedMessage);
                }
            }
        }

        public void Trace(string message)
        {
            string wrappedMessage;

            if ((wrappedMessage = this.CheckIfCanLog(message, LoggerLevel.Trace)) != null)
            {
                foreach (var logger in this.LoggerWriters)
                {
                    logger.Trace(wrappedMessage);
                }
            }
        }

        private string CheckIfCanLog(string message, LoggerLevel level, bool alwaysLogErrors = false)
        {
            //if (this.LoggerLevel >= level)
            //{
            //    return null;
            //}

            return this.WrapMessage(message, level, alwaysLogErrors);
        }

        private string WrapMessage(string message, LoggerLevel logLevel, bool alwaysLogErrors)
        {
            if ((this.LoggerLevel <= 0 && !alwaysLogErrors)
                || string.IsNullOrEmpty(message))
            {
                return null;
            }

            if (!this.UseTimeStamp)
            {
                return message;
            }

            var d = DateTime.Now.ToString("s") + ":" + DateTime.Now.Millisecond.ToString("D3") + " ";

            string wrappedMessage = $"{d}\t{logLevel}\t{this.Name}\t{message}";

            return wrappedMessage;
        }
    }
}