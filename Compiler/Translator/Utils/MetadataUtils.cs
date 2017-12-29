using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.NRefactory.Semantics;

namespace Bridge.Translator
{
    public class MetadataUtils
    {
        public static JObject ConstructTypeMetadata(ITypeDefinition type, IEmitter emitter, bool ifHasAttribute, SyntaxTree tree)
        {
            var properties = ifHasAttribute ? new JObject() : MetadataUtils.GetCommonTypeProperties(type, emitter);
            var scriptableAttributes = type.GetScriptableAttributes(tree).ToArray();
            if (scriptableAttributes.Any())
            {
                JArray attrArr = new JArray();
                foreach (var a in scriptableAttributes)
                {
                    attrArr.Add(ConstructAttribute(a, type, emitter));
                }

                properties.Add("at", attrArr);
            }

            if (type.Kind == TypeKind.Class || type.Kind == TypeKind.Struct || type.Kind == TypeKind.Interface || type.Kind == TypeKind.Enum)
            {
                var members = type.Members.Where(m => m.IsReflectable(ifHasAttribute, tree))
                                          .OrderBy(m => m, MemberOrderer.Instance)
                                          .Select(m => MetadataUtils.ConstructMemberInfo(m, emitter, false, false, tree))
                                          .ToList();
                if (members.Count > 0)
                {
                    properties.Add("m", new JArray(members));
                }

                var aua = type.GetAttributeUsage();
                if (aua != null)
                {
                    if (!aua.Inherited)
                    {
                        properties.Add("ni", true);
                    }

                    if (aua.AllowMultiple)
                    {
                        properties.Add("am", true);
                    }
                }
            }

            return properties.Count > 0 ? properties : null;
        }

        public static JObject ConstructITypeMetadata(IType type, IEmitter emitter)
        {
            var properties = MetadataUtils.GetCommonTypeProperties(type, emitter);

            if (type.Kind == TypeKind.Class || type.Kind == TypeKind.Anonymous)
            {
                var members = type.GetMembers(null, GetMemberOptions.IgnoreInheritedMembers).Where(m => m.IsReflectable(false, null))
                                          .OrderBy(m => m, MemberOrderer.Instance)
                                          .Select(m => MetadataUtils.ConstructMemberInfo(m, emitter, false, false, null))
                                          .ToList();
                if (members.Count > 0)
                {
                    properties.Add("m", new JArray(members));
                }
            }

            return properties.Count > 0 ? properties : null;
        }

        private static JObject GetCommonTypeProperties(IType type, IEmitter emitter)
        {
            var result = new JObject();
            var typedef = type.GetDefinition();

            if (typedef != null)
            {
                var cecilType = type.Kind == TypeKind.Anonymous ? null : type.GetDefinition();

                if (type.DeclaringType != null)
                {
                    result.Add("td", new JRaw(MetadataUtils.GetTypeName(type.DeclaringType, emitter, false)));
                }

                if (cecilType != null)
                {
                    TypeAttributes att = TypeAttributes.NotPublic;

                    switch (cecilType.Kind)
                    {
                        case TypeKind.Class:
                            att |= TypeAttributes.Class;
                            break;
                        case TypeKind.Interface:
                            att |= TypeAttributes.Interface;
                            break;
                    }
                    if (cecilType.DeclaringType != null)
                    {
                        if (cecilType.IsPublic)
                        {
                            att |= TypeAttributes.NestedPublic;
                        }
                        else if (cecilType.IsPrivate)
                        {
                            att |= TypeAttributes.NestedPrivate;
                        }
                        if (cecilType.IsProtected)
                        {
                            att |= TypeAttributes.NestedFamily;
                        }
                        if (cecilType.IsInternal)
                        {
                            att |= TypeAttributes.NestedAssembly;
                        }
                    }
                    else if (cecilType.IsPublic)
                    {
                        att |= TypeAttributes.Public;
                    }

                    var layoutType = cecilType.GetLayoutKind();
                    if (layoutType.HasValue)
                    {
                        switch (layoutType)
                        {
                            case LayoutKind.Sequential:
                                att |= TypeAttributes.SequentialLayout;
                                break;
                            case LayoutKind.Explicit:
                                att |= TypeAttributes.ExplicitLayout;
                                break;
                        }
                    }
                    else if(cecilType.Kind == TypeKind.Struct)
                    {
                        att |= TypeAttributes.SequentialLayout;
                    }

                    if (cecilType.IsAbstract)
                    {
                        att |= TypeAttributes.Abstract;
                    }
                    if (cecilType.IsSealed || cecilType.Kind == TypeKind.Enum)
                    {
                        att |= TypeAttributes.Sealed;
                    }

                    //SpecialName = 1024,
                    //RTSpecialName = 2048,
                    //Import = 4096,

                    if(cecilType.IsSerializable())
                    {
                        att |= TypeAttributes.Serializable;
                    }

                    //WindowsRuntime = 16384,
                    //UnicodeClass = 65536,
                    //AutoClass = 131072,
                    //StringFormatMask = 196608,
                    //HasSecurity = 262144,

                    if (cecilType.Kind != TypeKind.Interface && cecilType.Kind != TypeKind.Enum
                        && cecilType.Methods.All(m => !m.IsConstructor || !m.IsStatic))
                    {
                        att |= TypeAttributes.BeforeFieldInit;
                    }

                    //Forwarder = 2097152

                    result.Add("att", (int)att);
                }

                if (typedef.Accessibility != Accessibility.None)
                {
                    result.Add("a", (int)typedef.Accessibility);
                }

                if (typedef.IsStatic)
                {
                    result.Add("s", true);
                }
            }

            return result;
        }

