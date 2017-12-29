using Bridge.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bridge.Translator.Logging
{
    public class FileLoggerWriter : ILogger, IDisposable
    {
        private class BufferedMessage
        {
            public LoggerLevel LoggerLevel;
            public string Message;
            public bool UseWriteLine;
        }

        private const string LoggerFileName = "bridge.log";
        private const int LoggerFileMaxLength = 16 * 1024 * 1024;
        private const int MaxInitializationCount = 5;

        private bool IsInitializedSuccessfully { get; set; }
        private int InitializationCount { get; set; }
        private bool IsCleanedUp { get; set; }
        private Queue<BufferedMessage> Buffer { get; }

        public bool AlwaysLogErrors => false;
        public string BaseDirectory { get; private set; }
        public string FullName { get; private set; }
        public string FileName { get; private set; }
        public long MaxLogFileSize { get; private set; }

        public bool BufferedMode { get; set; }
        public LoggerLevel LoggerLevel { get; set; }

        public FileLoggerWriter(string baseDir, string fileName, long? maxSize)
        {
            this.Buffer = new Queue<BufferedMessage>();
            this.SetParameters(baseDir, fileName, maxSize);
        }

        public FileLoggerWriter() : this(null, null, null)
        {
        }

        public FileLoggerWriter(string baseDir) : this(baseDir, null, null)
        {
        }

        public void SetParameters(string baseDir, string fileName, long? maxSize)
        {
            this.IsInitializedSuccessfully = false;
            this.IsCleanedUp = false;
            this.InitializationCount = 0;

            this.BaseDirectory = string.IsNullOrEmpty(baseDir)
                ? null
                : (new FileHelper()).GetDirectoryAndFilenamePathComponents(baseDir)[0];

            this.FileName = string.IsNullOrEmpty(fileName) ? LoggerFileName : Path.GetFileName(fileName);
            this.MaxLogFileSize = !maxSize.HasValue || maxSize.Value <= 0 ? LoggerFileMaxLength : maxSize.Value;

            this.FullName = string.IsNullOrEmpty(this.BaseDirectory)
                ? this.FileName
                : Path.Combine(this.BaseDirectory, this.FileName);
        }

        private bool CanBeInitialized => this.InitializationCount < MaxInitializationCount;

        private bool CheckDirectoryAndLoggerSize()
        {
            if (this.IsInitializedSuccessfully)
            {
                return true;
            }

            if (!this.CanBeInitialized)
            {
                this.Buffer.Clear();
                return false;
            }

            this.InitializationCount++;

            try
            {
                var loggerFile = new FileInfo(this.FullName);
                loggerFile.Directory.Create();

                // Uncomment this lines if max file size logic required and handle fileMode in Flush()
                //if (loggerFile.Exists && loggerFile.Length > MaxLogFileSize)
                //{
                //    loggerFile.Delete();
                //}

                this.IsInitializedSuccessfully = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return this.IsInitializedSuccessfully;
        }

        private void WriteOrBuffer(LoggerLevel level, string message, bool useWriteLine)
        {
            if (!(this.IsInitializedSuccessfully || this.CanBeInitialized))
            {
                return;
            }

            this.Buffer.Enqueue(new BufferedMessage() { LoggerLevel = level, Message = message, UseWriteLine = useWriteLine });

            if (this.BufferedMode)
            {
                return;
            }

            this.Flush();
        }

        private void WriteOrBufferLine(LoggerLevel level, string s)
        {
            this.WriteOrBuffer(level, s, true);
        }

        private bool CheckLoggerLevel(LoggerLevel level)
        {
            return level <= this.LoggerLevel;
        }

        public void Flush()
        {
            if (!this.CheckDirectoryAndLoggerSize())
            {
                return;
            }

            if (this.Buffer.Any(x => this.CheckLoggerLevel(x.LoggerLevel)))
            {
                try
                {
                    var fileMode = FileMode.Append;

                    if (!this.IsCleanedUp)
                    {
                        fileMode = FileMode.Create;
                        this.IsCleanedUp = true;
                    }

                    FileInfo file = new FileInfo(this.FullName);

                    using (Stream stream = file.Open(fileMode, FileAccess.Write, FileShare.Write | FileShare.ReadWrite | FileShare.Delete))
                    {
                        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            stream.Position = stream.Length;

                            while (this.Buffer.Count > 0)
                            {
                                var message = this.Buffer.Dequeue();

                                if (!this.CheckLoggerLevel(message.LoggerLevel))
                                {
                                    continue;
                                }

                                if (message.UseWriteLine)
                                {
                                    writer.WriteLine(message.Message);
                                }
                                else
                                {
                                    writer.Write(message.Message);
                                }

                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                this.Buffer.Clear();
            }
        }

        public void Error(string message)
        {
            this.WriteOrBufferLine(LoggerLevel.Error, message);
        }

        public void Error(string message, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            this.Error(message);
        }

        public void Warn(string message)
        {
            this.WriteOrBufferLine(LoggerLevel.Warning, message);
        }

        public void Info(string message)
        {
            this.WriteOrBufferLine(LoggerLevel.Info, message);
        }

        public void Trace(string message)
        {
            this.WriteOrBufferLine(LoggerLevel.Trace, message);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        ~FileLoggerWriter()
        {
            this.Dispose(false);
        }
    }
}