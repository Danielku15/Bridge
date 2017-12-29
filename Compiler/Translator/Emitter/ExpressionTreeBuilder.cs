using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Expression = System.Linq.Expressions.Expression;

namespace Bridge.Translator
{
    public class ExpressionTreeBuilder : ResolveResultVisitor<string, object>
    {
        private readonly ICompilation _compilation;
        private readonly ITypeDefinition _expression;
        private readonly Dictionary<IVariable, string> _allParameters;
        private readonly SyntaxTree _syntaxTree;
        private readonly IEmitter _emitter;
        private readonly AbstractEmitterBlock _block;

        public ExpressionTreeBuilder(ICompilation compilation, IEmitter emitter, SyntaxTree syntaxTree, AbstractEmitterBlock block)
        {
            this._compilation = compilation;
            this._emitter = emitter;
            this._syntaxTree = syntaxTree;
            this._expression = (ITypeDefinition)ReflectionHelper.ParseReflectionName(typeof(Expression).FullName).Resolve(compilation);
            this._allParameters = new Dictionary<IVariable, string>();
            this._block = block;
        }

        private bool TypesMatch(IMethod method, Type[] argumentTypes)
        {
            if (method.Parameters.Count != argumentTypes.Length)
                return false;
            for (int i = 0; i < argumentTypes.Length; i++)
            {
                if (!method.Parameters[i].Type.Equals(ReflectionHelper.ParseReflectionName(argumentTypes[i].FullName).Resolve(this._compilation)))
                    return false;
            }
            return true;
        }

        private string CompileMethodCall(IMethod m, string[] a)
        {
            string inlineCode = this._emitter.GetInline(m);
            var argsInfo = new ArgumentsInfo(this._emitter, a);
            var block = new InlineArgumentsBlock(this._emitter, argsInfo, inlineCode, m);
            var oldWriter = this._block.SaveWriter();
            var sb = this._block.NewWriter();
            block.Emit();
            string result = sb.ToString();
            this._block.RestoreWriter(oldWriter);

            return result;
        }

        private string CompileFactoryCall(string factoryMethodName, Type[] argumentTypes, string[] arguments)
        {
            var method = this._expression.Methods.Single(m => m.Name == factoryMethodName && m.TypeParameters.Count == 0 && this.TypesMatch(m, argumentTypes));
            return this.CompileMethodCall(method, arguments);
        }

        public string BuildExpressionTree(LambdaResolveResult lambda)
        {
            return this.VisitLambdaResolveResult(lambda, null);
        }

        public override string VisitResolveResult(ResolveResult rr, object data)
        {
            if (rr.IsError)
            {
                throw new InvalidOperationException("ResolveResult" + rr + " is an error.");
            }

            return base.VisitResolveResult(rr, data);
        }

        public override string VisitLambdaResolveResult(LambdaResolveResult rr, object data)
        {
            var parameters = new JRaw[rr.Parameters.Count];
            var map = new Dictionary<string, string>();

            for (int i = 0; i < rr.Parameters.Count; i++)
            {
                var temp = this._block.GetTempVarName();
                this._allParameters[rr.Parameters[i]] = temp;
                parameters[i] = new JRaw(temp);

                map.Add(temp, this.CompileFactoryCall("Parameter", new[] { typeof(Type), typeof(string) }, new[] { GetTypeName(rr.Parameters[i].Type, this._emitter), rr.Parameters[i].Name.ToJavaScript() }));
            }

            var body = this.VisitResolveResult(rr.Body, null);
            var lambda = this.CompileFactoryCall("Lambda", new[] { typeof(Expression), typeof(ParameterExpression[]) }, new[] { body, parameters.ToJavaScript() });

            if (map.Count > 0)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("(");

                foreach (var item in map)
                {
                    sb.Append(item.Key);
                    sb.Append(" = ");
                    sb.Append(item.Value);
                    sb.Append(", ");
                }

                sb.Append(lambda);
                sb.Append(")");

                return sb.ToString();
            }

            return lambda;
        }

