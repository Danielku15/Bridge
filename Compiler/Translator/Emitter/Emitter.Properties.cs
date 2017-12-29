using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bridge.Translator
{
    public partial class Emitter : Visitor
    {
        public string Tag
        {
            get;
            set;
        }

        public ILogger Log => this.Translator.Log;

        public EmitterCache Cache
        {
            get;
        }

        public bool IsAssignment
        {
            get;
            set;
        }

        public AssignmentOperatorType AssignmentType
        {
            get;
            set;
        }

        public UnaryOperatorType UnaryOperatorType
        {
            get;
            set;
        }

        public bool IsUnaryAccessor
        {
            get;
            set;
        }

        public Dictionary<string, AstType> Locals
        {
            get;
            set;
        }

        public Dictionary<IVariable, string> LocalsMap
        {
            get;
            set;
        }

        public Dictionary<string, string> LocalsNamesMap
        {
            get;
            set;
        }

        public Stack<Dictionary<string, AstType>> LocalsStack
        {
            get;
            set;
        }

        public int Level
        {
            get;
            set;
        }

        private int _initialLevel;
        public int InitialLevel
        {
            get => this._initialLevel;
            set
            {
                this._initialLevel = value;
                this.ResetLevel();
            }
        }

        public int ResetLevel(int? level = null)
        {
            if (!level.HasValue)
            {
                level = this.InitialLevel;
            }

            if (level < this.InitialLevel && !this.InitPosition.HasValue )
            {
                level = this.InitialLevel;
            }

            if (level < 0)
            {
                level = 0;
            }

            this.Level = level.Value;

            return this.Level;
        }

        public InitPosition? InitPosition
        {
            get;
            set;
        }

        public bool IsNewLine
        {
            get;
            set;
        }

        public bool EnableSemicolon
        {
            get;
            set;
        }

        public int IteratorCount
        {
            get;
            set;
        }

        public int ThisRefCounter
        {
            get;
            set;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public StringBuilder Output
        {
            get;
            set;
        }

        public Stack<IWriter> Writers
        {
            get;
            set;
        }

        public bool Comma
        {
            get;
            set;
        }

        public IMemberResolver Resolver => this.Translator.Resolver;

        public IAssemblyInfo AssemblyInfo => this.Translator.AssemblyInfo;

        public List<IPluginDependency> CurrentDependencies
        {
            get;
            set;
        }

        public IEmitterOutputs Outputs
        {
            get;
            set;
        }

        public IEmitterOutput EmitterOutput
        {
            get;
            set;
        }

        public bool SkipSemiColon
        {
            get;
            set;
        }

        public bool IsAsync
        {
            get;
            set;
        }

        public bool IsYield
        {
            get;
            set;
        }

        public List<string> AsyncVariables
        {
            get;
            set;
        }

        public IAsyncBlock AsyncBlock
        {
            get;
            set;
        }

        public bool ReplaceAwaiterByVar
        {
            get;
            set;
        }

        public bool AsyncExpressionHandling
        {
            get;
            set;
        }

        public AstNode IgnoreBlock
        {
            get;
            set;
        }

        public AstNode NoBraceBlock
        {
            get;
            set;
        }

        public Action BeforeBlock
        {
            get;
            set;
        }

        public IWriterInfo LastSavedWriter
        {
            get;
            set;
        }

        public List<IJumpInfo> JumpStatements
        {
            get;
            set;
        }

        public SwitchStatement AsyncSwitch
        {
            get;
            set;
        }

        public Dictionary<string, bool> TempVariables
        {
            get;
            set;
        }

        public Dictionary<string, string> NamedTempVariables
        {
            get;
            set;
        }

        public Dictionary<string, bool> ParentTempVariables
        {
            get;
            set;
        }

        public ITranslator Translator
        {
            get;
            set;
        }

        public IJsDoc JsDoc
        {
            get;
            set;
        }

        public IType ReturnType
        {
            get;
            set;
        }

        public bool ReplaceJump
        {
            get;
            set;
        }

        public string CatchBlockVariable
        {
            get;
            set;
        }

        public bool StaticBlock
        {
            get;
            set;
        }

        public Dictionary<string, string> NamedFunctions
        {
            get;
            set;
        }

        public Dictionary<IType, Dictionary<string, string>> NamedBoxedFunctions
        {
            get;
            set;
        }

        public bool IsJavaScriptOverflowMode 
            => this.AssemblyInfo.OverflowMode.HasValue && this.AssemblyInfo.OverflowMode == OverflowMode.Javascript;

        public bool IsRefArg
        {
            get;
            set;
        }

        public Dictionary<AnonymousType, IAnonymousTypeConfig> AnonymousTypes
        {
            get;
            set;
        }

        public List<string> AutoStartupMethods
        {
            get;
            set;
        }

        public bool IsAnonymousReflectable
        {
            get; set;
        }

        public string MetaDataOutputName
        {
            get; set;
        }

        public ITypeInfo[] ReflectableTypes
        {
            get; set;
        }

        public Dictionary<string, int> NamespacesCache
        {
            get; set;
        }

        private bool AssemblyJsDocWritten
        {
            get; set;
        }

        public bool ForbidLifting
        {
            get; set;
        }

        public bool DisableDependencyTracking
        {
            get; set;
        }

        public Dictionary<IAssembly, NameRule[]> AssemblyNameRuleCache
        {
            get;
        }

        public Dictionary<ITypeDefinition, NameRule[]> ClassNameRuleCache
        {
            get;
        }

        public Dictionary<IAssembly, CompilerRule[]> AssemblyCompilerRuleCache
        {
            get;
        }

        public Dictionary<ITypeDefinition, CompilerRule[]> ClassCompilerRuleCache
        {
            get;
        }

        public string SourceFileName
        {
            get;
            set;
        }

        public int SourceFileNameIndex
        {
            get;
            set;
        }

        public string LastSequencePoint
        {
            get;
            set;
        }

        public bool InConstructor
        {
            get; set;
        }

        public CompilerRule Rules
        {
            get; set;
        }
    }
}