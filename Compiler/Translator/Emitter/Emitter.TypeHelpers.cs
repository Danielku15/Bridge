using System;
using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Object.Net.Utilities;

namespace Bridge.Translator
{
    public partial class Emitter
    {
        public virtual ITypeDefinition GetTypeDefinition()
        {
            return this.TypeInfo.Type.GetDefinition();
        }

        public virtual ITypeDefinition GetTypeDefinition(AstType reference)
        {
            var resolveResult = this.Resolver.ResolveNode(reference);
            var type = this.Translator.Types.Get(resolveResult.Type);
            return type?.Type;
        }

        private string GetCustomName(string name, ITypeInfo type, bool excludeNs, bool isNested, ref bool isCustomName, string moduleName)
        {
            var customName = this.GetCustomTypeName(type.Type, excludeNs);

            if (!string.IsNullOrEmpty(customName))
            {
                isCustomName = true;
                name = customName;
            }

            if (!string.IsNullOrEmpty(moduleName) && (!isNested || isCustomName))
            {
                name = string.IsNullOrWhiteSpace(name) ? moduleName : (moduleName + "." + name);
            }

            return name;
        }

        private string ObjectLiteralSignature(IType type)
        {
            var typeDef = type.GetDefinition();
            var isObjectLiteral = typeDef != null && typeDef.IsObjectLiteral();

            if (isObjectLiteral)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");

                var fields = type.GetFields().Where(f => f.IsPublic && f.DeclaringTypeDefinition.FullName != "System.Object");
                var properties = type.GetProperties().Where(p => p.IsPublic && p.DeclaringTypeDefinition.FullName != "System.Object");

                var comma = false;

                foreach (var field in fields)
                {
                    if (comma)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(OverloadsCollection.Create(this, field).GetOverloadName());
                    sb.Append(": ");
                    sb.Append(this.ToTypeScriptName(field.Type));

                    comma = true;
                }

                foreach (var property in properties)
                {
                    if (comma)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(OverloadsCollection.Create(this, property).GetOverloadName());
                    sb.Append(": ");
                    sb.Append(this.ToTypeScriptName(property.ReturnType));

                    comma = true;
                }

                sb.Append("}");
                return sb.ToString();
            }

            return null;
        }

        public string ToJsName(AstType ast, bool asDefinition = false, bool excludens = false,
            bool isAlias = false, bool skipMethodTypeParam = false, bool removeScope = true, bool nomodule = false,
            bool ignoreLiteralName = true, bool ignoreVirtual = false)
        {
            var type = this.Resolver.ResolveNode(ast);
            var name = this.ToJsName(type.Type, asDefinition, excludens, isAlias, skipMethodTypeParam, removeScope, nomodule,
                ignoreLiteralName, ignoreVirtual);
            if (name != CS.NS.BRIDGE && !name.StartsWith(CS.Bridge.DOTNAME) && ast.ToString().StartsWith(CS.NS.GLOBAL))
            {
                return JS.Types.Bridge.Global.DOTNAME + name;
            }
            return name;
        }