        public string GetExpressionForLocal(string name, string accessor, IType type)
        {
            var scriptType = GetTypeName(type, this._emitter);

            string getterDefinition = "function (){ return " + accessor + "}";
            string setterDefinition = "function ($){ " + accessor + " = $; }";

            /*if (UsesThisVisitor.Analyze(accessor))
            {
                getterDefinition = JsExpression.Invocation(JsExpression.Member(getterDefinition, "bind"), JsExpression.This);
                setterDefinition = JsExpression.Invocation(JsExpression.Member(setterDefinition, "bind"), JsExpression.This);
            }*/

            var obj = new JObject
            {
                ["ntype"] = (int)ExpressionType.MemberAccess,
                ["t"] = new JRaw(scriptType)
            };

            var expression = new JObject
            {
                ["ntype"] = (int)ExpressionType.Constant,
                ["t"] = new JRaw(scriptType),
                ["value"] = new JObject()
            };
            obj["expression"] = expression;

            var member = new JObject
            {
                ["td"] = new JRaw("System.Object"),
                ["n"] = name,
                ["t"] = (int)MemberTypes.Property,
                ["rt"] = new JRaw(scriptType)
            };

            var getter = new JObject
            {
                ["td"] = new JRaw("System.Object"),
                ["n"] = "get" + name,
                ["t"] = (int)MemberTypes.Method,
                ["rt"] = new JRaw(scriptType),
                ["p"] = new JRaw("[]"),
                ["def"] = new JRaw(getterDefinition)
            };
            member["g"] = getter;

            var setter = new JObject
            {
                ["td"] = new JRaw("System.Object"),
                ["n"] = "set" + name,
                ["t"] = (int)MemberTypes.Method,
                ["rt"] = new JRaw("System.Object"),
                ["p"] = new JRaw("[" + scriptType + "]"),
                ["def"] = new JRaw(setterDefinition)
            };
            member["s"] = setter;

            obj["member"] = member;

            return obj.ToString(Formatting.None);
        }

        public override string VisitLocalResolveResult(LocalResolveResult rr, object data)
        {
            if (this._allParameters.TryGetValue(rr.Variable, out var name))
            {
                return name;
            }
            var id = rr.Variable.Name;

            if (this._emitter.LocalsNamesMap != null && this._emitter.LocalsNamesMap.ContainsKey(id))
            {
                id = this._emitter.LocalsNamesMap[id];
            }
            else if (this._emitter.LocalsMap != null && this._emitter.LocalsMap.ContainsKey(rr.Variable))
            {
                id = this._emitter.LocalsMap[rr.Variable];
            }

            return this.GetExpressionForLocal(rr.Variable.Name, id, rr.Variable.Type);
        }

        public override string VisitOperatorResolveResult(OperatorResolveResult rr, object data)
        {
            bool isUserDefined = rr.UserDefinedOperatorMethod != null && !rr.UserDefinedOperatorMethod.DeclaringTypeDefinition.IsExternal();
            var arguments = new string[rr.Operands.Count + 1];
            for (int i = 0; i < rr.Operands.Count; i++)
                arguments[i] = this.VisitResolveResult(rr.Operands[i], null);
            arguments[arguments.Length - 1] = isUserDefined ? this.GetMember(rr.UserDefinedOperatorMethod) : GetTypeName(rr.Type, this._emitter);
            if (rr.OperatorType == ExpressionType.Conditional)
                return this.CompileFactoryCall("Condition", new[] { typeof(Expression), typeof(Expression), typeof(Expression), typeof(Type) }, arguments);
            else
            {
                return this.CompileFactoryCall(rr.OperatorType.ToString(), rr.Operands.Count == 1 ? new[] { typeof(Expression), isUserDefined ? typeof(MethodInfo) : typeof(Type) } : new[] { typeof(Expression), typeof(Expression), isUserDefined ? typeof(MethodInfo) : typeof(Type) }, arguments);
            }
        }