        public static JRaw ConstructAttribute(IAttribute attr, ITypeDefinition currentType, IEmitter emitter)
        {
            var block = new AttributeCreateBlock(emitter, attr);
            var oldWriter = block.SaveWriter();
            block.NewWriter();
            block.Emit();
            var str = emitter.Output.ToString();

            block.RestoreWriter(oldWriter);
            return new JRaw(str);
        }



        public static bool IsJsGeneric(IMethod method, IEmitter emitter)
        {
            return method.TypeParameters.Count > 0 && !method.IsIgnoreGeneric();
        }

        public static bool IsJsGeneric(ITypeDefinition type, IEmitter emitter)
        {
            return type.TypeParameterCount > 0 && !type.IsIgnoreGeneric();
        }

        private static JObject ConstructParameterInfo(IParameter p, IEmitter emitter, bool includeDeclaringType, bool isGenericSpecialization, SyntaxTree tree)
        {
            var result = new JObject();

            var attr = p.GetScriptableAttributes(tree).ToArray();
            if (attr.Any())
            {
                JArray attrArr = new JArray();
                foreach (var a in attr)
                {
                    attrArr.Add(MetadataUtils.ConstructAttribute(a, null, emitter));
                }

                result.Add("at", attrArr);
            }

            result.Add("n", p.Name);

            if (p.IsOptional)
            {
                var typeParam = p.Type as ITypeParameter;
                if (typeParam != null && p.ConstantValue == null)
                {
                    result.Add("dv",
                        typeParam.OwnerType == SymbolKind.Method
                            ? new JRaw(p.ConstantValue.ToJavaScript())
                            : new JRaw(string.Format("{0}({1})", JS.Funcs.BRIDGE_GETDEFAULTVALUE, typeParam.Name)));
                }
                else
                {
                    result.Add("dv", new JRaw(p.ConstantValue.ToJavaScript()));
                }

                result.Add("o", true);
            }

            if (p.IsOut)
            {
                result.Add("out", true);
            }

            if (p.IsRef)
            {
                result.Add("ref", true);
            }

            if (p.IsParams)
            {
                result.Add("ip", true);
            }

            result.Add("pt", new JRaw(MetadataUtils.GetTypeName(p.Type, emitter, isGenericSpecialization)));
            result.Add("ps", p.Owner.Parameters.IndexOf(p));

            var nameAttr = p.GetNameAttribute();
            if (nameAttr != null)
            {
                var name = Helpers.ConvertNameTokens(nameAttr, p.Name);
                if (Helpers.IsReservedWord(emitter, name))
                {
                    name = Helpers.ChangeReservedWord(name);
                }
                result.Add("sn", name);
            }

            return result;
        }

