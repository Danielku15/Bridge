using System;
using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Linq;

namespace Bridge.Translator
{
    public partial class Inspector
    {
        private string _namespace;
        private ITypeInfo _currentType;
        private List<Tuple<TypeDeclaration, ITypeInfo>> _nestedTypes;

        public ITranslator Translator { get; }

        public Inspector(ITranslator translator)
        {
            this.Translator = translator;
        }

        #region Semantic Inspector

        public void ReadTypes()
        {
            var compilation = this.Translator.Resolver.Compilation;
            if (compilation.MainAssembly.AssemblyName != CS.NS.BRIDGE 
                || (this.Translator.AssemblyInfo.Assembly?.EnableReservedNamespaces ?? true))
            {
                this.ReadTypes(compilation.MainAssembly, true);
            }

            foreach (var item in compilation.ReferencedAssemblies)
            {
                this.ReadTypes(item);
            }

        }
        protected virtual void ReadTypes(IAssembly assembly, bool isMain = false)
        {
            this.Translator.Log.Trace("Reading types for assembly " + assembly.FullAssemblyName + " ...");

            this.AddNestedTypes(assembly.TopLevelTypeDefinitions);
            if (isMain)
            {
                this.Translator.AssemblyInfo.Module = assembly.GetModule() ?? this.Translator.AssemblyInfo.Module;
                this.Translator.AssemblyInfo.FileName = assembly.GetFileName() ?? this.Translator.AssemblyInfo.FileName;
                var output = assembly.GetOutput();
                if (output != null)
                {
                    var configHelper = new ConfigHelper();
                    this.Translator.AssemblyInfo.Output = configHelper.ConvertPath(output);
                }

                var outputBy = assembly.GetOutputBy();
                if (outputBy != null)
                {
                    this.Translator.AssemblyInfo.OutputBy = outputBy.Item1;
                    if (outputBy.Item2.HasValue)
                    {
                        this.Translator.AssemblyInfo.StartIndexInName = outputBy.Item2.Value;
                    }
                }

                this.Translator.AssemblyInfo.Dependencies.AddRange(assembly.GetModuleDependencies());

                assembly.ReadReflectionInfo(this.Translator.AssemblyInfo.Reflection);
            }

            this.Translator.Log.Trace("Reading types for assembly done");
        }

        protected virtual void AddNestedTypes(IEnumerable<ITypeDefinition> types)
        {
            foreach (var type in types.OrderBy(t => t.FullName))
            {
                if (type.FullName.Contains("<"))
                {
                    continue;
                }

                var duplicateBridgeType = this.Translator.Types.Get(type);
                if (duplicateBridgeType != null)
                {
                    var duplicate = duplicateBridgeType.Type;
                    var message = string.Format(
                        Constants.Messages.Exceptions.DUPLICATE_BRIDGE_TYPE,
                        type.ParentAssembly.FullAssemblyName,
                        type.FullName,
                        duplicate.ParentAssembly.FullAssemblyName,
                        duplicate.FullName);
                    this.Translator.Log.Error(message);
                    throw new System.InvalidOperationException(message);
                }

                var typeInfo = this.Translator.Types.GetOrCreateTypeInfo(type);
                if (!type.IsExternal() && type.Kind != TypeKind.Delegate)
                {
                    var fileName = type.GetFileName();
                    if (fileName != null)
                    {
                        typeInfo.FileName = fileName;
                    }
                    var module = type.GetModule();
                    if (module != null)
                    {
                        typeInfo.Module = module;
                    }

                    var dependency = type.GetModuleDependency();
                    if (dependency != null)
                    {
                        typeInfo.Dependencies.Add(dependency);
                    }
                    typeInfo.Priority = type.GetPriority();
                }

                if (type.NestedTypes != null)
                {
                    this.AddNestedTypes(type.NestedTypes);
                }
            }
        }


        #endregion

        private Expression GetDefaultFieldInitializer(AstType type)
        {
            return new PrimitiveExpression(GetDefaultFieldValue(type, this.Translator.Resolver), "?");
        }

        public static object GetDefaultFieldValue(AstType type, IMemberResolver resolver)
        {
            if (type is PrimitiveType primitiveType)
            {
                switch (primitiveType.KnownTypeCode)
                {
                    case KnownTypeCode.Decimal:
                        return 0m;

                    case KnownTypeCode.Int64:
                        return 0L;

                    case KnownTypeCode.UInt64:
                        return 0UL;

                    case KnownTypeCode.Int16:
                    case KnownTypeCode.Int32:
                    case KnownTypeCode.UInt16:
                    case KnownTypeCode.UInt32:
                    case KnownTypeCode.Byte:
                    case KnownTypeCode.Double:
                    case KnownTypeCode.SByte:
                    case KnownTypeCode.Single:
                        return 0;

                    case KnownTypeCode.Boolean:
                        return false;
                }
            }