        public override string VisitConversionResolveResult(ConversionResolveResult rr, object data)
        {
            var input = this.VisitResolveResult(rr.Input, null);
            if (rr.Conversion.IsIdentityConversion)
            {
                return input;
            }
            else if (rr.Conversion.IsAnonymousFunctionConversion)
            {
                var result = input;
                if (rr.Type.Name == "Expression")
                    result = this.CompileFactoryCall("Quote", new[] { typeof(Expression) }, new[] { result });
                return result;
            }
            else if (rr.Conversion.IsNullLiteralConversion)
            {
                return this.CompileFactoryCall("Constant", new[] { typeof(object), typeof(Type) }, new[] { input, GetTypeName(rr.Type, this._emitter) });
            }
            else if (rr.Conversion.IsMethodGroupConversion)
            {
                var methodInfo = this._compilation.FindType(typeof(MethodInfo));
                return this.CompileFactoryCall("Convert", new[] { typeof(Expression), typeof(Type) }, new[] {
                    this.CompileFactoryCall("Call", new[] { typeof(Expression), typeof(MethodInfo), typeof(Expression[]) }, new[] {
                        this.CompileFactoryCall("Constant", new[] { typeof(object), typeof(Type) }, new[] { this.GetMember(rr.Conversion.Method), GetTypeName(methodInfo, this._emitter) }),
                               this.GetMember(methodInfo.GetMethods().Single(m => m.Name == "CreateDelegate" && m.Parameters.Count == 2 && m.Parameters[0].Type.FullName == typeof(Type).FullName && m.Parameters[1].Type.FullName == typeof(object).FullName)),
                               new [] {
                                   new JRaw(GetTypeName(rr.Type, this._emitter)),
                                   new JRaw(rr.Conversion.Method.IsStatic ? "null" : this.VisitResolveResult(((MethodGroupResolveResult)rr.Input).TargetResult, null))
                               }.ToJavaScript()
                           }),
                           GetTypeName(rr.Type, this._emitter)
                       });
            }
            else
            {
                string methodName;
                if (rr.Conversion.IsTryCast)
                    methodName = "TypeAs";
                else if (rr.CheckForOverflow)
                    methodName = "ConvertChecked";
                else
                    methodName = "Convert";
                if (rr.Conversion.IsUserDefined)
                    return this.CompileFactoryCall(methodName, new[] { typeof(Expression), typeof(Type), typeof(MethodInfo) }, new[] { input, GetTypeName(rr.Type, this._emitter), this.GetMember(rr.Conversion.Method) });
                else
                    return this.CompileFactoryCall(methodName, new[] { typeof(Expression), typeof(Type) }, new[] { input, GetTypeName(rr.Type, this._emitter) });
            }
        }

        public override string VisitTypeIsResolveResult(TypeIsResolveResult rr, object data)
        {
            return this.CompileFactoryCall("TypeIs", new[] { typeof(Expression), typeof(Type) }, new[] { this.VisitResolveResult(rr.Input, null), GetTypeName(rr.TargetType, this._emitter) });
        }

        public override string VisitMemberResolveResult(MemberResolveResult rr, object data)
        {
            var instance = rr.Member.IsStatic ? "null" : this.VisitResolveResult(rr.TargetResult, null);
            if (rr.TargetResult.Type.Kind == TypeKind.Array && rr.Member.Name == "Length")
                return this.CompileFactoryCall("ArrayLength", new[] { typeof(Expression) }, new[] { instance });

            if (rr.Member is IProperty)
                return this.CompileFactoryCall("Property", new[] { typeof(Expression), typeof(PropertyInfo) }, new[] { instance, this.GetMember(rr.Member) });
            if (rr.Member is IField)
                return this.CompileFactoryCall("Field", new[] { typeof(Expression), typeof(FieldInfo) }, new[] { instance, this.GetMember(rr.Member) });
            else
                throw new ArgumentException("Unsupported member " + rr + " in expression tree");
        }

        private List<IMember> GetMemberPath(ResolveResult rr)
        {
            var result = new List<IMember>();
            for (var mrr = rr as MemberResolveResult; mrr != null; mrr = mrr.TargetResult as MemberResolveResult)
            {
                result.Insert(0, mrr.Member);
            }
            return result;
        }

        private List<Tuple<List<IMember>, IList<ResolveResult>, IMethod>> BuildAssignmentMap(IEnumerable<ResolveResult> initializers)
        {
            var result = new List<Tuple<List<IMember>, IList<ResolveResult>, IMethod>>();
            foreach (var init in initializers)
            {
                if (init is OperatorResolveResult)
                {
                    var orr = init as OperatorResolveResult;
                    if (orr.OperatorType != ExpressionType.Assign)
                        throw new InvalidOperationException("Invalid initializer " + init);
                    result.Add(Tuple.Create(this.GetMemberPath(orr.Operands[0]), (IList<ResolveResult>)new[] { orr.Operands[1] }, (IMethod)null));
                }
                else if (init is InvocationResolveResult)
                {
                    var irr = init as InvocationResolveResult;
                    if (irr.Member.Name != "Add")
                        throw new InvalidOperationException("Invalid initializer " + init);
                    result.Add(Tuple.Create(this.GetMemberPath(irr.TargetResult), irr.GetArgumentsForCall(), (IMethod)irr.Member));
                }
                else
                    throw new InvalidOperationException("Invalid initializer " + init);
            }
            return result;
        }