        public static JObject ConstructMemberInfo(IMember m, IEmitter emitter, bool includeDeclaringType, bool isGenericSpecialization, SyntaxTree tree)
        {
            if (m is IMethod && ((IMethod)m).IsConstructor)
            {
                return MetadataUtils.ConstructConstructorInfo((IMethod)m, emitter, includeDeclaringType, isGenericSpecialization, tree);
            }

            var properties = MetadataUtils.GetCommonMemberInfoProperties(m, emitter, includeDeclaringType, isGenericSpecialization, tree);
            if (m.IsStatic)
            {
                properties.Add("is", true);
            }

            if (m is IMethod)
            {
                var method = (IMethod)m;
                var inline = emitter.GetInline(method);

                if (string.IsNullOrEmpty(inline) && method.ExpandParams())
                {
                    properties.Add("exp", true);
                }

                properties.Add("t", (int)MemberTypes.Method);

                var parametersInfo = method.Parameters.Select(p => MetadataUtils.ConstructParameterInfo(p, emitter, false, false, tree)).ToList();
                if (parametersInfo.Count > 0)
                {
                    properties.Add("pi", new JArray(parametersInfo));
                }

                if (!string.IsNullOrEmpty(inline))
                {
                    var isSelf = inline.StartsWith("<self>");
                    if (isSelf)
                    {
                        inline = inline.Substring(6);
                    }

                    if(!method.IsStatic && !isSelf && !inline.Contains("{this}"))
                    {
                        inline = "this." + inline;
                    }

                    var block = new InlineArgumentsBlock(emitter, new ArgumentsInfo(emitter, method), inline, method);
                    var oldWriter = block.SaveWriter();
                    block.NewWriter();
                    block.EmitFunctionReference(true);
                    var str = emitter.Output.ToString();

                    block.RestoreWriter(oldWriter);
                    properties.Add("tpc", method.TypeParameters.Count);
                    properties.Add("def", new JRaw(str));
                }
                else
                {
                    if (MetadataUtils.IsJsGeneric(method, emitter))
                    {
                        properties.Add("tpc", method.TypeParameters.Count);
                        properties.Add("tprm", new JArray(method.TypeParameters.Select(tp => tp.Name).ToArray()));
                    }

                    string sname;
                    if (method.IsAccessor)
                    {
                        if (method.AccessorOwner is IProperty)
                        {
                            sname = Helpers.GetPropertyRef(method.AccessorOwner, emitter, ((IProperty)method.AccessorOwner).Setter == method);
                        }
                        else if (method.AccessorOwner is IEvent)
                        {
                            sname = Helpers.GetEventRef(method.AccessorOwner, emitter, ((IEvent)method.AccessorOwner).RemoveAccessor == method);
                        }
                        else
                        {
                            sname = OverloadsCollection.Create(emitter, method).GetOverloadName();
                        }
                    }
                    else
                    {
                        sname = OverloadsCollection.Create(emitter, method).GetOverloadName();
                    }

                    if (sname.Contains("\""))
                    {
                        properties.Add("sn", new JRaw(sname));
                    }
                    else
                    {
                        properties.Add("sn", sname);
                    }
                }
                properties.Add("rt", new JRaw(MetadataUtils.GetTypeName(method.ReturnType, emitter, isGenericSpecialization)));

                var attr = method.GetScriptableAttributes(true, tree);
                if (attr.Any())
                {
                    JArray attrArr = new JArray();
                    foreach (var a in attr)
                    {
                        attrArr.Add(MetadataUtils.ConstructAttribute(a, null, emitter));
                    }

                    properties.Add("rta", attrArr);
                }

                if (method.Parameters.Count > 0)
                {
                    properties.Add("p", new JArray(method.Parameters.Select(p => new JRaw(MetadataUtils.GetTypeName(p.Type, emitter, isGenericSpecialization)))));
                }

                MetadataUtils.AddBox(m, emitter, properties);
            }
            else if (m is IField)
            {
                var field = (IField)m;

                properties.Add("t", (int)MemberTypes.Field);
                properties.Add("rt", new JRaw(MetadataUtils.GetTypeName(field.ReturnType, emitter, isGenericSpecialization)));
                properties.Add("sn", OverloadsCollection.Create(emitter, field).GetOverloadName());
                if (field.IsReadOnly)
                {
                    properties.Add("ro", field.IsReadOnly);
                }

                MetadataUtils.AddBox(m, emitter, properties);
            }
            else if (m is IProperty)
            {
                var typeDef = m.DeclaringTypeDefinition;
                var monoProp = typeDef != null ? typeDef.Properties.FirstOrDefault(p => p.Name == m.Name) : null;

                var prop = (IProperty)m;
                var canGet = prop.CanGet;
                var canSet = prop.CanSet;

                if (monoProp != null)
                {
                    canGet = monoProp.Getter != null;
                    canSet = monoProp.Setter != null;
                }

                properties.Add("t", (int)MemberTypes.Property);
                properties.Add("rt", new JRaw(MetadataUtils.GetTypeName(prop.ReturnType, emitter, isGenericSpecialization)));
                if (prop.Parameters.Count > 0)
                {
                    properties.Add("p", new JArray(prop.Parameters.Select(p => new JRaw(MetadataUtils.GetTypeName(p.Type, emitter, isGenericSpecialization)))));
                }

                if (prop.IsIndexer)
                {
                    properties.Add("i", true);
                }

                if (prop.IsIndexer)
                {
                    if (prop.Getter != null)
                    {
                        var parametersInfo = prop.Getter.Parameters.Select(p => MetadataUtils.ConstructParameterInfo(p, emitter, false, false, tree)).ToList();
                        if (parametersInfo.Count > 0)
                        {
                            properties.Add("ipi", new JArray(parametersInfo));
                        }
                    }
                    else if (prop.Setter != null)
                    {
                        var parametersInfo = prop.Setter.Parameters.Take(prop.Setter.Parameters.Count - 1).Select(p => MetadataUtils.ConstructParameterInfo(p, emitter, false, false, tree)).ToList();
                        if (parametersInfo.Count > 0)
                        {
                            properties.Add("ipi", new JArray(parametersInfo));
                        }
                    }
                }

                var inlineGetter = canGet && (emitter.GetInline(prop.Getter) != null || prop.Getter.IsScript());
                var inlineSetter = canSet && (emitter.GetInline(prop.Setter) != null || prop.Setter.IsScript());

                if (inlineGetter || inlineSetter || prop.IsIndexer)
                {
                    if (canGet)
                    {
                        properties.Add("g", MetadataUtils.ConstructMemberInfo(prop.Getter, emitter, includeDeclaringType, isGenericSpecialization, tree));
                    }

                    if (canSet)
                    {
                        properties.Add("s", MetadataUtils.ConstructMemberInfo(prop.Setter, emitter, includeDeclaringType, isGenericSpecialization, tree));
                    }
                }
                else
                {
                    var fieldName = OverloadsCollection.Create(emitter, prop).GetOverloadName();
                    if (canGet)
                    {
                        properties.Add("g", MetadataUtils.ConstructFieldPropertyAccessor(prop.Getter, emitter, fieldName, true, includeDeclaringType, isGenericSpecialization, tree));
                    }
                    if (canSet)
                    {
                        properties.Add("s", MetadataUtils.ConstructFieldPropertyAccessor(prop.Setter, emitter, fieldName, false, includeDeclaringType, isGenericSpecialization, tree));
                    }

                    properties.Add("fn", fieldName);
                }
            }
            else if (m is IEvent)
            {
                var evt = (IEvent)m;

                properties.Add("t", (int)MemberTypes.Event);
                properties.Add("ad", MetadataUtils.ConstructMemberInfo(evt.AddAccessor, emitter, includeDeclaringType, isGenericSpecialization, tree));
                properties.Add("r", MetadataUtils.ConstructMemberInfo(evt.RemoveAccessor, emitter, includeDeclaringType, isGenericSpecialization, tree));
            }
            else
            {
                throw new ArgumentException("Invalid member " + m);
            }

            return properties;
        }

