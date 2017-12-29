using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Mono.Cecil;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public partial class Translator
    {
        public IAssemblyInfo AssemblyInfo
        {
            get;
            set;
        }

        public AssemblyDefinition AssemblyDefinition
        {
            get;
            set;
        }

        public virtual IEnumerable<AssemblyDefinition> References
        {
            get;
            set;
        }

        public string BridgeLocation
        {
            get;
            set;
        }

        public string Location
        {
            get;
            protected set;
        }

        public string AssemblyLocation
        {
            get;
            protected set;
        }

        public ProjectProperties ProjectProperties
        {
            get;
            set;
        }

        public string DefaultNamespace
        {
            get;
            set;
        }

        public string BuildArguments
        {
            get;
            set;
        }

        public string MSBuildVersion
        {
            get; set;
        } = "4.0.30319";

        public IList<string> SourceFiles
        {
            get;
            protected set;
        }

        public IList<ParsedSourceFile> ParsedSourceFiles
        {
            get;
            protected set;
        }

        public bool Rebuild
        {
            get; set;
        } = true;

        public TranslatorOutput Outputs
        {
            get;
            protected set;
        }

        public IPlugins Plugins
        {
            get;
            set;
        }

        public ITypeInfoCollection Types
        {
            get;
            set;
        }

        public AstNode EmitNode
        {
            get;
            set;
        }

        public bool FolderMode
        {
            get;
            set;
        }

        public string Source
        {
            get;
            set;
        }

        public bool Recursive
        {
            get;
            set;
        }

        public bool FromTask
        {
            get;
            set;
        }

        public bool NoStrictMode
        {
            get;
            set;
        }

        public string[] SkipPluginAssemblies
        {
            get;
            set;
        }

        public OverflowMode? OverflowMode
        {
            get;
            set;
        }

        public HashSet<string> ExtractedScripts
        {
            get; set;
        } = new HashSet<string>();

        public IEmitter Emitter
        {
            get;
            private set;
        }

        public IMemberResolver Resolver
        {
            get;
            private set;
        }

    }
}