using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using System.Collections.Generic;

namespace Bridge.Contract
{
    public interface IPlugins
    {
        void OnConfigRead(IAssemblyInfo config);

        IEnumerable<string> GetConstructorInjectors(IConstructorBlock constructorBlock);

        IInvocationInterceptor OnInvocation(IAbstractEmitterBlock block, InvocationExpression expression, InvocationResolveResult resolveResult);

        IReferenceInterceptor OnReference(IAbstractEmitterBlock block, MemberReferenceExpression expression, MemberResolveResult resolveResult);

        bool HasConstructorInjectors(IConstructorBlock constructorBlock);

        IEnumerable<IPlugin> Parts
        {
            get;
        }

        void BeforeEmit(ITranslator translator);

        void AfterEmit(ITranslator translator);

        void BeforeTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types);

        void AfterTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types);

        void BeforeTypeEmit(ITranslator translator, ITypeInfo type);

        void AfterTypeEmit(ITranslator translator, ITypeInfo type);

        void AfterOutput(ITranslator translator, string outputPath, bool nocore);
    }
}