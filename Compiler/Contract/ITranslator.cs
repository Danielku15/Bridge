using ICSharpCode.NRefactory.CSharp;
using Mono.Cecil;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Contract
{
    public interface ITranslator : ILogger
    {
        AssemblyDefinition AssemblyDefinition
        {
            get;
            set;
        }

        IAssemblyInfo AssemblyInfo
        {
            get;
            set;
        }

        string AssemblyLocation
        {
            get;
        }

        string BridgeLocation
        {
            get;
            set;
        }

        ProjectProperties ProjectProperties
        {
            get;
            set;
        }

        string DefaultNamespace
        {
            get;
            set;
        }

        string Location
        {
            get;
        }

        string MSBuildVersion
        {
            get;
            set;
        }

        ILogger Log
        {
            get;
        }

        TranslatorOutput Outputs
        {
            get;
        }

        bool Rebuild
        {
            get;
            set;
        }

        IList<string> SourceFiles
        {
            get;
        }

        ITypeInfoCollection Types
        {
            get;
            set;
        }

        IPlugins Plugins
        {
            get;
            set;
        }

        AstNode EmitNode
        {
            get;
            set;
        }


        bool FolderMode
        {
            get;
            set;
        }

        string Source
        {
            get;
            set;
        }

        bool Recursive
        {
            get;
            set;
        }

        IEnumerable<AssemblyDefinition> References
        {
            get;
        }

        /// <summary>
        /// Indicates whether strict mode will be added to generated script files
        /// </summary>
        bool NoStrictMode
        {
            get;
        }

        IEmitter Emitter
        {
            get;
        }

        IMemberResolver Resolver
        {
            get;
        }

        VersionContext GetVersionContext();

        EmitterException CreateExceptionFromLastNode();

        void Save(string path, string defaultFileName);
        void Translate();
        ITypeInfo GetTypeInfo(AstNode type);
        bool IsCurrentAssembly(IAssembly assembly);
        bool IsInCurrentAssembly(IEntity entity);
    }
}