        public string ToJsName(IType type, bool asDefinition = false, bool excludens = false,
          bool isAlias = false, bool skipMethodTypeParam = false, bool removeScope = true, bool nomodule = false,
          bool ignoreLiteralName = true, bool ignoreVirtual = false)
        {
            var itypeDef = type.GetDefinition();
            var bridgeType = this.Translator.Types.Get(type);

            if (itypeDef != null)
            {
                var globalTarget = itypeDef.GetGlobalTarget(null, removeScope);
                if (globalTarget != null)
                {
                    if (bridgeType != null && !nomodule)
                    {
                        bool customName;
                        globalTarget = this.AddModule(globalTarget, bridgeType, excludens, false,
                            out customName);
                    }
                    return globalTarget;
                }
            }

            if (itypeDef != null && itypeDef.IsNonScriptable())
            {
                throw new EmitterException(this.Translator.EmitNode,
                    "Type " + type.FullName + " is marked as not usable from script");
            }

            if (type.Kind == TypeKind.Array)
            {
                if (type is ICSharpCode.NRefactory.TypeSystem.ArrayType arrayType && arrayType.ElementType != null)
                {
                    var elementAlias = this.ToJsName(arrayType.ElementType, asDefinition, excludens,
                        isAlias, skipMethodTypeParam);

                    if (isAlias)
                    {
                        return $"{elementAlias}$Array{(arrayType.Dimensions > 1 ? "$" + arrayType.Dimensions : "")}";
                    }

                    if (arrayType.Dimensions > 1)
                    {
                        return JS.Types.System.Array.TYPE + $"({elementAlias}, {arrayType.Dimensions})";
                    }
                    return JS.Types.System.Array.TYPE + $"({elementAlias})";
                }

                return JS.Types.ARRAY;
            }

            if (type.Kind == TypeKind.Delegate)
            {
                return JS.Types.FUNCTION;
            }

            if (type.Kind == TypeKind.Dynamic)
            {
                return JS.Types.System.Object.NAME;
            }

            if (type is ICSharpCode.NRefactory.TypeSystem.ByReferenceType)
            {
                return this.ToJsName(((ByReferenceType)type).ElementType, asDefinition, excludens,
                    isAlias, skipMethodTypeParam);
            }

            if (ignoreLiteralName)
            {
                var isObjectLiteral = itypeDef != null && itypeDef.IsObjectLiteral();
                var isPlainMode = isObjectLiteral && itypeDef.GetObjectCreateMode() == 0;

                if (isPlainMode)
                {
                    return "System.Object";
                }
            }

            if (type.Kind == TypeKind.Anonymous)
            {
                var at = type as AnonymousType;
                if (at != null && this.AnonymousTypes.ContainsKey(at))
                {
                    return this.AnonymousTypes[at].Name;
                }
                else
                {
                    return JS.Types.System.Object.NAME;
                }
            }

            var typeParam = type as ITypeParameter;
            if (typeParam != null)
            {
                if (skipMethodTypeParam && (typeParam.OwnerType == SymbolKind.Method) ||
                    typeParam.Owner.IsIgnoreGeneric())
                {
                    return JS.Types.System.Object.NAME;
                }
            }

            var name = excludens ? "" : type.Namespace;

            var hasTypeDef = bridgeType != null && bridgeType.Type != null;
            var isNested = false;
            if (hasTypeDef)
            {
                var typeDef = bridgeType.Type;

                if (typeDef.DeclaringType != null && !excludens)
                {
                    name = this.ToJsName(typeDef.DeclaringType, true, ignoreVirtual: true,
                        nomodule: nomodule);
                    isNested = true;
                }

                name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) +
                       this.GetTypeName(itypeDef).ConvertName();
            }
            else
            {
                if (type.DeclaringType != null && !excludens)
                {
                    name = this.ToJsName(type.DeclaringType, true, ignoreVirtual: true);
                    isNested = true;
                }

                name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + type.Name.ConvertName();
            }

            bool isCustomName = false;
            if (bridgeType != null)
            {
                if (nomodule)
                {
                    name = this.GetCustomName(name, bridgeType, excludens, isNested, ref isCustomName, null);
                }
                else
                {
                    name = this.AddModule(name, bridgeType, excludens, isNested, out isCustomName);
                }
            }

            var tDef = type.GetDefinition();
            var skipSuffix = tDef != null && tDef.ParentAssembly.AssemblyName != CS.NS.BRIDGE && tDef.IsExternal() &&
                             tDef.IsIgnoreGeneric();

            if (!hasTypeDef && !isCustomName && type.TypeArguments.Count > 0 && !skipSuffix)
            {
                name += Helpers.PrefixDollar(type.TypeArguments.Count);
            }

            var genericSuffix = "$" + type.TypeArguments.Count;
            if (skipSuffix && !isCustomName && type.TypeArguments.Count > 0 && name.EndsWith(genericSuffix))
            {
                name = name.Substring(0, name.Length - genericSuffix.Length);
            }

            if (isAlias)
            {
                name = OverloadsCollection.NormalizeInterfaceName(name);
            }

