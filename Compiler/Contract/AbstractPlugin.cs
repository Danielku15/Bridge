using System.Collections.Generic;

namespace Bridge.Contract
{
    public abstract class AbstractPlugin : IPlugin
    {
        public virtual ILogger Logger { get; set; }

        public virtual IEnumerable<string> GetConstructorInjectors(IConstructorBlock constructorBlock)
        {
            return null;
        }

        public virtual void OnInvocation(IInvocationInterceptor interceptor)
        {
        }

        public virtual void OnReference(IReferenceInterceptor interceptor)
        {
        }

        public virtual bool HasConstructorInjectors(IConstructorBlock constructorBlock)
        {
            return false;
        }

        public virtual void OnConfigRead(IAssemblyInfo config)
        {
        }

        public virtual void BeforeEmit(ITranslator translator)
        {
        }

        public virtual void AfterEmit(ITranslator translator)
        {
        }

        public virtual void BeforeTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types)
        {
        }

        public virtual void AfterTypesEmit(ITranslator translator, IEnumerable<ITypeInfo> types)
        {
        }

        public virtual void BeforeTypeEmit(ITranslator translator, ITypeInfo type)
        {
        }

        public virtual void AfterTypeEmit(ITranslator translator, ITypeInfo type)
        {
        }

        public virtual void AfterOutput(ITranslator translator, string outputPath, bool nocore)
        {
        }
    }
}