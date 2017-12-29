using Bridge.Contract;

namespace Bridge.Translator
{
    public partial class Translator
    {
        public ILogger Log
        {
            get;
        }

        bool ILogger.AlwaysLogErrors => this.Log.AlwaysLogErrors;

        bool ILogger.BufferedMode
        {
            get => this.Log.BufferedMode;
            set => this.Log.BufferedMode = value;
        }

        void ILogger.Flush() => this.Log.Flush();

        LoggerLevel ILogger.LoggerLevel
        {
            get => this.Log.LoggerLevel;
            set => this.Log.LoggerLevel = value;
        }

        void ILogger.Warn(string message) => this.Log.Warn(message);

        void ILogger.Error(string message) => this.Log.Error(message);

        void ILogger.Error(string message, string file, int lineNumber, int columnNumber, int endLineNumber,
            int endColumnNumber)
            => this.Log.Error(message, file, lineNumber, columnNumber, endLineNumber, endColumnNumber);

        void ILogger.Info(string message) => this.Log.Info(message);

        void ILogger.Trace(string message) => this.Log.Trace(message);
    }
}