            if (type.TypeArguments.Count > 0 && !type.IsIgnoreGeneric() && !asDefinition)
            {
                if (isAlias)
                {
                    StringBuilder sb = new StringBuilder(name);
                    bool needComma = false;
                    sb.Append(JS.Vars.D);
                    bool isStr = false;
                    foreach (var typeArg in type.TypeArguments)
                    {
                        if (sb.ToString().EndsWith(")"))
                        {
                            sb.Append(" + \"");
                        }

                        if (needComma && !sb.ToString().EndsWith(JS.Vars.D.ToString()))
                        {
                            sb.Append(JS.Vars.D);
                        }

                        needComma = true;
                        bool needGet = typeArg.Kind == TypeKind.TypeParameter && !asDefinition;
                        if (needGet)
                        {
                            if (!isStr)
                            {
                                sb.Insert(0, "\"");
                                isStr = true;
                            }
                            sb.Append("\" + " + JS.Types.Bridge.GET_TYPE_ALIAS + "(");
                        }

                        var typeArgName = this.ToJsName(typeArg, asDefinition, false, true,
                            skipMethodTypeParam, ignoreVirtual: true);

                        if (!needGet && typeArgName.StartsWith("\""))
                        {
                            var tName = typeArgName.Substring(1);

                            if (tName.EndsWith("\""))
                            {
                                tName = tName.Remove(tName.Length - 1);
                            }

                            sb.Append(tName);

                            if (!isStr)
                            {
                                isStr = true;
                                sb.Insert(0, "\"");
                            }
                        }
                        else
                        {
                            sb.Append(typeArgName);
                        }

                        if (needGet)
                        {
                            sb.Append(")");
                        }
                    }

                    if (isStr && sb.Length >= 1)
                    {
                        var sbEnd = sb.ToString(sb.Length - 1, 1);

                        if (!sbEnd.EndsWith(")") && !sbEnd.EndsWith("\""))
                        {
                            sb.Append("\"");
                        }
                    }

                    name = sb.ToString();
                }
                else
                {
                    StringBuilder sb = new StringBuilder(name);
                    bool needComma = false;
                    sb.Append("(");
                    foreach (var typeArg in type.TypeArguments)
                    {
                        if (needComma)
                        {
                            sb.Append(",");
                        }

                        needComma = true;

                        sb.Append(this.ToJsName(typeArg, skipMethodTypeParam: skipMethodTypeParam));
                    }
                    sb.Append(")");
                    name = sb.ToString();
                }
            }

            if (!ignoreVirtual && !isAlias)
            {
                var td = type.GetDefinition();
                if (td != null && td.IsVirtual())
                {
                    string fnName = td.Kind == TypeKind.Interface
                        ? JS.Types.Bridge.GET_INTERFACE
                        : JS.Types.Bridge.GET_CLASS;
                    name = fnName + "(\"" + name + "\")";
                }
                else if (!isAlias && itypeDef != null && itypeDef.Kind == TypeKind.Interface)
                {
                    var externalInterface = itypeDef.GetExternalInterface();
                    if (externalInterface != null && externalInterface.IsVirtual)
                    {
                        name = JS.Types.Bridge.GET_INTERFACE + "(\"" + name + "\")";
                    }
                }
            }

