using Bridge.Contract;
using System;

namespace Bridge.Translator
{
    public class TranslatorException : Exception, IVisitorException
    {
        public TranslatorException(string message)
            : base(message)
        {
        }

        public static TranslatorException Create(string format, params object[] args)
        {
            return new TranslatorException(String.Format(format, args));
        }

        public static void Throw(string format, params object[] args)
        {
            throw Create(format, args);
        }
    }
}