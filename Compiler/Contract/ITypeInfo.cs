using System;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;

namespace Bridge.Contract
{
    /// <summary>
    /// This class wraps a method declaration and it's resolved method symbol. 
    /// </summary>
    public class MethodDeclarationAndSymbol
    {
        public IMethod Symbol { get; set; }
        public MethodDeclaration Declaration { get; set; }

        public MethodDeclarationAndSymbol(IMethod symbol, MethodDeclaration declaration)
        {
            Symbol = symbol;
            Declaration = declaration;
        }
    }

    public interface ITypeInfo
    {
        string Key
        {
            get;
            set;
        }

        TypeDeclaration TypeDeclaration
        {
            get;
            set;
        }

        Dictionary<string, EventDeclaration> EventsDeclarations
        {
            get;
        }

        bool IsStatic
        {
            get;
            set;
        }

        ClassType ClassType
        {
            get;
            set;
        }

        string Namespace
        {
            get;
            set;
        }

        string Name
        {
            get;
            set;
        }

        List<ConstructorDeclaration> Ctors
        {
            get;
            set;
        }

        ConstructorDeclaration StaticCtor
        {
            get;
            set;
        }

        Dictionary<string, FieldDeclaration> FieldsDeclarations
        {
            get;
        }

        Dictionary<OperatorType, List<OperatorDeclaration>> Operators
        {
            get;
        }

        Dictionary<string, List<MethodDeclarationAndSymbol>> StaticMethods
        {
            get;
        }

        Dictionary<string, List<MethodDeclarationAndSymbol>> InstanceMethods
        {
            get;
        }

        Dictionary<string, List<EntityDeclaration>> StaticProperties
        {
            get;
        }

        Dictionary<string, List<EntityDeclaration>> InstanceProperties
        {
            get;
        }

        bool HasStatic
        {
            get;
        }

        bool HasInstantiable
        {
            get;
        }

        object LastEnumValue
        {
            get;
            set;
        }

        bool IsEnum
        {
            get;
            set;
        }

        string FileName
        {
            get;
            set;
        }

        string JsName
        {
            get;
            set;
        }

        Module Module
        {
            get;
            set;
        }

        int? Priority
        {
            get; set;
        }

        List<IPluginDependency> Dependencies
        {
            get;
            set;
        }

        ITypeInfo ParentType
        {
            get;
            set;
        }

        TypeConfigInfo StaticConfig
        {
            get;
            set;
        }

        TypeConfigInfo InstanceConfig
        {
            get;
            set;
        }

        bool IsObjectLiteral
        {
            get;
            set;
        }

        ITypeDefinition Type
        {
            get;
            set;
        }

        List<TypeDeclaration> PartialTypeDeclarations
        {
            get;
            set;
        }

        bool? IsOutputType
        {
            get;
            set;
        }

        bool HasRealStatic(IEmitter emitter);
        bool HasRealInstantiable(IEmitter emitter);

        List<IType> GetBaseTypes(IEmitter emitter);

        IType GetBaseClass(IEmitter emitter);

        string GetNamespace(IEmitter emitter, bool nons = false);
    }
}