        private static void AddBox(IMember m, IEmitter emitter, JObject properties)
        {
            bool needBox = ConversionBlock.IsBoxable(m.ReturnType, emitter)
                        || m.ReturnType.IsKnownType(KnownTypeCode.NullableOfT)
                        && ConversionBlock.IsBoxable(NullableType.GetUnderlyingType(m.ReturnType), emitter);

            if (needBox)
            {
                StringBuilder sb = new StringBuilder("function (" + JS.Vars.V + ") { return ");

                sb.Append(JS.Types.Bridge.BOX);
                sb.Append("(" + JS.Vars.V + ", ");
                sb.Append(ConversionBlock.GetBoxedType(m.ReturnType, emitter));

                var inlineMethod = ConversionBlock.GetInlineMethod(emitter, CS.Methods.TOSTRING,
                    emitter.Resolver.Compilation.FindType(KnownTypeCode.String), m.ReturnType, null);

                if (inlineMethod != null)
                {
                    sb.Append(", " + inlineMethod);
                }

                inlineMethod = ConversionBlock.GetInlineMethod(emitter, CS.Methods.GETHASHCODE,
                    emitter.Resolver.Compilation.FindType(KnownTypeCode.Int32), m.ReturnType, null);

                if (inlineMethod != null)
                {
                    sb.Append(", " + inlineMethod);
                }

                sb.Append(");");

                sb.Append("}");
                properties.Add(JS.Fields.BOX, new JRaw(sb.ToString()));
            }
        }

