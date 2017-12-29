using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.NRefactory.CSharp.Resolver;
using Newtonsoft.Json;
using ArrayType = ICSharpCode.NRefactory.TypeSystem.ArrayType;

namespace Bridge.Contract
{
    public static class Helpers
    {
        private static Dictionary<string, string> _replacements;
        private static Regex _convRegex;
        public static string ConvertName(this string name)
        {
            if (_convRegex == null)
            {
                _replacements = new Dictionary<string, string>(4);
                _replacements.Add("`", JS.Vars.D.ToString());
                _replacements.Add("/", ".");
                _replacements.Add("+", ".");
                _replacements.Add("[", "");
                _replacements.Add("]", "");
                _replacements.Add("&", "");

                _convRegex = new Regex("(\\" + string.Join("|\\", _replacements.Keys.ToArray()) + ")", RegexOptions.Compiled | RegexOptions.Singleline);
            }

            return _convRegex.Replace
            (
                name,
                delegate (Match m)
                {
                    return _replacements[m.Value];
                }
            );
        }

        public static string ToJavaScript(this object value)
        {
            return JsonConvert.SerializeObject(value);
        }

        public static string GetGlobalTarget(this ITypeDefinition typeDefinition, AstNode node, bool removeGlobal = false)
        {
            string globalTarget = null;
            var globalMethods = typeDefinition.HasGlobalMethodsAttribute();
            if (globalMethods.HasValue)
            {
                globalTarget = !removeGlobal || globalMethods.Value ? JS.Types.Bridge.Global.NAME : "";
            }
            else
            {
                try
                {
                    var mixin = typeDefinition.GetMixin();
                    globalTarget = mixin;
                }
                catch (InvalidDataException e)
                {
                    throw new EmitterException(node, e.Message);
                }
            }

            return globalTarget;
        }

        public static bool IsRightAssigmentExpression(this AssignmentExpression expression)
        {
            return (expression.Right is ParenthesizedExpression ||
                    expression.Right is IdentifierExpression ||
                    expression.Right is MemberReferenceExpression ||
                    expression.Right is PrimitiveExpression ||
                    expression.Right is IndexerExpression ||
                    expression.Right is LambdaExpression ||
                    expression.Right is AnonymousMethodExpression ||
                    expression.Right is ObjectCreateExpression);
        }


        public static void CheckIdentifier(string name, AstNode context)
        {
            if (IsReservedWord(null, name))
            {
                throw new EmitterException(context, "Cannot use '" + name + "' as identifier");
            }
        }

        public static bool IsDelegateOrLambda(this ResolveResult result)
        {
            return result.Type.Kind == ICSharpCode.NRefactory.TypeSystem.TypeKind.Delegate || result is LambdaResolveResult;
        }

