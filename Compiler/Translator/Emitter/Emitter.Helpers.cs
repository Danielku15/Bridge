using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Object.Net.Utilities;

namespace Bridge.Translator
{
    public partial class Emitter
    {
        public static object ConvertConstant(object value, Expression expression, IEmitter emitter)
        {
            try
            {
                if (expression.Parent != null)
                {
                    var rr = emitter.Resolver.ResolveNode(expression);
                    var conversion = emitter.Resolver.Resolver.GetConversion(expression);
                    var expectedType = emitter.Resolver.Resolver.GetExpectedType(expression);

                    if (conversion.IsNumericConversion && expectedType.IsKnownType(KnownTypeCode.Double) && rr.Type.IsKnownType(KnownTypeCode.Single))
                    {
                        return (double)(float)value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return value;
        }

        public virtual Tuple<bool, bool, string> GetInlineCode(MemberReferenceExpression node)
        {
            var member = LiftNullableMember(node);
            var info = GetInlineCodeFromMember(member, node);
            return WrapNullableMember(info, member, node);
        }

        public virtual Tuple<bool, bool, string> GetInlineCode(InvocationExpression node)
        {
            var target = node.Target as MemberReferenceExpression;
            IMember member = null;
            if (target != null)
            {
                member = LiftNullableMember(target);
            }

            var info = GetInlineCodeFromMember(member, node);
            return WrapNullableMember(info, member, node.Target);
        }

        internal Tuple<bool, bool, string> GetInlineCodeFromMember(IMember member, Expression node)
        {
            if (member == null)
            {
                var resolveResult = this.Resolver.ResolveNode(node);
                var memberResolveResult = resolveResult as MemberResolveResult;

                if (memberResolveResult == null)
                {
                    return new Tuple<bool, bool, string>(false, false, null);
                }

                member = memberResolveResult.Member;
            }

            bool isInlineMethod = IsInlineMethod(member);
            var inlineCode = isInlineMethod ? null : GetInline(member);
            var isStatic = member.IsStatic;

            if (!string.IsNullOrEmpty(inlineCode) && member is IProperty)
            {
                inlineCode = inlineCode.Replace("{value}", "{0}");
            }

            return new Tuple<bool, bool, string>(isStatic, isInlineMethod, inlineCode);
        }

        private Tuple<bool, bool, string> WrapNullableMember(Tuple<bool, bool, string> info, IMember member, Expression node)
        {
            if (member != null && !string.IsNullOrEmpty(info.Item3))
            {
                IMethod method = (IMethod)member;

                StringBuilder savedBuilder = this.Output;
                this.Output = new StringBuilder();
                var mrr = new MemberResolveResult(null, member);
                var argsInfo = new ArgumentsInfo(this, node, mrr);
                argsInfo.ThisArgument = JS.Vars.T;
                new InlineArgumentsBlock(this, argsInfo, info.Item3, method, mrr).EmitNullableReference();
                string tpl = this.Output.ToString();
                this.Output = savedBuilder;

                if (member.Name == CS.Methods.EQUALS)
                {
                    tpl = string.Format(JS.Types.SYSTEM_NULLABLE + "." + JS.Funcs.EQUALS + "({{this}}, {{{0}}}, {1})", method.Parameters.First().Name, tpl);
                }
                else if (member.Name == CS.Methods.TOSTRING)
                {
                    tpl = string.Format(JS.Types.SYSTEM_NULLABLE + "." + JS.Funcs.TOSTIRNG + "({{this}}, {0})", tpl);
                }
                else if (member.Name == CS.Methods.GETHASHCODE)
                {
                    tpl = string.Format(JS.Types.SYSTEM_NULLABLE + "." + JS.Funcs.GETHASHCODE + "({{this}}, {0})", tpl);
                }

                info = new Tuple<bool, bool, string>(info.Item1, info.Item2, tpl);
            }
            return info;
        }

        private IMember LiftNullableMember(MemberReferenceExpression target)
        {
            var targetrr = this.Resolver.ResolveNode(target.Target);
            IMember member = null;
            if (targetrr.Type.IsKnownType(KnownTypeCode.NullableOfT))
            {
                string name = null;
                int count = 0;
                IType typeArg = null;
                if (target.MemberName == CS.Methods.TOSTRING || target.MemberName == CS.Methods.GETHASHCODE)
                {
                    name = target.MemberName;
                }
                else if (target.MemberName == CS.Methods.EQUALS)
                {
                    if (target.Parent is InvocationExpression)
                    {
                        var rr = this.Resolver.ResolveNode(target.Parent) as InvocationResolveResult;
                        if (rr != null)
                        {
                            typeArg = rr.Arguments.First().Type;
                        }
                    }
                    name = target.MemberName;
                    count = 1;
                }

                if (name != null)
                {
                    var type = ((ParameterizedType)targetrr.Type).TypeArguments[0];
                    var methods = type.GetMethods(null, GetMemberOptions.IgnoreInheritedMembers);

                    if (count == 0)
                    {
                        member = methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == count);
                    }
                    else
                    {
                        member = methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == count && m.Parameters.First().Type.Equals(typeArg));

                        if (member == null)
                        {
                            var typeDef = typeArg.GetDefinition();
                            member = methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == count && m.Parameters.First().Type.GetDefinition().IsDerivedFrom(typeDef));
                        }
                    }
                }
            }
            return member;
        }