        public static JObject ConstructFieldPropertyAccessor(IMethod m, IEmitter emitter, string fieldName, bool isGetter, bool includeDeclaringType, bool isGenericSpecialization, SyntaxTree tree)
        {
            var properties = MetadataUtils.GetCommonMemberInfoProperties(m, emitter, includeDeclaringType, isGenericSpecialization, tree);
            properties.Add("t", (int)MemberTypes.Method);
            if (m.Parameters.Count > 0)
            {
                properties.Add("p", new JArray(m.Parameters.Select(p => new JRaw(MetadataUtils.GetTypeName(p.Type, emitter, isGenericSpecialization)))));
            }

            properties.Add("rt", new JRaw(MetadataUtils.GetTypeName(m.ReturnType, emitter, isGenericSpecialization)));
            properties.Add(isGetter ? "fg" : "fs", fieldName);
            if (m.IsStatic)
            {
                properties.Add("is", true);
            }

            MetadataUtils.AddBox(m, emitter, properties);

            return properties;
        }

        private static JObject ConstructConstructorInfo(IMethod constructor, IEmitter emitter, bool includeDeclaringType, bool isGenericSpecialization, SyntaxTree tree)
        {
            var properties = MetadataUtils.GetCommonMemberInfoProperties(constructor, emitter, includeDeclaringType, isGenericSpecialization, tree);

            if (constructor.IsNonScriptable())
            {
                return null;
            }

            properties.Add("t", (int)MemberTypes.Constructor);
            if (constructor.Parameters.Count > 0)
            {
                properties.Add("p", new JArray(constructor.Parameters.Select(p => new JRaw(MetadataUtils.GetTypeName(p.Type, emitter, isGenericSpecialization)))));
            }

            var parametersInfo = constructor.Parameters.Select(p => MetadataUtils.ConstructParameterInfo(p, emitter, false, false, tree)).ToList();
            if (parametersInfo.Count > 0)
            {
                properties.Add("pi", new JArray(parametersInfo));
            }

            var inline = emitter.GetInline(constructor);
            var typeDef = constructor.DeclaringTypeDefinition;
            string customCtor = null;
            if (typeDef != null)
            {
                customCtor = typeDef.GetCustomConstructor();
            }

            if (string.IsNullOrEmpty(inline) && customCtor == null)
            {
                string sname;
                if (constructor.IsStatic || constructor.DeclaringType.Kind == TypeKind.Anonymous)
                {
                    sname = JS.Funcs.CONSTRUCTOR;
                }
                else
                {
                    sname = OverloadsCollection.Create(emitter, constructor).GetOverloadName();
                }

                properties.Add("sn", sname);
            }

            if (constructor.IsStatic)
            {
                properties.Add("sm", true);
            }

            if (string.IsNullOrEmpty(inline) && constructor.ExpandParams())
            {
                properties.Add("exp", true);
            }

            if (!string.IsNullOrEmpty(inline))
            {
                var block = new InlineArgumentsBlock(emitter, new ArgumentsInfo(emitter, constructor), inline, constructor);
                var oldWriter = block.SaveWriter();
                block.NewWriter();
                block.EmitFunctionReference(true);
                var str = emitter.Output.ToString();
                block.RestoreWriter(oldWriter);

                properties.Add("def", new JRaw(str));
            }
            else if (customCtor != null)
            {
                inline = customCtor;
                if (Regex.Match(inline, @"\s*\{\s*\}\s*").Success)
                {
                    var names = constructor.Parameters.Select(p => p.Name);

                    StringBuilder sb = new StringBuilder("function (" + string.Join(", ", names.ToArray()) + ") { return {");

                    bool needComma = false;
                    foreach (var name in names)
                    {
                        if (needComma)
                        {
                            sb.Append(", ");
                        }

                        needComma = true;

                        sb.Append(name + ": " + name);
                    }
                    sb.Append("};}");
                    properties.Add("def", new JRaw(sb.ToString()));
                }
                else
                {
                    var block = new InlineArgumentsBlock(emitter, new ArgumentsInfo(emitter, constructor), inline, constructor);
                    var oldWriter = block.SaveWriter();
                    block.NewWriter();
                    block.EmitFunctionReference(true);
                    var str = emitter.Output.ToString();
                    block.RestoreWriter(oldWriter);

                    properties.Add("def", new JRaw(str));
                }
            }

            return properties;
        }