        public static bool IsBridgeClass(this ITypeDefinition type)
        {
            foreach (var i in type.GetAllBaseTypes())
            {
                if (i.FullName == JS.Types.BRIDGE_IBridgeClass)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsValueType(this ITypeDefinition type)
        {
            return (type.Kind == TypeKind.Struct || type.Kind == TypeKind.Enum);
        }

        public static bool IsDefaultStructConstructor(this IMethod method)
        {
            return method.Parameters.Count == 0 && method.DeclaringTypeDefinition.Kind == TypeKind.Struct;
        }

        public static void AcceptChildren(this AstNode node, IAstVisitor visitor)
        {
            foreach (AstNode child in node.Children)
            {
                child.AcceptVisitor(visitor);
            }
        }

        public static ITypeDefinition GetBaseClassDefinition(this IType type)
        {
            if (type.FullName == "System.Object")
            {
                return null;
            }
            return type.DirectBaseTypes.Select(t => t.GetDefinition() ).FirstOrDefault(t => t != null && t.Kind != TypeKind.Interface);

        }

        public static string ReplaceSpecialChars(string name)
        {
            return name.Replace('`', JS.Vars.D).Replace('/', '.').Replace("+", ".");
        }

        public static bool HasGenericArgument(IType type, ITypeDefinition searchType, IEmitter emitter, bool deep)
        {
            foreach (var gArg in type.TypeArguments)
            {
                var typeDefinition = gArg.GetDefinition();
                if (typeDefinition != null)
                {
                    // types equal directly
                    if (typeDefinition.FullTypeName.Equals(searchType.FullTypeName))
                    {
                        return true;
                    }

                    // or in case of deep type searching we check if the type argument 
                    // is a subclass or a class implementing the interface
                    if (deep && IsAssignableFrom(typeDefinition, searchType, emitter))
                    {
                        return true;
                    }

                    // if the type argument itself is again generic, search there too: 
                    // e.g.     interface ITest<X> { }
                    // class Test : List< ITest<X> >
                    if (gArg.TypeArguments.Count > 0)
                    {
                        var result = HasGenericArgument(gArg, searchType, emitter, deep);
                        if (result)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsTypeArgInSubclass(ITypeDefinition thisTypeDefinition, ITypeDefinition typeArgDefinition, IEmitter emitter, bool deep = true)
        {
            foreach (var interfaceReference in thisTypeDefinition.GetAllBaseTypes().Where(t=>t.Kind == TypeKind.Interface))
            {
                if (interfaceReference.TypeArguments.Count > 0 && Helpers.HasGenericArgument(interfaceReference, typeArgDefinition, emitter, deep))
                {
                    return true;
                }
            }

            var baseType = thisTypeDefinition.GetBaseClassDefinition();
            if (baseType != null)
            {
                if (baseType.TypeParameterCount > 0 && Helpers.HasGenericArgument(baseType, typeArgDefinition, emitter, deep))
                {
                    return true;
                }

                if (deep)
                {
                    return Helpers.IsTypeArgInSubclass(baseType, typeArgDefinition, emitter);
                }
            }
            return false;
        }

        public static bool IsAssignableFrom(ITypeDefinition thisTypeDefinition, ITypeDefinition typeDefinition, IEmitter emitter)
        {
            if (thisTypeDefinition.FullTypeName.Equals(typeDefinition.FullTypeName))
            {
                return true;
            }

            // get all base types and look for matching type
            var allBaseTypes = thisTypeDefinition.GetAllBaseTypeDefinitions();
            foreach (var baseType in allBaseTypes)
            {
                if (baseType.FullTypeName.Equals(typeDefinition.FullTypeName))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsIntegerType(IType type, IMemberResolver resolver)
        {
            type = type.IsKnownType(KnownTypeCode.NullableOfT) ? ((ParameterizedType)type).TypeArguments[0] : type;

            return type.Equals(resolver.Compilation.FindType(KnownTypeCode.Byte))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.SByte))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Char))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Int16))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.UInt16))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Int32))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.UInt32))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Int64))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.UInt64));
        }

        public static bool IsInteger32Type(IType type, IMemberResolver resolver)
        {
            type = type.IsKnownType(KnownTypeCode.NullableOfT) ? ((ParameterizedType)type).TypeArguments[0] : type;

            return type.Equals(resolver.Compilation.FindType(KnownTypeCode.Int32))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.UInt32));
        }

        public static bool IsFloatType(IType type, IMemberResolver resolver)
        {
            type = type.IsKnownType(KnownTypeCode.NullableOfT) ? ((ParameterizedType)type).TypeArguments[0] : type;

            return type.Equals(resolver.Compilation.FindType(KnownTypeCode.Decimal))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Double))
                || type.Equals(resolver.Compilation.FindType(KnownTypeCode.Single));
        }

        public static bool IsDecimalType(IType type, IMemberResolver resolver, bool allowArray = false)
        {
            return Helpers.IsKnownType(KnownTypeCode.Decimal, type, resolver, allowArray);
        }

        public static bool IsLongType(IType type, IMemberResolver resolver, bool allowArray = false)
        {
            return Helpers.IsKnownType(KnownTypeCode.Int64, type, resolver, allowArray);
        }

        public static bool IsULongType(IType type, IMemberResolver resolver, bool allowArray = false)
        {
            return Helpers.IsKnownType(KnownTypeCode.UInt64, type, resolver, allowArray);
        }

        public static bool Is64Type(IType type, IMemberResolver resolver, bool allowArray = false)
        {
            return Helpers.IsKnownType(KnownTypeCode.UInt64, type, resolver, allowArray) || Helpers.IsKnownType(KnownTypeCode.Int64, type, resolver, allowArray);
        }

        public static bool IsKnownType(KnownTypeCode typeCode, IType type, IMemberResolver resolver, bool allowArray = false)
        {
            if (allowArray && type.Kind == TypeKind.Array)
            {
                var elements = (TypeWithElementType)type;
                type = elements.ElementType;
            }

            type = type.IsKnownType(KnownTypeCode.NullableOfT) ? ((ParameterizedType)type).TypeArguments[0] : type;

            return type.Equals(resolver.Compilation.FindType(typeCode));
        }

        public static void CheckValueTypeClone(ResolveResult resolveResult, Expression expression, IAbstractEmitterBlock block, int insertPosition)
        {
            if (resolveResult == null || resolveResult.IsError)
            {
                return;
            }

            if (block.Emitter.IsAssignment)
            {
                return;
            }

            var conversion = block.Emitter.Resolver.Resolver.GetConversion(expression);
            if (block.Emitter.Rules.Boxing == BoxingRule.Managed && (conversion.IsBoxingConversion || conversion.IsUnboxingConversion))
            {
                return;
            }

            bool writeClone = false;
            if (resolveResult is InvocationResolveResult)
            {
                bool ret = true;
                if (expression.Parent is InvocationExpression)
                {
                    var invocationExpression = (InvocationExpression)expression.Parent;
                    if (invocationExpression.Arguments.Any(a => a == expression))
                    {
                        ret = false;
                    }
                }
                else if (expression.Parent is AssignmentExpression)
                {
                    ret = false;
                }
                else if (expression.Parent is VariableInitializer)
                {
                    ret = false;
                }
                else
                {
                    var prop = (resolveResult as MemberResolveResult)?.Member as IProperty;

                    if (prop != null && prop.IsIndexer)
                    {
                        ret = false;
                        writeClone = true;
                    }
                }

                if (ret)
                {
                    return;
                }
            }

            var rrtype = resolveResult.Type;
            var nullable = rrtype.IsKnownType(KnownTypeCode.NullableOfT);

            var forEachResolveResult = resolveResult as ForEachResolveResult;
            if (forEachResolveResult != null)
            {
                rrtype = forEachResolveResult.ElementType;
            }

            var type = nullable ? ((ParameterizedType)rrtype).TypeArguments[0] : rrtype;
            if (type.Kind == TypeKind.Struct)
            {
                if (Helpers.IsImmutableStruct(block.Emitter, type))
                {
                    return;
                }

                if (writeClone)
                {
                    Helpers.WriteClone(block, insertPosition, nullable);
                    return;
                }

                var memberResult = resolveResult as MemberResolveResult;

                var field = memberResult != null ? memberResult.Member as DefaultResolvedField : null;

                if (field != null && field.IsReadOnly)
                {
                    Helpers.WriteClone(block, insertPosition, nullable);
                    return;
                }

                var isOperator = false;
                if (expression != null &&
                    (expression.Parent is BinaryOperatorExpression || expression.Parent is UnaryOperatorExpression))
                {
                    var orr = block.Emitter.Resolver.ResolveNode(expression.Parent) as OperatorResolveResult;

                    isOperator = orr != null && orr.UserDefinedOperatorMethod != null;
                }

                if (expression == null || isOperator ||
                    expression.Parent is NamedExpression ||
                    expression.Parent is ObjectCreateExpression ||
                    expression.Parent is ArrayInitializerExpression ||
                    expression.Parent is ReturnStatement ||
                    expression.Parent is InvocationExpression ||
                    expression.Parent is AssignmentExpression ||
                    expression.Parent is VariableInitializer ||
                    expression.Parent is ForeachStatement && resolveResult is ForEachResolveResult)
                {
                    if (expression != null && expression.Parent is InvocationExpression)
                    {
                        var invocationExpression = (InvocationExpression)expression.Parent;
                        if (invocationExpression.Target == expression)
                        {
                            return;
                        }
                    }

                    Helpers.WriteClone(block, insertPosition, nullable);
                }
            }
        }

        private static void WriteClone(IAbstractEmitterBlock block, int insertPosition, bool nullable)
        {
            if (nullable)
            {
                block.Emitter.Output.Insert(insertPosition,
                    JS.Types.SYSTEM_NULLABLE + "." + JS.Funcs.Math.LIFT1 + "(\"" + JS.Funcs.CLONE + "\", ");
                block.WriteCloseParentheses();
            }
            else
            {
                block.Write("." + JS.Funcs.CLONE + "()");
            }
        }

        public static bool IsImmutableStruct(IEmitter emitter, IType type)
        {
            if (type.Kind != TypeKind.Struct)
            {
                return true;
            }

            if (type.GetDefinition().IsExternal() || type.GetDefinition().IsImmutableType())
            {
                return true;
            }

            var typeDef = type.GetDefinition();
            var mutableFields = type.GetFields(f => !f.IsReadOnly && !f.IsConst, GetMemberOptions.IgnoreInheritedMembers);
            var autoProps = typeDef.Properties.Where(Helpers.IsAutoProperty);
            var autoEvents = type.GetEvents(null, GetMemberOptions.IgnoreInheritedMembers);

            if (!mutableFields.Any() && !autoProps.Any() && !autoEvents.Any())
            {
                return true;
            }
            return false;
        }



        public static bool IsAutoProperty(IProperty propertyDeclaration)
        {
            if (propertyDeclaration.CanGet && propertyDeclaration.Getter.IsScript())
            {
                return false;
            }

            if (propertyDeclaration.CanSet && propertyDeclaration.Setter.IsScript())
            {
                return false;
            }
            // auto properties don't have bodies
            return (propertyDeclaration.CanGet && (!propertyDeclaration.Getter.HasBody || propertyDeclaration.Getter.BodyRegion.IsEmpty)) ||
                   (propertyDeclaration.CanSet && (!propertyDeclaration.Setter.HasBody || propertyDeclaration.Setter.BodyRegion.IsEmpty));
        }

        public static string GetAddOrRemove(bool isAdd, string name = null)
        {
            return (isAdd ? JS.Funcs.Event.ADD : JS.Funcs.Event.REMOVE) + name;
        }

        public static string GetEventRef(CustomEventDeclaration property, IEmitter emitter, bool remove = false, bool noOverload = false, bool ignoreInterface = false, bool withoutTypeParams = false)
        {
            MemberResolveResult resolveResult = emitter.Resolver.ResolveNode(property) as MemberResolveResult;
            if (resolveResult != null && resolveResult.Member != null)
            {
                return Helpers.GetEventRef(resolveResult.Member, emitter, remove, noOverload, ignoreInterface, withoutTypeParams);
            }

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, remove);
                return overloads.GetOverloadName(ignoreInterface, Helpers.GetAddOrRemove(!remove), withoutTypeParams);
            }

            var name = emitter.GetEntityName(property);
            return Helpers.GetAddOrRemove(!remove, name);
        }

        public static string GetEventRef(IMember property, IEmitter emitter, bool remove = false, bool noOverload = false, bool ignoreInterface = false, bool withoutTypeParams = false, bool skipPrefix = false)
        {
            var attrName = emitter.GetEntityNameFromAttr(property, remove);

            if (!String.IsNullOrEmpty(attrName))
            {
                return Helpers.AddInterfacePrefix(property, emitter, ignoreInterface, attrName, remove);
            }

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, remove);
                return overloads.GetOverloadName(ignoreInterface, skipPrefix ? null : Helpers.GetAddOrRemove(!remove), withoutTypeParams);
            }

            var name = emitter.GetEntityName(property);
            return skipPrefix ? name : Helpers.GetAddOrRemove(!remove, name);
        }

        public static string GetSetOrGet(bool isSetter, string name = null)
        {
            return (isSetter ? JS.Funcs.Property.SET : JS.Funcs.Property.GET) + name;
        }

        public static string GetPropertyRef(PropertyDeclaration property, IEmitter emitter, bool isSetter = false, bool noOverload = false, bool ignoreInterface = false, bool withoutTypeParams = false, bool skipPrefix = true)
        {
            ResolveResult resolveResult = emitter.Resolver.ResolveNode(property) as MemberResolveResult;
            if (resolveResult != null && ((MemberResolveResult)resolveResult).Member != null)
            {
                return Helpers.GetPropertyRef(((MemberResolveResult)resolveResult).Member, emitter, isSetter, noOverload, ignoreInterface, withoutTypeParams, skipPrefix);
            }

            string name;

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, isSetter);
                return overloads.GetOverloadName(ignoreInterface, skipPrefix ? null : Helpers.GetSetOrGet(isSetter), withoutTypeParams);
            }

            name = emitter.GetEntityName(property);
            return Helpers.GetSetOrGet(isSetter, name);
        }

        public static string GetPropertyRef(IndexerDeclaration property, IEmitter emitter, bool isSetter = false, bool noOverload = false, bool ignoreInterface = false)
        {
            ResolveResult resolveResult = emitter.Resolver.ResolveNode(property) as MemberResolveResult;
            if (resolveResult != null && ((MemberResolveResult)resolveResult).Member != null)
            {
                return Helpers.GetIndexerRef(((MemberResolveResult)resolveResult).Member, emitter, isSetter, noOverload, ignoreInterface);
            }

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, isSetter);
                return overloads.GetOverloadName(ignoreInterface, Helpers.GetSetOrGet(isSetter));
            }

            var name = emitter.GetEntityName(property);
            return Helpers.GetSetOrGet(isSetter, name);
        }

        public static string GetIndexerRef(IMember property, IEmitter emitter, bool isSetter = false, bool noOverload = false, bool ignoreInterface = false)
        {
            var attrName = emitter.GetEntityNameFromAttr(property, isSetter);

            if (!String.IsNullOrEmpty(attrName))
            {
                return Helpers.AddInterfacePrefix(property, emitter, ignoreInterface, attrName, isSetter);
            }

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, isSetter);
                return overloads.GetOverloadName(ignoreInterface, Helpers.GetSetOrGet(isSetter));
            }

            var name = emitter.GetEntityName(property);
            return Helpers.GetSetOrGet(isSetter, name);
        }

        public static string GetPropertyRef(IMember property, IEmitter emitter, bool isSetter = false, bool noOverload = false, bool ignoreInterface = false, bool withoutTypeParams = false, bool skipPrefix = true)
        {
            var attrName = emitter.GetEntityNameFromAttr(property, isSetter);

            if (!String.IsNullOrEmpty(attrName))
            {
                return Helpers.AddInterfacePrefix(property, emitter, ignoreInterface, attrName, isSetter);
            }

            string name = null;

            if (property.SymbolKind == SymbolKind.Indexer)
            {
                skipPrefix = false;
            }

            if (!noOverload)
            {
                var overloads = OverloadsCollection.Create(emitter, property, isSetter);
                return overloads.GetOverloadName(ignoreInterface, skipPrefix ? null : Helpers.GetSetOrGet(isSetter), withoutTypeParams);
            }

            name = emitter.GetEntityName(property);
            return skipPrefix ? name : Helpers.GetSetOrGet(isSetter, name);
        }

        private static string AddInterfacePrefix(IMember property, IEmitter emitter, bool ignoreInterface, string attrName, bool isSetter)
        {
            IMember interfaceMember = null;
            if (property.IsExplicitInterfaceImplementation)
            {
                interfaceMember = property.ImplementedInterfaceMembers.First();
            }
            else if (property.DeclaringTypeDefinition != null && property.DeclaringTypeDefinition.Kind == TypeKind.Interface)
            {
                interfaceMember = property;
            }

            if (interfaceMember != null && !ignoreInterface)
            {
                return OverloadsCollection.GetInterfaceMemberName(emitter, interfaceMember, attrName, null, false, isSetter);
            }

            return attrName;
        }

        public static bool IsReservedWord(IEmitter emitter, string word)
        {
            if (emitter != null && (emitter.TypeInfo.JsName == word || emitter.TypeInfo.JsName.StartsWith(word + ".")))
            {
                return true;
            }
            return JS.Reserved.Words.Contains(word);
        }

        public static string ChangeReservedWord(string name)
        {
            return Helpers.PrefixDollar(name);
        }

        public static object GetEnumValue(IEmitter emitter, IType type, object constantValue)
        {
            var enumMode = type.GetDefinition().EnumEmitMode();

            if ((type.GetDefinition().IsExternal() && enumMode == -1) || enumMode == 2)
            {
                return constantValue;
            }

            if (enumMode >= 3 && enumMode < 7)
            {
                var member = type.GetFields().FirstOrDefault(f => f.ConstantValue != null && f.ConstantValue.Equals(constantValue));

                if (member == null)
                {
                    return constantValue;
                }

                string enumStringName = member.Name;
                if (member.HasNameAttribute())
                {
                    enumStringName = emitter.GetEntityName(member);
                }
                else
                {
                    switch (enumMode)
                    {
                        case 3:
                            enumStringName = member.Name.Substring(0, 1).ToLower(CultureInfo.InvariantCulture) + member.Name.Substring(1);
                            break;

                        case 4:
                            break;

                        case 5:
                            enumStringName = enumStringName.ToLowerInvariant();
                            break;

                        case 6:
                            enumStringName = enumStringName.ToUpperInvariant();
                            break;
                    }
                }

                return enumStringName;
            }

            return constantValue;
        }

        public static string GetBinaryOperatorMethodName(BinaryOperatorType operatorType)
        {
            switch (operatorType)
            {
                case BinaryOperatorType.Any:
                    return null;

                case BinaryOperatorType.BitwiseAnd:
                    return "op_BitwiseAnd";

                case BinaryOperatorType.BitwiseOr:
                    return "op_BitwiseOr";

                case BinaryOperatorType.ConditionalAnd:
                    return "op_LogicalAnd";

                case BinaryOperatorType.ConditionalOr:
                    return "op_LogicalOr";

                case BinaryOperatorType.ExclusiveOr:
                    return "op_ExclusiveOr";

                case BinaryOperatorType.GreaterThan:
                    return "op_GreaterThan";

                case BinaryOperatorType.GreaterThanOrEqual:
                    return "op_GreaterThanOrEqual";

                case BinaryOperatorType.Equality:
                    return "op_Equality";

                case BinaryOperatorType.InEquality:
                    return "op_Inequality";

                case BinaryOperatorType.LessThan:
                    return "op_LessThan";

                case BinaryOperatorType.LessThanOrEqual:
                    return "op_LessThanOrEqual";

                case BinaryOperatorType.Add:
                    return "op_Addition";

                case BinaryOperatorType.Subtract:
                    return "op_Subtraction";

                case BinaryOperatorType.Multiply:
                    return "op_Multiply";

                case BinaryOperatorType.Divide:
                    return "op_Division";

                case BinaryOperatorType.Modulus:
                    return "op_Modulus";

                case BinaryOperatorType.ShiftLeft:
                    return "LeftShift";

                case BinaryOperatorType.ShiftRight:
                    return "RightShift";

                case BinaryOperatorType.NullCoalescing:
                    return null;

                default:
                    throw new ArgumentOutOfRangeException("operatorType", operatorType, null);
            }
        }

        public static string GetUnaryOperatorMethodName(UnaryOperatorType operatorType)
        {
            switch (operatorType)
            {
                case UnaryOperatorType.Any:
                    return null;

                case UnaryOperatorType.Not:
                    return "op_LogicalNot";

                case UnaryOperatorType.BitNot:
                    return "op_OnesComplement";

                case UnaryOperatorType.Minus:
                    return "op_UnaryNegation";

                case UnaryOperatorType.Plus:
                    return "op_UnaryPlus";

                case UnaryOperatorType.Increment:
                case UnaryOperatorType.PostIncrement:
                    return "op_Increment";

                case UnaryOperatorType.Decrement:
                case UnaryOperatorType.PostDecrement:
                    return "op_Decrement";

                case UnaryOperatorType.Dereference:
                    return null;

                case UnaryOperatorType.AddressOf:
                    return null;

                case UnaryOperatorType.Await:
                    return null;

                default:
                    throw new ArgumentOutOfRangeException("operatorType", operatorType, null);
            }
        }

        public static BinaryOperatorType TypeOfAssignment(AssignmentOperatorType operatorType)
        {
            switch (operatorType)
            {
                case AssignmentOperatorType.Assign:
                    return BinaryOperatorType.Any;

                case AssignmentOperatorType.Add:
                    return BinaryOperatorType.Add;

                case AssignmentOperatorType.Subtract:
                    return BinaryOperatorType.Subtract;

                case AssignmentOperatorType.Multiply:
                    return BinaryOperatorType.Multiply;

                case AssignmentOperatorType.Divide:
                    return BinaryOperatorType.Divide;

                case AssignmentOperatorType.Modulus:
                    return BinaryOperatorType.Modulus;

                case AssignmentOperatorType.ShiftLeft:
                    return BinaryOperatorType.ShiftLeft;

                case AssignmentOperatorType.ShiftRight:
                    return BinaryOperatorType.ShiftRight;

                case AssignmentOperatorType.BitwiseAnd:
                    return BinaryOperatorType.BitwiseAnd;

                case AssignmentOperatorType.BitwiseOr:
                    return BinaryOperatorType.BitwiseOr;

                case AssignmentOperatorType.ExclusiveOr:
                    return BinaryOperatorType.ExclusiveOr;

                case AssignmentOperatorType.Any:
                    return BinaryOperatorType.Any;

                default:
                    throw new ArgumentOutOfRangeException("operatorType", operatorType, null);
            }
        }



        public static string GetTypedArrayName(IType elementType)
        {
            switch (elementType.FullName)
            {
                case CS.Types.System_Byte:
                    return JS.Types.Uint8Array;

                case CS.Types.System_SByte:
                    return JS.Types.Int8Array;

                case CS.Types.System_Int16:
                    return JS.Types.Int16Array;

                case CS.Types.System_UInt16:
                    return JS.Types.Uint16Array;

                case CS.Types.System_Int32:
                    return JS.Types.Int32Array;

                case CS.Types.System_UInt32:
                    return JS.Types.Uint32Array;

                case CS.Types.System_Single:
                    return JS.Types.Float32Array;

                case CS.Types.System_Double:
                    return JS.Types.Float64Array;
            }
            return null;
        }

        public static string PrefixDollar(params object[] parts)
        {
            return JS.Vars.D + String.Join("", parts);
        }

        public static string ReplaceFirstDollar(string s)
        {
            if (s == null)
            {
                return s;
            }

            if (s.StartsWith(JS.Vars.D.ToString()))
            {
                return s.Substring(1);
            }

            return s;
        }

        public static bool IsEntryPointMethod(IEmitter emitter, MethodDeclaration methodDeclaration)
        {
            var member_rr = emitter.Resolver.ResolveNode(methodDeclaration) as MemberResolveResult;
            IMethod method = member_rr != null ? member_rr.Member as IMethod : null;

            return Helpers.IsEntryPointMethod(method);
        }

        public static bool IsEntryPointMethod(IMethod method)
        {
            if (method != null && method.Name == CS.Methods.AUTO_STARTUP_METHOD_NAME &&
                method.IsStatic &&
                !method.IsAbstract &&
                Helpers.IsEntryPointCandidate(method))
            {
                bool isReady = method.HasReadyAttribute();
                if (!isReady)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEntryPointCandidate(IEmitter emitter, MethodDeclaration methodDeclaration)
        {
            if (methodDeclaration == null)
            {
                return false;
            }

            var m_rr = emitter.Resolver.ResolveNode(methodDeclaration) as MemberResolveResult;

            if (m_rr == null || !(m_rr.Member is IMethod))
            {
                return false;
            }

            var m = (IMethod)m_rr.Member;

            return Helpers.IsEntryPointCandidate(m);
        }

        public static bool IsEntryPointCandidate(IMethod m)
        {
            if (m.Name != CS.Methods.AUTO_STARTUP_METHOD_NAME || !m.IsStatic || m.DeclaringTypeDefinition.TypeParameterCount > 0 ||
                m.TypeParameters.Count > 0) // Must be a static, non-generic Main
                return false;
            if (!m.ReturnType.IsKnownType(KnownTypeCode.Void) && !m.ReturnType.IsKnownType(KnownTypeCode.Int32))
                // Must return void or int.
                return false;
            if (m.Parameters.Count == 0) // Can have 0 parameters.
                return true;
            if (m.Parameters.Count > 1) // May not have more than 1 parameter.
                return false;
            if (m.Parameters[0].IsRef || m.Parameters[0].IsOut) // The single parameter must not be ref or out.
                return false;

            var at = m.Parameters[0].Type as ArrayType;
            return at != null && at.Dimensions == 1 && at.ElementType.IsKnownType(KnownTypeCode.String);
            // The single parameter must be a one-dimensional array of strings.
        }

        public static bool IsTypeParameterType(IType type)
        {
            var typeDef = type.GetDefinition();
            if (typeDef != null && typeDef.IsIgnoreGeneric())
            {
                return false;
            }
            return type.TypeArguments.Any(Helpers.HasTypeParameters);
        }

        public static bool HasTypeParameters(IType type)
        {
            if (type.Kind == TypeKind.TypeParameter)
            {
                return true;
            }

            if (type.TypeArguments.Count > 0)
            {
                foreach (var typeArgument in type.TypeArguments)
                {
                    var typeDef = typeArgument.GetDefinition();
                    if (typeDef != null && typeDef.IsIgnoreGeneric())
                    {
                        continue;
                    }

                    if (Helpers.HasTypeParameters(typeArgument))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Regex validIdentifier = new Regex("^[$A-Z_][0-9A-Z_$]*$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        public static bool IsValidIdentifier(string name)
        {
            return Helpers.validIdentifier.IsMatch(name);
        }


        public static bool IsValueEnum(IType type)
        {
            return type.GetDefinition().EnumEmitMode() == 2;
        }

        public static bool IsNameEnum(IType type)
        {
            var enumEmitMode = type.GetDefinition().EnumEmitMode();
            return enumEmitMode == 1 || enumEmitMode > 6;
        }

        public static bool IsStringNameEnum(IType type)
        {
            var mode = type.GetDefinition().EnumEmitMode();
            return mode >= 3 && mode <= 6;
        }

        public static bool IsReservedStaticName(string name, bool ignoreCase = true)
        {
            return JS.Reserved.StaticNames.Any(n => String.Equals(name, n, ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture));
        }

        public static string GetFunctionName(NamedFunctionMode mode, IMember member, IEmitter emitter, bool isSetter = false)
        {
            var overloads = OverloadsCollection.Create(emitter, member, isSetter);
            string name = null;
            switch (mode)
            {
                case NamedFunctionMode.None:
                    break;
                case NamedFunctionMode.Name:
                    name = overloads.GetOverloadName(false, null, true);
                    break;
                case NamedFunctionMode.FullName:
                    var td = member.DeclaringTypeDefinition;
                    name = td != null ? emitter.ToJsName(td, true) : "";
                    name = name.Replace(".", "_");
                    name += "_" + overloads.GetOverloadName(false, null, true);
                    break;
                case NamedFunctionMode.ClassName:
                    var t = member.DeclaringType;
                    name = emitter.ToJsName(t, true, true);
                    name = name.Replace(".", "_");
                    name += "_" + overloads.GetOverloadName(false, null, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (name != null)
            {
                if (member is IProperty)
                {
                    name = name + "_" + (isSetter ? "set" : "get");
                }
                else if (member is IEvent)
                {
                    name = name + "_" + (isSetter ? "remove" : "add");
                }
            }

            return name;
        }

        public static bool HasThis(string template)
        {
            return template.IndexOf("{this}", StringComparison.Ordinal) > -1 || template.IndexOf("{$}", StringComparison.Ordinal) > -1;
        }

        public static string ConvertTokens(IEmitter emitter, string template, IMember member)
        {
            string name = OverloadsCollection.Create(emitter, member).GetOverloadName(true);
            return template.Replace("{@}", name).Replace("{$}", "{this}." + name);
        }

        public static string ConvertNameTokens(string name, string replacer)
        {
            return name.Replace("{@}", replacer).Replace("{$}", replacer);
        }

        public static string ReplaceThis(IEmitter emitter, string template, string replacer, IMember member)
        {
            template = Helpers.ConvertTokens(emitter, template, member);
            return template.Replace("{this}", replacer);
        }

        public static string DelegateToTemplate(string tpl, IMethod method, IEmitter emitter)
        {
            bool addThis = !method.IsStatic;

            StringBuilder sb = new StringBuilder(tpl);
            sb.Append("(");

            bool comma = false;
            if (addThis)
            {
                sb.Append("{this}");
                comma = true;
            }

            if (!method.IsIgnoreGeneric() && method.TypeArguments.Count > 0)
            {
                foreach (var typeParameter in method.TypeArguments)
                {
                    if (comma)
                    {
                        sb.Append(", ");
                    }

                    if (typeParameter.Kind == TypeKind.TypeParameter)
                    {
                        sb.Append("{");
                        sb.Append(typeParameter.Name);
                        sb.Append("}");
                    }
                    else
                    {
                        sb.Append(emitter.ToJsName(typeParameter));
                    }
                    comma = true;
                }
            }

            foreach (var parameter in method.Parameters)
            {
                if (comma)
                {
                    sb.Append(", ");
                }

                sb.Append("{");

                if (parameter.IsParams &&
                    method.ExpandParams())
                {
                    sb.Append("*");
                }

                sb.Append(parameter.Name);
                sb.Append("}");
                comma = true;
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}