        public virtual bool IsForbiddenInvocation(InvocationExpression node)
        {
            var resolveResult = this.Resolver.ResolveNode(node);
            var memberResolveResult = resolveResult as MemberResolveResult;

            if (memberResolveResult == null)
            {
                return false;
            }

            var member = memberResolveResult.Member;

            if (member != null)
            {
                var initPosition = member.GetInitPosition();
                if (initPosition.HasValue && initPosition.Value > Contract.InitPosition.After)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual string GetEntityNameFromAttr(IEntity member, bool setter = false)
        {
            var prop = member as IProperty;
            if (prop != null)
            {
                member = setter ? prop.Setter : prop.Getter;
            }
            else
            {
                var e = member as IEvent;
                if (e != null)
                {
                    member = setter ? e.AddAccessor : e.RemoveAccessor;
                }
            }

            if (member == null)
            {
                return null;
            }

            bool isIgnore = member.DeclaringTypeDefinition != null && member.DeclaringTypeDefinition.IsExternal();
            return member.GetNameAttribute(this.GetEntityName(member), !isIgnore);
        }

        Dictionary<IEntity, NameSemantic> entityNameCache = new Dictionary<IEntity, NameSemantic>();
        public virtual NameSemantic GetNameSemantic(IEntity member)
        {
            NameSemantic result;
            if (this.entityNameCache.TryGetValue(member, out result))
            {
                return result;
            }

            result = new NameSemantic { Entity = member, Emitter = this };

            this.entityNameCache.Add(member, result);
            return result;
        }

        public string GetEntityName(IEntity member)
        {
            var semantic = NameSemantic.Create(member, this);
            semantic.IsObjectLiteral = false;
            return semantic.Name;
        }

        public string GetTypeName(ITypeDefinition typeDefinition)
        {
            var semantic = NameSemantic.Create(typeDefinition, this);
            return semantic.Name;
        }

        private Stack<ITypeDefinition> _stack = new Stack<ITypeDefinition>();
        public virtual string GetCustomTypeName(ITypeDefinition type, bool excludeNs)
        {
            if (this._stack.Contains(type))
            {
                return null;
            }

            var nsAtrr = excludeNs ? null : type.GetNamespace();
            bool hasNs = nsAtrr != null;
            var nameAttr = type.GetName();

            string name = null;
            bool changeCase = false;
            if (nameAttr != null)
            {
                if (nameAttr.Item1 != null)
                {
                    name = Helpers.ConvertNameTokens(nameAttr.Item1, type.Name);
                }
                else
                {
                    var boolValue = (bool)nameAttr.Item2;

                    if (boolValue)
                    {
                        if (hasNs)
                        {
                            changeCase = true;
                        }
                        else
                        {
                            this._stack.Push(type);
                            name = this.ToJsName(type);
                            var i = name.LastIndexOf(".");

                            if (i > -1)
                            {
                                char[] chars = name.ToCharArray();
                                chars[i + 1] = Char.ToLowerInvariant(chars[i + 1]);
                                name = new string(chars);
                            }
                            else
                            {
                                name = name.ToLowerCamelCase();
                            }
                            this._stack.Pop();

                            return name;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (hasNs)
            {
                name = "";
                if (nsAtrr.Item1 != null)
                {
                    name = nsAtrr.Item1;
                }

                if (nsAtrr.Item2)
                {
                    return null;
                }

                if (type.DeclaringType != null)
                {
                    name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + this.GetParentNames(type);
                }


                var typeName = this.GetTypeName(type);
                name = (string.IsNullOrEmpty(name) ? "" : (name + ".")) + (changeCase ? typeName.ToLowerCamelCase() : typeName).ConvertName();

                return name;
            }

            if (type.IsObjectLiteral())
            {
                var mode = type.GetObjectCreateMode();
                if (type.IsExternal() && mode == 0)
                {
                    return JS.Types.System.Object.NAME;
                }
            }

            return null;
        }

        public string GetLiteralEntityName(IEntity member)
        {
            var semantic = NameSemantic.Create(member, this);
            semantic.IsObjectLiteral = true;
            return semantic.Name;
        }

        public virtual string GetEntityName(EntityDeclaration entity)
        {
            var rr = this.Resolver.ResolveNode(entity) as MemberResolveResult;

            if (rr != null)
            {
                return this.GetEntityName(rr.Member);
            }

            return null;
        }

        public virtual string GetParameterName(ParameterDeclaration entity)
        {
            var name = entity.Name;

            if (entity.Parent != null && entity.GetParent<SyntaxTree>() != null)
            {
                var rr = this.Resolver.ResolveNode(entity) as LocalResolveResult;
                if (rr != null)
                {
                    var iparam = rr.Variable as IParameter;

                    if (iparam != null)
                    {
                        name = iparam.GetNameAttribute() ?? name;
                    }
                }
            }

            if (Helpers.IsReservedWord(this, name))
            {
                name = Helpers.ChangeReservedWord(name);
            }

            return name;
        }

        public virtual string GetFieldName(FieldDeclaration field)
        {
            if (!string.IsNullOrEmpty(field.Name))
            {
                return field.Name;
            }

            if (field.Variables.Count > 0)
            {
                return field.Variables.First().Name;
            }

            return null;
        }

        public virtual string GetEventName(EventDeclaration evt)
        {
            if (!string.IsNullOrEmpty(evt.Name))
            {
                return evt.Name;
            }

            if (evt.Variables.Count > 0)
            {
                return evt.Variables.First().Name;
            }

            return null;
        }

        public virtual string GetInline(EntityDeclaration method)
        {
            var mrr = this.Resolver.ResolveNode<MemberResolveResult>(method);

            if (mrr != null)
            {
                return this.GetInline(mrr.Member);
            }

            return null;
        }

        public virtual string GetInline(IEntity entity)
        {
            if (entity.SymbolKind == SymbolKind.Property)
            {
                var prop = (IProperty)entity;
                entity = this.IsAssignment ? prop.Setter : prop.Getter;
            }
            else if (entity.SymbolKind == SymbolKind.Event)
            {
                var ev = (IEvent)entity;
                entity = this.IsAssignment ? (this.AssignmentType == AssignmentOperatorType.Add ? ev.AddAccessor : ev.RemoveAccessor) : ev.InvokeAccessor;
            }

            return entity?.GetTemplate(this);
        }

        protected virtual bool IsInlineMethod(IEntity entity)
        {
            if (entity != null)
            {
                return entity.IsInlineMethod();
            }
            return false;
        }

        protected virtual IEnumerable<string> GetScriptArguments(ICSharpCode.NRefactory.CSharp.Attribute attr)
        {
            if (attr == null)
            {
                return null;
            }

            var result = new List<string>();

            foreach (var arg in attr.Arguments)
            {
                string value = "";
                if (arg is PrimitiveExpression)
                {
                    PrimitiveExpression expr = (PrimitiveExpression)arg;
                    value = (string)expr.Value;
                }
                else
                {
                    var rr = this.Resolver.ResolveNode<ConstantResolveResult>(arg);
                    if (rr?.ConstantValue != null)
                    {
                        value = rr.ConstantValue.ToString();
                    }
                }

                result.Add(value);
            }

            return result;
        }

        public virtual bool IsNativeMember(string fullName)
        {
            return fullName.StartsWith(CS.Bridge.DOTNAME, StringComparison.Ordinal) || fullName.StartsWith("System.", StringComparison.Ordinal);
        }


        public virtual void InitEmitter()
        {
            this.Output = new StringBuilder();
            this.Locals = null;
            this.LocalsStack = null;
            this.IteratorCount = 0;
            this.ThisRefCounter = 0;
            this.Writers = new Stack<IWriter>();
            this.IsAssignment = false;
            this.ResetLevel();
            this.IsNewLine = true;
            this.EnableSemicolon = true;
            this.Comma = false;
            this.CurrentDependencies = new List<IPluginDependency>();
        }

        public virtual bool ContainsOnlyOrEmpty(StringBuilder sb, params char[] c)
        {
            if (sb == null || sb.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < sb.Length; i++)
            {
                if (!c.Contains(sb[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool AddOutputItem(List<TranslatorOutputItem> target, string fileName, TranslatorOutputItemContent content, TranslatorOutputKind outputKind, string location = null, string assembly = null)
        {
            var fileHelper = new FileHelper();

            var outputType = fileHelper.GetOutputType(fileName);

            TranslatorOutputItem output = null;

            bool isMinJs = fileHelper.IsMinJS(fileName);

            var searchName = fileName;

            if (isMinJs)
            {
                searchName = fileHelper.GetNonMinifiedJSFileName(fileName);
            }

            output = target.FirstOrDefault(x => string.Compare(x.Name, searchName, StringComparison.InvariantCultureIgnoreCase) == 0);

            if (output != null)
            {
                bool isAdded;

                if (isMinJs)
                {
                    isAdded = output.MinifiedVersion == null;

                    output.MinifiedVersion = new TranslatorOutputItem
                    {
                        Name = fileName,
                        OutputType = outputType,
                        OutputKind = outputKind | TranslatorOutputKind.Minified,
                        Location = location,
                        Content = content,
                        IsMinified = true,
                        Assembly = assembly
                    };
                }
                else
                {
                    isAdded = output.IsEmpty;
                    output.IsEmpty = false;
                }

                return isAdded;
            }

            output = new TranslatorOutputItem
            {
                Name = searchName,
                OutputType = outputType,
                OutputKind = outputKind,
                Location = location,
                Content = new TranslatorOutputItemContent((string)null),
                Assembly = assembly
            };

            if (isMinJs)
            {
                output.IsEmpty = true;

                output.MinifiedVersion = new TranslatorOutputItem
                {
                    Name = fileName,
                    OutputType = outputType,
                    OutputKind = outputKind | TranslatorOutputKind.Minified,
                    Location = location,
                    Content = content,
                    IsMinified = true,
                    Assembly = assembly
                };
            }
            else
            {
                output.Content = content;
            }

            target.Add(output);

            return true;
        }
    }
}