        private static JObject GetCommonMemberInfoProperties(IMember m, IEmitter emitter, bool includeDeclaringType, bool isGenericSpecialization, SyntaxTree tree)
        {
            var result = new JObject();
            var attr = m.GetScriptableAttributes(tree).ToArray();
            if (attr.Any())
            {
                JArray attrArr = new JArray();
                foreach (var a in attr)
                {
                    attrArr.Add(MetadataUtils.ConstructAttribute(a, m.DeclaringTypeDefinition, emitter));
                }

                result.Add("at", attrArr);
            }

            if (includeDeclaringType)
            {
                result.Add("td", new JRaw(MetadataUtils.GetTypeName(m.DeclaringType, emitter, isGenericSpecialization)));
            }

            if (m.IsOverride)
            {
                result.Add("ov", true);
            }

            if (m.IsVirtual)
            {
                result.Add("v", true);
            }

            if (m.IsAbstract)
            {
                result.Add("ab", true);
            }

            if (m.Accessibility != Accessibility.None)
            {
                result.Add("a", (int)m.Accessibility);
            }

            if (m.IsSealed)
            {
                result.Add("sl", true);
            }

            if (m.IsSynthetic)
            {
                result.Add("isSynthetic", true);
            }

            result.Add("n", m.Name);

            return result;
        }

        internal static string GetTypeName(IType type, IEmitter emitter, bool isGenericSpecialization, out int? namespaceKey, bool asDefinition = false)
        {
            namespaceKey = null;
            var typeParam = type as ITypeParameter;
            if (typeParam != null && (typeParam.OwnerType == SymbolKind.Method || typeParam.Owner.IsIgnoreGeneric()))
            {
                return JS.Types.System.Object.NAME;
            }

            var itypeDef = type.GetDefinition();
            if (itypeDef != null && itypeDef.IsNonScriptable())
            {
                return JS.Types.System.Object.NAME;
            }

            var name = emitter.ToJsName(type, asDefinition, skipMethodTypeParam: true);

            if (emitter.NamespacesCache != null && name.StartsWith(type.Namespace + "."))
            {
                int key;
                if (emitter.NamespacesCache.ContainsKey(type.Namespace))
                {
                    namespaceKey = key = emitter.NamespacesCache[type.Namespace];
                }
                else
                {
                    namespaceKey = key = emitter.NamespacesCache.Count;
                    emitter.NamespacesCache.Add(type.Namespace, key);
                }

                name = string.Concat("$n[", key, "]", name.Substring(type.Namespace.Length));
            }

            return name;
        }

        internal static string GetTypeName(IType type, IEmitter emitter, bool isGenericSpecialization, bool asDefinition = false)
        {
            int? namespaceKey;
            return GetTypeName(type, emitter, isGenericSpecialization, out namespaceKey, asDefinition);
        }
    }
}