        private bool FirstNEqual<T>(IList<T> first, IList<T> second, int count)
        {
            if (first.Count < count || second.Count < count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!Equals(first[i], second[i]))
                    return false;
            }
            return true;
        }

        private Tuple<List<JRaw>, bool> GenerateMemberBindings(IEnumerator<Tuple<List<IMember>, IList<ResolveResult>, IMethod>> initializers, int index)
        {
            var firstPath = initializers.Current.Item1;
            var result = new List<JRaw>();
            bool hasMore = true;
            do
            {
                var currentTarget = initializers.Current.Item1[index];
                if (initializers.Current.Item1.Count > index + 1)
                {
                    var innerBindings = this.GenerateMemberBindings(initializers, index + 1);
                    result.Add(new JRaw(this.CompileFactoryCall("MemberBind", new[] { typeof(MemberInfo), typeof(MemberBinding[]) }, new[] { this.GetMember(currentTarget), innerBindings.Item1.ToJavaScript() })));

                    if (!innerBindings.Item2)
                    {
                        hasMore = false;
                        break;
                    }
                }
                else if (initializers.Current.Item3 != null)
                {
                    var currentPath = initializers.Current.Item1;
                    var elements = new List<JRaw>();
                    do
                    {
                        elements.Add(new JRaw(this.CompileFactoryCall("ElementInit", new[] { typeof(MethodInfo), typeof(Expression[]) }, new[] { this.GetMember(initializers.Current.Item3), initializers.Current.Item2.Select(i => new JRaw(this.VisitResolveResult(i, null))).ToJavaScript() })));
                        if (!initializers.MoveNext())
                        {
                            hasMore = false;
                            break;
                        }
                    } while (this.FirstNEqual(currentPath, initializers.Current.Item1, index + 1));

                    result.Add(new JRaw(this.CompileFactoryCall("ListBind", new[] { typeof(MemberInfo), typeof(ElementInit[]) }, new[] { this.GetMember(currentTarget), elements.ToJavaScript() })));

                    if (!hasMore)
                        break;
                }
                else
                {
                    result.Add(new JRaw(this.CompileFactoryCall("Bind", new[] { typeof(MemberInfo), typeof(Expression) }, new[] { this.GetMember(currentTarget), this.VisitResolveResult(initializers.Current.Item2[0], null) })));

                    if (!initializers.MoveNext())
                    {
                        hasMore = false;
                        break;
                    }
                }
            } while (this.FirstNEqual(firstPath, initializers.Current.Item1, index));

            return Tuple.Create(result, hasMore);
        }

        public override string VisitCSharpInvocationResolveResult(CSharpInvocationResolveResult rr, object data)
        {
            return this.VisitInvocationResolveResult(rr, data);
        }

        public override string VisitInvocationResolveResult(InvocationResolveResult rr, object data)
        {
            if (rr.Member.DeclaringType is AnonymousType type)
            {
                if (!this._emitter.AnonymousTypes.ContainsKey(type))
                {
                    var config = new AnonymousTypeCreateBlock(this._emitter, null).CreateAnonymousType(type);
                    this._emitter.AnonymousTypes.Add(type, config);
                }
            }

            if (rr.Member.DeclaringType.Kind == TypeKind.Delegate && rr.Member.Name == "Invoke")
            {
                return this.CompileFactoryCall("Invoke", new[] { typeof(Type), typeof(Expression), typeof(Expression[]) }, new[] { GetTypeName(rr.Type, this._emitter), this.VisitResolveResult(rr.TargetResult, null), rr.GetArgumentsForCall().Select(a => new JRaw(this.VisitResolveResult(a, null))).ToJavaScript() });
            }
            else if (rr.Member is IMethod && ((IMethod)rr.Member).IsConstructor)
            {
                if (rr.Member.DeclaringType.Kind == TypeKind.Anonymous)
                {
                    var args = new List<JRaw>();
                    var members = new List<JRaw>();
                    foreach (var init in rr.InitializerStatements)
                    {
                        if (!(init is OperatorResolveResult assign) || assign.OperatorType != ExpressionType.Assign || !((assign.Operands[0] as MemberResolveResult)?.Member is IProperty))
                            throw new Exception("Invalid anonymous type initializer " + init);
                        args.Add(new JRaw(this.VisitResolveResult(assign.Operands[1], null)));
                        members.Add(new JRaw(this.GetMember(((MemberResolveResult)assign.Operands[0]).Member)));
                    }
                    return this.CompileFactoryCall("New", new[] { typeof(ConstructorInfo), typeof(Expression[]), typeof(MemberInfo[]) }, new[] { this.GetMember(rr.Member), args.ToJavaScript(), members.ToJavaScript() });
                }
                else
                {
                    var result = this.CompileFactoryCall("New", new[] { typeof(ConstructorInfo), typeof(Expression[]) }, new[] { this.GetMember(rr.Member), rr.GetArgumentsForCall().Select(a => new JRaw(this.VisitResolveResult(a, null))).ToJavaScript() });
                    if (rr.InitializerStatements.Count > 0)
                    {
                        if (rr.InitializerStatements[0] is InvocationResolveResult invoc && invoc.TargetResult is InitializedObjectResolveResult)
                        {
                            var elements = new List<JRaw>();
                            foreach (var stmt in rr.InitializerStatements)
                            {
                                if (!(stmt is InvocationResolveResult irr))
                                {
                                    throw new Exception("Expected list initializer, was " + stmt);
                                }
                                elements.Add(new JRaw(this.CompileFactoryCall("ElementInit", new[] { typeof(MethodInfo), typeof(Expression[]) }, new[] { this.GetMember(irr.Member), irr.Arguments.Select(i => new JRaw(this.VisitResolveResult(i, null))).ToJavaScript() })));
                            }
                            result = this.CompileFactoryCall("ListInit", new[] { typeof(NewExpression), typeof(ElementInit[]) }, new[] { result, elements.ToJavaScript() });
                        }
                        else
                        {
                            var map = this.BuildAssignmentMap(rr.InitializerStatements);
                            using (IEnumerator<Tuple<List<IMember>, IList<ResolveResult>, IMethod>> enm = map.GetEnumerator())
                            {
                                enm.MoveNext();
                                var bindings = this.GenerateMemberBindings(enm, 0);
                                result = this.CompileFactoryCall("MemberInit", new[] { typeof(NewExpression), typeof(MemberBinding[]) }, new[] { result, bindings.Item1.ToJavaScript() });
                            }
                        }
                    }
                    return result;
                }
            }
            else
            {
                var member = rr.Member is IProperty ? ((IProperty)rr.Member).Getter : rr.Member;    // If invoking a property (indexer), use the get method.
                return this.CompileFactoryCall("Call", new[] { typeof(Expression), typeof(MethodInfo), typeof(Expression[]) }, new[] { member.IsStatic ? "null" : this.VisitResolveResult(rr.TargetResult, null), this.GetMember(member), rr.GetArgumentsForCall().Select(a => new JRaw(this.VisitResolveResult(a, null))).ToJavaScript() });
            }
        }

        public override string VisitTypeOfResolveResult(TypeOfResolveResult rr, object data)
        {
            return this.CompileFactoryCall("Constant", new[] { typeof(object), typeof(Type) }, new[] { GetTypeName(rr.ReferencedType, this._emitter), GetTypeName(rr.Type, this._emitter) });
        }

        public override string VisitDefaultResolveResult(ResolveResult rr, object data)
        {
            if (rr.Type.Kind == TypeKind.Null)
            {
                return "null";
            }

            throw new InvalidOperationException("Resolve result " + rr + " is not handled.");
        }

        private string MakeConstant(ResolveResult rr)
        {
            var value = rr.ConstantValue == null ? DefaultValueBlock.DefaultValue(rr, this._emitter) : AbstractEmitterBlock.ToJavaScript(rr.ConstantValue, this._emitter);
            return this.CompileFactoryCall("Constant", new[] { typeof(object), typeof(Type) }, new[] { value, GetTypeName(rr.Type, this._emitter) });
        }

        public override string VisitConstantResolveResult(ConstantResolveResult rr, object data)
        {
            return this.MakeConstant(rr);
        }

        public override string VisitSizeOfResolveResult(SizeOfResolveResult rr, object data)
        {
            if (rr.ConstantValue == null)
            {
                throw new Exception("Cannot take the size of type " + rr.ReferencedType.FullName);
            }
            return this.MakeConstant(rr);
        }

        public override string VisitArrayAccessResolveResult(ArrayAccessResolveResult rr, object data)
        {
            var array = this.VisitResolveResult(rr.Array, null);
            if (rr.Indexes.Count == 1)
                return this.CompileFactoryCall("ArrayIndex", new[] { typeof(Type), typeof(Expression), typeof(Expression) }, new[] { GetTypeName(rr.Type, this._emitter), array, this.VisitResolveResult(rr.Indexes[0], null) });
            else
                return this.CompileFactoryCall("ArrayIndex", new[] { typeof(Type), typeof(Expression), typeof(Expression[]) }, new[] { GetTypeName(rr.Type, this._emitter), array, rr.Indexes.Select(i => new JRaw(this.VisitResolveResult(i, null))).ToArray().ToJavaScript() });
        }

        public override string VisitArrayCreateResolveResult(ArrayCreateResolveResult rr, object data)
        {
            var arrayType = rr.Type as ArrayType;
            if (rr.InitializerElements != null)
            {
                return this.CompileFactoryCall("NewArrayInit", new[] { typeof(Type), typeof(Expression[]) }, new[] { GetTypeName(arrayType != null ? arrayType.ElementType : rr.Type, this._emitter), rr.InitializerElements.Select(e => new JRaw(this.VisitResolveResult(e, null))).ToArray().ToJavaScript() });
            }

            return this.CompileFactoryCall("NewArrayBounds", new[] { typeof(Type), typeof(Expression[]) }, new[] { GetTypeName(arrayType != null ? arrayType.ElementType : rr.Type, this._emitter), rr.SizeArguments.Select(a => new JRaw(this.VisitResolveResult(a, null))).ToArray().ToJavaScript() });
        }

        public override string VisitThisResolveResult(ThisResolveResult rr, object data)
        {
            return this.CompileFactoryCall("Constant", new[] { typeof(object), typeof(Type) }, new[] { AbstractEmitterBlock.GetThisAlias(this._emitter), GetTypeName(rr.Type, this._emitter) });
        }

        private static string GetTypeName(IType type, IEmitter emitter)
        {
            /*var typeParam = type as ITypeParameter;
            if (typeParam != null && typeParam.OwnerType == SymbolKind.Method)
            {
                return "Object";
            }*/

            return emitter.ToJsName(type);
        }

        private int FindIndexInReflectableMembers(IMember member)
        {
            var type = member.DeclaringTypeDefinition;
            bool hasAttr = false;

            if (!this._emitter.ReflectableTypes.Any(t => t.Type.Equals(type)))
            {
                hasAttr = this._emitter.Translator.Types.OutputTypes.Any(t => t.Type.Equals(type));

                if (!hasAttr)
                {
                    return -1;
                }
            }

            if (!member.IsReflectable(hasAttr, this._syntaxTree))
            {
                return -1;
            }

            int i = 0;
            foreach (var m in member.DeclaringTypeDefinition.Members.Where(m => m.IsReflectable(hasAttr, this._syntaxTree))
                                                                    .OrderBy(m => m, MemberOrderer.Instance))
            {
                if (m.Equals(member.MemberDefinition ?? member))
                    return i;
                i++;
            }
            throw new Exception("Member " + member + " not found even though it should be present");
        }

        public string GetMember(IMember member)
        {
            var owner = (member is IMethod method) && method.IsAccessor ? method.AccessorOwner : null;

            int index = this.FindIndexInReflectableMembers(owner ?? member);
            if (index >= 0)
            {
                string result = $"Bridge.getMetadata({GetTypeName(member.DeclaringType, this._emitter)}).m[{index}]";
                if (owner != null)
                {
                    if (owner is IProperty)
                    {
                        if (ReferenceEquals(member, ((IProperty)owner).Getter))
                            result = result + ".g";
                        else if (ReferenceEquals(member, ((IProperty)owner).Setter))
                            result = result + ".s";
                        else
                            throw new ArgumentException("Invalid member " + member);
                    }
                    else if (owner is IEvent)
                    {
                        if (ReferenceEquals(member, ((IEvent)owner).AddAccessor))
                            result = result + ".ad";
                        else if (ReferenceEquals(member, ((IEvent)owner).RemoveAccessor))
                            result = result + ".r";
                        else
                            throw new ArgumentException("Invalid member " + member);
                    }
                    else
                        throw new ArgumentException("Invalid owner " + owner);
                }
                return result;
            }
            else
            {
                return MetadataUtils.ConstructMemberInfo(member, this._emitter, true, false, this._syntaxTree).ToString(Formatting.None);
            }
        }
    }
}