            return name;
        }

        public string ToTypeScriptName(AstType astType, bool asDefinition = false,
             bool ignoreDependency = false)
        {
            string name = null;
            var primitive = astType as PrimitiveType;
            name = GetTsPrimitivie(primitive);
            if (name != null)
            {
                return name;
            }

            var composedType = astType as ComposedType;
            if (composedType != null && composedType.ArraySpecifiers != null && composedType.ArraySpecifiers.Count > 0)
            {
                return this.ToTypeScriptName(composedType.BaseType) + string.Concat(Enumerable.Repeat("[]", composedType.ArraySpecifiers.Count));
            }

            var simpleType = astType as SimpleType;
            if (simpleType != null && simpleType.Identifier == "dynamic")
            {
                return "any";
            }

            var resolveResult = this.Resolver.ResolveNode(astType);
            return this.ToTypeScriptName(resolveResult.Type, asDefinition: asDefinition, ignoreDependency: ignoreDependency);

        }

        private static string GetTsPrimitivie(PrimitiveType primitive)
        {
            if (primitive != null)
            {
                switch (primitive.KnownTypeCode)
                {
                    case KnownTypeCode.Void:
                        return "void";

                    case KnownTypeCode.Boolean:
                        return "boolean";

                    case KnownTypeCode.String:
                        return "string";

                    case KnownTypeCode.Double:
                    case KnownTypeCode.Byte:
                    case KnownTypeCode.Char:
                    case KnownTypeCode.Int16:
                    case KnownTypeCode.Int32:
                    case KnownTypeCode.SByte:
                    case KnownTypeCode.Single:
                    case KnownTypeCode.UInt16:
                    case KnownTypeCode.UInt32:
                        return "number";
                }
            }

            return null;
        }


        public string ToTypeScriptName(IType type, bool asDefinition = false, bool excludens = false, bool ignoreDependency = false, List<string> guard = null)
        {
            if (type.Kind == TypeKind.Delegate)
            {
                if (guard == null)
                {
                    guard = new List<string>();
                }

                if (guard.Contains(type.FullName))
                {
                    return "Function";
                }

                guard.Add(type.FullName);
                var method = type.GetDelegateInvokeMethod();

                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.Append("(");

                var last = method.Parameters.LastOrDefault();
                foreach (var p in method.Parameters)
                {
                    var ptype = this.ToTypeScriptName(p.Type, guard: guard);

                    if (p.IsOut || p.IsRef)
                    {
                        ptype = "{v: " + ptype + "}";
                    }

                    sb.Append(p.Name + ": " + ptype);
                    if (p != last)
                    {
                        sb.Append(", ");
                    }
                }

                sb.Append(")");
                sb.Append(": ");
                sb.Append(this.ToTypeScriptName(method.ReturnType, guard: guard));
                sb.Append("}");
                guard.Remove(type.FullName);
                return sb.ToString();
            }

            var oname = this.ObjectLiteralSignature(type);

            if (oname != null)
            {
                return oname;
            }

            if (type.IsKnownType(KnownTypeCode.String))
            {
                return "string";
            }

            if (type.IsKnownType(KnownTypeCode.Boolean))
            {
                return "boolean";
            }

            if (type.IsKnownType(KnownTypeCode.Void))
            {
                return "void";
            }

            if (type.IsKnownType(KnownTypeCode.Array))
            {
                return "any[]";
            }

            if (type.IsKnownType(KnownTypeCode.Byte) ||
                type.IsKnownType(KnownTypeCode.Char) ||
                type.IsKnownType(KnownTypeCode.Double) ||
                type.IsKnownType(KnownTypeCode.Int16) ||
                type.IsKnownType(KnownTypeCode.Int32) ||
                type.IsKnownType(KnownTypeCode.SByte) ||
                type.IsKnownType(KnownTypeCode.Single) ||
                type.IsKnownType(KnownTypeCode.UInt16) ||
                type.IsKnownType(KnownTypeCode.UInt32))
            {
                return "number";
            }

            if (type.Kind == TypeKind.Array)
            {
                ArrayType arrayType = (ArrayType)type;
                return this.ToTypeScriptName(arrayType.ElementType, asDefinition, excludens, guard: guard) + "[]";
            }

            if (type.Kind == TypeKind.Dynamic || type.IsKnownType(KnownTypeCode.Object))
            {
                return "any";
            }

            if (type.Kind == TypeKind.Enum && type.DeclaringType != null && !excludens)
            {
                return "number";
            }

            if (NullableType.IsNullable(type))
            {
                return this.ToTypeScriptName(NullableType.GetUnderlyingType(type), asDefinition, excludens, guard: guard);
            }

            var bridgeType = this.Translator.Types.Get(type);
            //string name = Types.ConvertName(excludens ? type.Name : type.FullName);

            var name = excludens ? "" : type.Namespace;

            var hasTypeDef = bridgeType != null && bridgeType.Type != null;
            bool isNested = false;

            if (hasTypeDef)
            {
                var typeDef = bridgeType.Type;
                if (typeDef.DeclaringType != null && !excludens)
                {
                    //name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + Types.GetParentNames(emitter, typeDef);
                    name = this.ToJsName(typeDef.DeclaringType, true, ignoreVirtual: true);
                    isNested = true;
                }

                name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + this.GetTypeName(bridgeType.Type).ConvertName();
            }
            else
            {
                if (type.DeclaringType != null && !excludens)
                {
                    //name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + Types.GetParentNames(emitter, type);
                    name = this.ToJsName(type.DeclaringType, true, ignoreVirtual: true);

                    if (type.DeclaringType.TypeArguments.Count > 0)
                    {
                        name += Helpers.PrefixDollar(type.TypeArguments.Count);
                    }
                    isNested = true;
                }

                name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + type.Name.ConvertName();
            }

            bool isCustomName = false;
            if (bridgeType != null)
            {
                if (!ignoreDependency && this.AssemblyInfo.OutputBy != OutputBy.Project && bridgeType.IsOutputType.GetValueOrDefault() &&
                    bridgeType.Namespace != this.TypeInfo.Namespace)
                {
                    var info = this.GetNamespaceFilename(bridgeType);
                    var ns = info.Item1;
                    var fileName = info.Item2;

                    if (!this.CurrentDependencies.Any(d => d.DependencyName == fileName))
                    {
                        this.CurrentDependencies.Add(new ModuleDependency()
                        {
                            DependencyName = fileName
                        });
                    }
                }

                name = this.GetCustomName(name, bridgeType, excludens, isNested, ref isCustomName, null);
            }

            if (!hasTypeDef && !isCustomName && type.TypeArguments.Count > 0)
            {
                name += Helpers.PrefixDollar(type.TypeArguments.Count);
            }

            if (!asDefinition && type.TypeArguments.Count > 0 && !type.IsIgnoreGeneric(true))
            {
                StringBuilder sb = new StringBuilder(name);
                bool needComma = false;
                sb.Append("<");
                foreach (var typeArg in type.TypeArguments)
                {
                    if (needComma)
                    {
                        sb.Append(",");
                    }

                    needComma = true;
                    sb.Append(this.ToTypeScriptName(typeArg, asDefinition, excludens, guard: guard));
                }
                sb.Append(">");
                name = sb.ToString();
            }

            return name;
        }

        public Tuple<string, string, Module> GetNamespaceFilename(ITypeInfo typeInfo)
        {
            var ns = typeInfo.GetNamespace(this, true);
            var fileName = ns ?? typeInfo.GetNamespace(this);
            var module = typeInfo.Module;
            string moduleName = null;

            if (module != null && module.Type == ModuleType.UMD)
            {
                if (!module.PreventModuleName)
                {
                    moduleName = module.ExportAsNamespace;
                }

                if (!String.IsNullOrEmpty(moduleName))
                {
                    ns = string.IsNullOrWhiteSpace(ns) ? moduleName : (moduleName + "." + ns);
                }
                else
                {
                    module = null;
                }
            }

            switch (this.AssemblyInfo.FileNameCasing)
            {
                case FileNameCaseConvert.Lowercase:
                    fileName = fileName.ToLower();
                    break;

                case FileNameCaseConvert.CamelCase:
                    var sepList = new string[] { ".", System.IO.Path.DirectorySeparatorChar.ToString(), "\\", "/" };

                    // Populate list only with needed separators, as usually we will never have all four of them
                    var neededSepList = new List<string>();

                    foreach (var separator in sepList)
                    {
                        if (fileName.Contains(separator.ToString()) && !neededSepList.Contains(separator))
                        {
                            neededSepList.Add(separator);
                        }
                    }

                    // now, separating the filename string only by the used separators, apply lowerCamelCase
                    if (neededSepList.Count > 0)
                    {
                        foreach (var separator in neededSepList)
                        {
                            var stringList = new List<string>();

                            foreach (var str in fileName.Split(separator[0]))
                            {
                                stringList.Add(str.ToLowerCamelCase());
                            }

                            fileName = stringList.Join(separator);
                        }
                    }
                    else
                    {
                        fileName = fileName.ToLowerCamelCase();
                    }
                    break;
            }

            return new Tuple<string, string, Module>(ns, fileName, module);
        }

        public string GetParentNames(IType type)
        {
            List<string> names = new List<string>();
            while (type.DeclaringType != null)
            {
                var name = this.ToJsName(type.DeclaringType, true, true).ConvertName();

                if (type.DeclaringType.TypeArguments.Count > 0)
                {
                    name += Helpers.PrefixDollar(type.TypeArguments.Count);
                }
                names.Add(name);
                type = type.DeclaringType;
            }

            names.Reverse();
            return names.Join(".");
        }
    }
}