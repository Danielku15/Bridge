using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System;
using System.Collections.Generic;

namespace Bridge.Contract
{
    public interface IEmitter : IAstVisitor
    {
        string Tag
        {
            get;
            set;
        }

        IAssemblyInfo AssemblyInfo
        {
            get;
        }

        AssignmentOperatorType AssignmentType
        {
            get;
            set;
        }

        UnaryOperatorType UnaryOperatorType
        {
            get;
            set;
        }

        bool IsUnaryAccessor
        {
            get;
            set;
        }

        IAsyncBlock AsyncBlock
        {
            get;
            set;
        }

        bool AsyncExpressionHandling
        {
            get;
            set;
        }

        SwitchStatement AsyncSwitch
        {
            get;
            set;
        }

        List<string> AsyncVariables
        {
            get;
            set;
        }

        bool Comma
        {
            get;
            set;
        }

        List<IPluginDependency> CurrentDependencies
        {
            get;
            set;
        }

        List<TranslatorOutputItem> Emit();

        bool EnableSemicolon
        {
            get;
            set;
        }

        string GetEntityName(EntityDeclaration entity);

        string GetParameterName(ParameterDeclaration entity);

        NameSemantic GetNameSemantic(IEntity member);

        string GetEntityName(IEntity member);

        string GetTypeName(ITypeDefinition typeDefinition);
        string GetCustomTypeName(ITypeDefinition type, bool excludeNs);

        string GetLiteralEntityName(IEntity member);

        string GetInline(EntityDeclaration method);

        string GetInline(IEntity entity);

        Tuple<bool, bool, string> GetInlineCode(InvocationExpression node);

        Tuple<bool, bool, string> GetInlineCode(MemberReferenceExpression node);

        bool IsForbiddenInvocation(InvocationExpression node);

        ITypeDefinition GetTypeDefinition();

        ITypeDefinition GetTypeDefinition(AstType reference);

        AstNode IgnoreBlock
        {
            get;
            set;
        }

        bool IsAssignment
        {
            get;
            set;
        }

        bool IsAsync
        {
            get;
            set;
        }

        bool IsYield
        {
            get;
            set;
        }

        bool IsNativeMember(string fullName);

        bool IsNewLine
        {
            get;
            set;
        }

        int IteratorCount
        {
            get;
            set;
        }

        List<IJumpInfo> JumpStatements
        {
            get;
            set;
        }

        IWriterInfo LastSavedWriter
        {
            get;
            set;
        }

        int Level
        {
            get;
        }

        int InitialLevel
        {
            get;
        }

        int ResetLevel(int? level = null);

        InitPosition? InitPosition
        {
            get;
            set;
        }

        Dictionary<string, AstType> Locals
        {
            get;
            set;
        }

        Dictionary<IVariable, string> LocalsMap
        {
            get;
            set;
        }

        Dictionary<string, string> LocalsNamesMap
        {
            get;
            set;
        }

        Stack<Dictionary<string, AstType>> LocalsStack
        {
            get;
            set;
        }

        ILogger Log
        {
            get;
        }

        AstNode NoBraceBlock
        {
            get;
            set;
        }

        Action BeforeBlock
        {
            get;
            set;
        }

        System.Text.StringBuilder Output
        {
            get;
            set;
        }

        string SourceFileName
        {
            get;
            set;
        }

        int SourceFileNameIndex
        {
            get;
            set;
        }

        string LastSequencePoint
        {
            get;
            set;
        }

        IEmitterOutputs Outputs
        {
            get;
            set;
        }

        IEmitterOutput EmitterOutput
        {
            get;
            set;
        }

        bool ReplaceAwaiterByVar
        {
            get;
            set;
        }

        IMemberResolver Resolver
        {
            get;
        }

        bool SkipSemiColon
        {
            get;
            set;
        }

        int ThisRefCounter
        {
            get;
            set;
        }

        ITypeInfo TypeInfo
        {
            get;
            set;
        }

        Stack<IWriter> Writers
        {
            get;
            set;
        }

        void Throw(AstNode node, string message = null);

        EmitterCache Cache
        {
            get;
        }

        string GetFieldName(FieldDeclaration field);

        string GetEventName(EventDeclaration evt);

        Dictionary<string, bool> TempVariables
        {
            get;
            set;
        }

        Dictionary<string, string> NamedTempVariables
        {
            get;
            set;
        }

        Dictionary<string, bool> ParentTempVariables
        {
            get;
            set;
        }

        ITranslator Translator
        {
            get;
            set;
        }

        void InitEmitter();

        IJsDoc JsDoc
        {
            get;
            set;
        }

        IType ReturnType
        {
            get;
            set;
        }

        string GetEntityNameFromAttr(IEntity member, bool setter = false);

        bool ReplaceJump
        {
            get;
            set;
        }

        string CatchBlockVariable
        {
            get;
            set;
        }

        Dictionary<string, string> NamedFunctions
        {
            get; set;
        }

        Dictionary<IType, Dictionary<string, string>> NamedBoxedFunctions
        {
            get; set;
        }

        bool StaticBlock
        {
            get;
            set;
        }

        bool IsJavaScriptOverflowMode
        {
            get;
        }

        bool IsRefArg
        {
            get;
            set;
        }

        Dictionary<AnonymousType, IAnonymousTypeConfig> AnonymousTypes
        {
            get; set;
        }

        List<string> AutoStartupMethods
        {
            get;
            set;
        }

        bool IsAnonymousReflectable
        {
            get; set;
        }

        string MetaDataOutputName
        {
            get; set;
        }

        ITypeInfo[] ReflectableTypes
        {
            get; set;
        }

        Dictionary<string, int> NamespacesCache
        {
            get; set;
        }

        bool DisableDependencyTracking { get; set; }

        void WriteIndented(string s, int? position = null);
        bool ForbidLifting { get; set; }

        Dictionary<IAssembly, NameRule[]> AssemblyNameRuleCache
        {
            get;
        }

        Dictionary<ITypeDefinition, NameRule[]> ClassNameRuleCache
        {
            get;
        }

        Dictionary<IAssembly, CompilerRule[]> AssemblyCompilerRuleCache
        {
            get;
        }

        Dictionary<ITypeDefinition, CompilerRule[]> ClassCompilerRuleCache
        {
            get;
        }

        bool InConstructor { get; set; }
        CompilerRule Rules { get; set; }

        string ToJsName(AstType type, bool asDefinition = false, bool excludens = false,
            bool isAlias = false, bool skipMethodTypeParam = false, bool removeScope = true, bool nomodule = false,
            bool ignoreLiteralName = true, bool ignoreVirtual = false);

        string ToJsName(IType type, bool asDefinition = false, bool excludens = false,
            bool isAlias = false, bool skipMethodTypeParam = false, bool removeScope = true, bool nomodule = false,
            bool ignoreLiteralName = true, bool ignoreVirtual = false);

        string ToTypeScriptName(IType type, bool asDefinition = false, bool excludens = false, 
            bool ignoreDependency = false, List<string> guard = null);

        string ToTypeScriptName(AstType astType, bool asDefinition = false,
            bool ignoreDependency = false);

        Tuple<string, string, Module> GetNamespaceFilename(ITypeInfo typeInfo);


        string AddModule(string name, ITypeInfo typeInfo, bool excludeNs, bool isNested, out bool isCustomName);
        string GetParentNames(IType type);
    }
}