using System.Collections.Generic;

namespace Bridge.Contract
{
    public interface IPlugin
    {
        ILogger Logger { get; set; }

        IEnumerable<string> GetConstructorInjectors(IConstructorBlock constructorBlock);

        void OnInvocation(IInvocationInterceptor interceptor);

        void OnReference(IReferenceInterceptor interceptor);

        bool HasConstructorInjectors(IConstructorBlock constructorBlock);

        void OnConfigRead(IAssemblyInfo config);

        void BeforeEmit(ITranslator translator);

        void AfterEmit(ITranslator translator);

        void AfterOutput(ITranslator translator, string outputPath, bool nocore);

        void BeforeTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types);

        void AfterTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types);

        void BeforeTypeEmit(ITranslator translator, ITypeInfo type);

        void AfterTypeEmit(ITranslator translator, ITypeInfo type);
    }
}