            var resolveResult = resolver.ResolveNode(type);

            var o = GetDefaultFieldValue(resolveResult.Type, type, false);

            if (o != null)
            {
                return o;
            }

            if (!resolveResult.IsError && NullableType.IsNullable(resolveResult.Type))
            {
                return null;
            }

            if (!resolveResult.IsError && (resolveResult.Type.IsKnownType(KnownTypeCode.Enum) || resolveResult.Type.Kind == TypeKind.Enum))
            {
                return 0;
            }

            if (!resolveResult.IsError && resolveResult.Type.Kind == TypeKind.Struct)
            {
                return type;
            }

            return null;
        }

        public static object GetDefaultFieldValue(IType type, AstType astType, bool wrapType = true)
        {
            if (type.Kind == TypeKind.TypeParameter && astType != null)
            {
                if (type is ITypeParameter parameter && (
                    parameter.Owner.IsIgnoreGeneric()||
                    parameter.Owner.DeclaringTypeDefinition != null && parameter.Owner.DeclaringTypeDefinition.IsIgnoreGeneric()))
                {
                    return null;
                }
                return new RawValue(JS.Funcs.BRIDGE_GETDEFAULTVALUE + "(" + type.Name + ")");
            }

            if (type.IsKnownType(KnownTypeCode.Decimal))
            {
                return 0m;
            }

            if (type.IsKnownType(KnownTypeCode.Int64))
            {
                return 0L;
            }

            if (type.IsKnownType(KnownTypeCode.UInt64))
            {
                return 0UL;
            }

            if (type.IsKnownType(KnownTypeCode.Char) ||
                type.IsKnownType(KnownTypeCode.Int16) ||
                type.IsKnownType(KnownTypeCode.Int32) ||
                type.IsKnownType(KnownTypeCode.UInt16) ||
                type.IsKnownType(KnownTypeCode.UInt32) ||
                type.IsKnownType(KnownTypeCode.Byte) ||
                type.IsKnownType(KnownTypeCode.Double) ||
                type.IsKnownType(KnownTypeCode.SByte) ||
                type.IsKnownType(KnownTypeCode.Single) ||
                type.IsKnownType(KnownTypeCode.Enum))
            {
                return 0;
            }

            if (NullableType.IsNullable(type))
            {
                return null;
            }

            if (type.IsKnownType(KnownTypeCode.Boolean))
            {
                return false;
            }

            if (type.IsKnownType(KnownTypeCode.Enum) || type.Kind == TypeKind.Enum)
            {
                return 0;
            }

            if (type.Kind == TypeKind.Struct && wrapType)
            {
                return type;
            }

            return null;
        }

        public static string GetStructDefaultValue(AstType type, IEmitter emitter)
        {
            var rr = emitter.Resolver.ResolveNode(type);
            return GetStructDefaultValue(rr.Type, emitter);
        }

        public static string GetStructDefaultValue(IType type, IEmitter emitter)
        {
            if (type.IsKnownType(KnownTypeCode.DateTime))
            {
                return $"{JS.Types.System.DateTime.GET_DEFAULT_VALUE}()";
            }

            var isGeneric = type.TypeArguments.Count > 0 && !type.IsIgnoreGeneric();

            return string.Concat("new ", isGeneric ? "(" : "", emitter.ToJsName(type), isGeneric ? ")" : "", "()");
        }

        protected virtual bool IsValidStaticInitializer(Expression expr)
        {
            if (expr.IsNull || expr is PrimitiveExpression)
            {
                return true;
            }

            if (!(expr is ArrayCreateExpression arrayExpr))
            {
                return false;
            }

            try
            {
                new ArrayInitializerVisitor().VisitArrayCreateExpression(arrayExpr);

                return true;
            }
            catch (TranslatorException)
            {
                return false;
            }
        }

        protected virtual void FixMethodParameters(AstNodeCollection<ParameterDeclaration> parameters, BlockStatement body)
        {
            /*if (parameters.Count == 0)
            {
                return;
            }

            foreach (var p in parameters)
            {
                string newName = JS.Vars.FIX_ARGUMENT_NAME + p.Name;
                string oldName = p.Name;

                VariableDeclarationStatement varState = new VariableDeclarationStatement(p.Type.Clone(), oldName, new CastExpression(p.Type.Clone(), new IdentifierExpression(newName)));

                p.Name = newName;

                body.InsertChildBefore(body.FirstChild, varState, new Role<VariableDeclarationStatement>("Statement"));
            }*/
        }

        /// <summary>
        /// Checks if the namespace name is likely to conflict with Bridge.NET namespace.
        /// </summary>
        /// <param name="namespaceName"></param>
        /// <returns></returns>
        protected static bool IsConflictingNamespace(string namespaceName)
        {
            return (namespaceName == CS.NS.BRIDGE);
        }

        /// <summary>
        /// Validates the namespace name against conflicts with Bridge.NET's namespaces.
        /// </summary>
        /// <param name="nsDecl">The NamespaceDefinition object of the validated item.</param>
        private void ValidateNamespace(NamespaceDeclaration nsDecl)
        {
            if (IsConflictingNamespace(nsDecl.FullName))
            {
                throw new EmitterException(nsDecl, "Namespace '" + nsDecl.FullName +
                    "' uses reserved name 'Bridge'."
                    + Emitter.NEW_LINE
                    + "This name is reserved for Bridge.NET core.");
            }
        }
        private void ValidateNamespace(ITypeInfo typeInfo, TypeDeclaration typeDeclaration)
        {
            var nsName = typeInfo.Type.GetNamespace();
            if (nsName != null && IsConflictingNamespace(nsName.Item1))
            {
                throw new EmitterException(typeDeclaration.NameToken, "Custom attribute '[" + typeInfo.Type.FullName +
                                                                      "]' uses reserved namespace name 'Bridge'."
                                                                      + Emitter.NEW_LINE
                                                                      + "This name is reserved for Bridge.NET core.");
            }
        }

        internal void CheckObjectLiteral(ITypeDefinition type, TypeDeclaration typeDeclaration)
        {
            if (!type.IsObjectLiteral())
            {
                return;
            }

            var objectCreateMode = type.GetObjectCreateMode();

            if (objectCreateMode == 0)
            {
                var ctors = type.GetConstructors();

                foreach (var ctor in ctors)
                {
                    foreach (var parameter in ctor.Parameters)
                    {
                        if (parameter.Type.FullName == "Bridge.ObjectCreateMode")
                        {
                            throw new EmitterException(typeDeclaration.NameToken, 
                                string.Format(Constants.Messages.Exceptions.OBJECT_LITERAL_PLAIN_NO_CREATE_MODE_CUSTOM_CONSTRUCTOR, type));
                        }

                        if (parameter.Type.FullName == "Bridge.ObjectInitializationMode")
                        {
                            continue;
                        }

                        throw new EmitterException(typeDeclaration.NameToken,
                            string.Format(Constants.Messages.Exceptions.OBJECT_LITERAL_PLAIN_CUSTOM_CONSTRUCTOR, type));
                    }
                }
            }

            if (type.Kind == TypeKind.Interface)
            {
                if (type.Methods.GroupBy(m => m.Name).Any(g => g.Count() > 1))
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_INTERFACE_NO_OVERLOAD_METHODS, type);
                }

                if (type.Events.Any())
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_INTERFACE_NO_EVENTS, type);
                }
            }
            else
            {
                if (type.Methods.Any(m => m.IsExplicitInterfaceImplementation) ||
                    type.Properties.Any(m => m.IsExplicitInterfaceImplementation))
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_INTERFACE_NO_EXPLICIT_IMPLEMENTATION, type);
                }
            }

            var baseType = type.GetBaseClassDefinition();
            if (baseType != null)
            {
                if (objectCreateMode == 1 && baseType.FullName != "System.Object" && baseType.FullName != "System.ValueType" && baseType.GetObjectCreateMode() == 0)
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_CONSTRUCTOR_INHERITANCE, type);
                }

                if (objectCreateMode == 0 && baseType.GetObjectCreateMode() == 1)
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_PLAIN_INHERITANCE, type);
                }
            }

            foreach (var @interface in type.GetAllBaseTypes().Where(t => t.Kind == TypeKind.Interface).Select(t => t.GetDefinition()))
            {
                if (!@interface.IsObjectLiteral())
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_INTERFACE_INHERITANCE, type);
                }
            }

            if (objectCreateMode == 0)
            {
                var hasVirtualMethods = false;

                foreach (var method in type.Methods)
                {
                    if (method.IsCompilerGenerated())
                    {
                        continue;
                    }

                    if (method.IsVirtual && !method.IsAccessor)
                    {
                        hasVirtualMethods = true;
                        break;
                    }
                }

                if (hasVirtualMethods)
                {
                    TranslatorException.Throw(Constants.Messages.Exceptions.OBJECT_LITERAL_NO_VIRTUAL_METHODS, type);
                }
            }
        }

    }
}