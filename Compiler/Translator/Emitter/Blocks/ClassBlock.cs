using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using Newtonsoft.Json;
using Object.Net.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Bridge.Translator
{
    public class ClassBlock : AbstractEmitterBlock
    {
        public ClassBlock(IEmitter emitter, ITypeInfo typeInfo)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.TypeInfo = typeInfo;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public bool IsGeneric
        {
            get;
            set;
        }

        public int StartPosition
        {
            get;
            set;
        }

        public bool HasEntryPoint
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            XmlToJsDoc.EmitComment(this, this.Emitter.Translator.EmitNode);
            string globalTarget = this.TypeInfo.Type.GetGlobalTarget(this.TypeInfo.TypeDeclaration);

            if (globalTarget != null)
            {
                this.CheckGlobalClass();
                this.Emitter.NamedFunctions = new Dictionary<string, string>();
                this.WriteTopInitMethods();

                this.Write(JS.Types.Bridge.APPLY);
                this.WriteOpenParentheses();
                this.Write(globalTarget);
                this.Write(", ");
                this.BeginBlock();

                new MethodBlock(this.Emitter, this.TypeInfo, true).Emit();

                this.WriteNewLine();
                this.EndBlock();
                this.WriteCloseParentheses();
                this.WriteSemiColon();

                this.EmitAnonymousTypes();
                this.EmitNamedFunctions();

                this.WriteAfterInitMethods();

                this.WriteNewLine();
            }
            else
            {
                this.EmitClassHeader();

                this.Emitter.NamedFunctions = new Dictionary<string, string>();

                if (this.TypeInfo.TypeDeclaration.ClassType != ClassType.Interface)
                {
                    MethodDeclaration entryPoint = null;
                    if (this.TypeInfo.StaticMethods.Any(group =>
                    {
                        return group.Value.Any(method =>
                        {
                            var result = Helpers.IsEntryPointMethod(this.Emitter, method.Declaration);
                            if (result)
                            {
                                entryPoint = method.Declaration;
                            }
                            return result;
                        });
                    }))
                    {
                        if (!entryPoint.Body.IsNull)
                        {
                            this.Emitter.VisitMethodDeclaration(entryPoint);
                        }
                    }

                    this.EmitStaticBlock();
                    this.EmitInstantiableBlock();
                }

                this.EmitClassEnd();
            }
        }

        protected void CheckGlobalClass()
        {
            var type = this.TypeInfo.Type.GetDefinition();
            if (!type.IsStatic)
            {
                throw new EmitterException(this.TypeInfo.TypeDeclaration, string.Format("The type {0} must be static in order to be decorated with a [MixinAttribute] or [GlobalMethodsAttribute]", this.TypeInfo.Type.FullName));
            }

            if (type.TypeParameters.Count > 0)
            {
                throw new EmitterException(this.TypeInfo.TypeDeclaration, string.Format("[MixinAttribute] or [GlobalMethodsAttribute] cannot be applied to the generic type {0}.", this.TypeInfo.Type.FullName));
            }

            if (type.Members.Any(m => !m.IsStatic && m.SymbolKind != SymbolKind.Method))
            {
                throw new EmitterException(this.TypeInfo.TypeDeclaration, string.Format("The type {0} can contain only methods in order to be decorated with a [MixinAttribute] or [GlobalMethodsAttribute]", this.TypeInfo.Type.FullName));
            }
        }

        protected virtual void EmitClassHeader()
        {
            this.WriteTopInitMethods();

            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.GetCustomTypeName(typeDef, false);
            this.IsGeneric = typeDef.TypeParameterCount > 0 && !TypeInfo.Type.IsIgnoreGeneric();

            if (name.IsEmpty())
            {
                name = this.Emitter.ToJsName(this.TypeInfo.Type, asDefinition: true, nomodule: true, ignoreLiteralName: false);
            }

            if (typeDef.Kind == TypeKind.Interface && IsGeneric)
            {
                this.Write(JS.Types.Bridge.DEFINE_I);
            }
            else
            {
                this.Write(JS.Types.Bridge.DEFINE);
            }

            this.WriteOpenParentheses();

            this.WriteScript(name);
            this.StartPosition = this.Emitter.Output.Length;
            this.Write(", ");

            if (this.IsGeneric)
            {
                this.WriteFunction();
                this.WriteOpenParentheses();

                foreach (var p in typeDef.TypeParameters)
                {
                    if (typeDef.TypeParameters.Count(gp => gp.FullName == p.FullName) > 1)
                    {
                        throw new EmitterException(this.TypeInfo.TypeDeclaration, $"Type parameter '{p.FullName}' has the same name as the type parameter from outer type.");
                    }
                    this.EnsureComma(false);
                    this.Write(p.Name);
                    this.Emitter.Comma = true;
                }
                this.Emitter.Comma = false;
                this.WriteCloseParentheses();

                this.Write(" { return ");
            }

            this.BeginBlock();

            var extend = GetTypeHierarchy();
            if (extend.IsNotEmpty() && !this.TypeInfo.IsEnum)
            {
                if (this.TypeInfo.InstanceMethods.Any(m => m.Value.Any(subm => this.Emitter.GetEntityName(subm.Declaration) == JS.Fields.INHERITS)) ||
                    this.TypeInfo.InstanceConfig.Fields.Any(m => m.GetName(this.Emitter) == JS.Fields.INHERITS))
                {
                    this.Write(JS.Vars.D);
                }

                this.Write(JS.Fields.INHERITS);
                this.WriteColon();

                // in case any generic parameter is the own class or a subclass, we need to define the class inheritance as function.
                // otherwise we try to access the type currently being defined. 
                // e.g. 
                //   interface ITest<X> { }
                //   class Test : ITest<Test> { }

                // will need to output
                // Bridge.define("Test", {
                //   inherits: function() { return [Test] },
                // ...
                if (Helpers.IsTypeArgInSubclass(this.TypeInfo.Type, this.TypeInfo.Type, this.Emitter, false))
                {
                    this.WriteFunction();
                    this.WriteOpenCloseParentheses(true);
                    this.WriteOpenBrace(true);
                    this.WriteReturn(true);
                    this.Write(extend);
                    this.WriteSemiColon();
                    this.WriteCloseBrace(true);
                }
                else
                {
                    this.Write(extend);
                }

                this.Emitter.Comma = true;
            }

            this.WriteKind();
            this.WriteObjectLiteral();

            if (this.TypeInfo.Module != null)
            {
                this.WriteScope();
            }

            this.WriteVariance();
        }

        private string GetTypeHierarchy()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            var list = new List<string>();

            foreach (var t in this.TypeInfo.GetBaseTypes(this.Emitter))
            {
                var name = this.Emitter.ToJsName(t);

                list.Add(name);
            }

            if (list.Count > 0 && list[0] == JS.Types.System.Object.NAME)
            {
                list.RemoveAt(0);
            }

            if (list.Count == 0)
            {
                return "";
            }

            bool needComma = false;

            foreach (var item in list)
            {
                if (needComma)
                {
                    sb.Append(",");
                }

                needComma = true;
                sb.Append(item);
            }

            sb.Append("]");

            return sb.ToString();
        }

        private void WriteTopInitMethods()
        {
            var beforeDefineMethods = this.GetBeforeDefineMethods();

            if (beforeDefineMethods.Any())
            {
                foreach (var method in beforeDefineMethods)
                {
                    if (!this.Emitter.IsNewLine)
                    {
                        this.WriteNewLine();
                    }

                    this.Write(method);
                }

                this.WriteNewLine();
                this.WriteNewLine();
            }

            var topDefineMethods = this.GetTopDefineMethods();

            if (topDefineMethods.Any())
            {
                foreach (var method in topDefineMethods)
                {
                    this.Emitter.EmitterOutput.TopOutput.Append(method);
                }
            }
        }

        protected virtual void WriteVariance()
        {
            var itypeDef = this.TypeInfo.Type.GetDefinition();
            if (itypeDef.Kind == TypeKind.Interface && MetadataUtils.IsJsGeneric(itypeDef, this.Emitter) &&
                itypeDef.TypeParameters != null &&
                itypeDef.TypeParameters.Any(typeParameter => typeParameter.Variance != VarianceModifier.Invariant))
            {
                this.EnsureComma();
                this.Write(JS.Fields.VARIANCE);
                this.WriteColon();
                this.WriteScript(
                    itypeDef.TypeParameters.Select(typeParameter => ClassBlock.ConvertVarianceToInt(typeParameter.Variance))
                        .ToArray());
                this.Emitter.Comma = true;
            }
        }

        private static int ConvertVarianceToInt(VarianceModifier variance)
        {
            switch (variance)
            {
                case VarianceModifier.Covariant:
                    return 1;

                case VarianceModifier.Contravariant:
                    return 2;

                default:
                    return 0;
            }
        }

        protected virtual void WriteScope()
        {
            this.EnsureComma();
            this.Write(JS.Vars.SCOPE);
            this.WriteColon();
            this.Write(this.TypeInfo.Module.Name);
            this.Emitter.Comma = true;
        }

        protected virtual void WriteKind()
        {
            if (this.TypeInfo.Type.Kind != TypeKind.Class)
            {
                this.EnsureComma();
                this.Write(JS.Fields.KIND);
                this.WriteColon();
                this.WriteScript(this.TypeInfo.Type.Kind.ToString().ToLowerInvariant());
                this.Emitter.Comma = true;
            }
        }

        protected virtual void WriteObjectLiteral()
        {
            if (this.TypeInfo.IsObjectLiteral)
            {
                this.EnsureComma();
                this.Write(JS.Fields.LITERAL);
                this.WriteColon();
                this.WriteScript(true);
                this.Emitter.Comma = true;
            }
        }

        protected virtual void EmitStaticBlock()
        {
            int pos = this.Emitter.Output.Length;
            bool comma = this.Emitter.Comma;
            bool newLine = this.Emitter.IsNewLine;

            this.Emitter.StaticBlock = true;
            this.EnsureComma();

            if (this.TypeInfo.InstanceMethods.Any(m => m.Value.Any(subm => this.Emitter.GetEntityName(subm.Declaration) == JS.Fields.STATICS)) ||
                this.TypeInfo.InstanceConfig.Fields.Any(m => m.GetName(this.Emitter) == JS.Fields.STATICS))
            {
                this.Write(JS.Vars.D);
            }

            this.Write(JS.Fields.STATICS);
            this.WriteColon();
            this.BeginBlock();
            int checkOutputPos = this.Emitter.Output.Length;

            var ctorBlock = new ConstructorBlock(this.Emitter, this.TypeInfo, true);
            ctorBlock.Emit();
            this.HasEntryPoint = ctorBlock.HasEntryPoint;

            new MethodBlock(this.Emitter, this.TypeInfo, true).Emit();
            var clear = checkOutputPos == this.Emitter.Output.Length;
            this.WriteNewLine();
            this.EndBlock();

            if (clear)
            {
                this.Emitter.Output.Length = pos;
                this.Emitter.Comma = comma;
                this.Emitter.IsNewLine = newLine;
            }
            else
            {
                this.Emitter.Comma = true;
            }

            this.Emitter.StaticBlock = false;
        }

        protected virtual void EmitInstantiableBlock()
        {
            if (this.TypeInfo.IsEnum)
            {
                if (this.TypeInfo.Type.GetDefinition().HasFlagsAttribute())
                {
                    this.EnsureComma();
                    this.Write(JS.Fields.FLAGS + ": true");
                    this.Emitter.Comma = true;
                }

                var etype = this.TypeInfo.Type.GetDefinition().EnumUnderlyingType;
                var enumMode = this.TypeInfo.Type.GetDefinition().EnumEmitMode();
                var isString = enumMode >= 3 && enumMode <= 6;
                if (isString)
                {
                    etype = this.Emitter.Resolver.Compilation.FindType(KnownTypeCode.String);
                }
                if (!etype.IsKnownType(KnownTypeCode.Int32))
                {
                    this.EnsureComma();
                    this.Write(JS.Fields.UNDERLYINGTYPE + ": ");
                    this.Write(this.Emitter.ToJsName(etype));

                    this.Emitter.Comma = true;
                }
            }

            if (this.HasEntryPoint)
            {
                this.EnsureComma();
                this.Write(JS.Fields.ENTRY_POINT + ": true");
                this.Emitter.Comma = true;
            }

            var ctorBlock = new ConstructorBlock(this.Emitter, this.TypeInfo, false);

            if (this.TypeInfo.HasRealInstantiable(this.Emitter) || this.Emitter.Translator.Plugins.HasConstructorInjectors(ctorBlock) || this.TypeInfo.Type.Kind == TypeKind.Struct)
            {
                ctorBlock.Emit();
                new MethodBlock(this.Emitter, this.TypeInfo, false).Emit();
            }
        }

        protected virtual void EmitClassEnd()
        {
            this.WriteNewLine();
            this.EndBlock();

            var classStr = this.Emitter.Output.ToString().Substring(this.StartPosition);

            if (Regex.IsMatch(classStr, "^\\s*,\\s*\\{\\s*\\}\\s*$", RegexOptions.Multiline))
            {
                this.Emitter.Output.Remove(this.StartPosition, this.Emitter.Output.Length - this.StartPosition);
            }

            if (this.IsGeneric)
            {
                this.Write("; }");
            }

            this.WriteCloseParentheses();
            this.WriteSemiColon();

            this.EmitAnonymousTypes();
            this.EmitNamedFunctions();

            this.WriteAfterInitMethods();

            this.WriteNewLine();
        }

        private void WriteAfterInitMethods()
        {
            var afterDefineMethods = this.GetAfterDefineMethods();

            if (afterDefineMethods.Any())
            {
                this.WriteNewLine();
            }

            foreach (var method in afterDefineMethods)
            {
                this.WriteNewLine();
                this.Write(method);
            }

            var bottomDefineMethods = this.GetBottomDefineMethods();

            if (bottomDefineMethods.Any())
            {
                foreach (var method in bottomDefineMethods)
                {
                    this.Emitter.EmitterOutput.BottomOutput.Append(method);
                }
            }
        }

        protected virtual void EmitAnonymousTypes()
        {
            var types = this.Emitter.AnonymousTypes.Values.Where(t => !t.Emitted).ToArray();
            if (types.Any())
            {
                this.Emitter.Comma = false;
                foreach (IAnonymousTypeConfig type in types)
                {
                    this.WriteNewLine();
                    this.WriteNewLine();

                    type.Emitted = true;
                    this.Write(type.Code);
                }
            }
        }

        protected virtual void EmitNamedFunctions()
        {
            if (this.Emitter.NamedFunctions.Count > 0)
            {
                this.Emitter.Comma = false;

                var name = this.Emitter.ToJsName(this.Emitter.TypeInfo.Type, true);

                this.WriteNewLine();
                this.WriteNewLine();
                this.Write(JS.Funcs.BRIDGE_NS);
                this.WriteOpenParentheses();
                this.WriteScript(name);
                this.Write(", " + JS.Vars.D_ + ")");
                this.WriteSemiColon();

                this.WriteNewLine();
                this.WriteNewLine();
                this.Write(JS.Types.Bridge.APPLY + "(" + JS.Vars.D_ + ".");
                this.Write(name);
                this.Write(", ");
                this.BeginBlock();

                foreach (KeyValuePair<string, string> namedFunction in this.Emitter.NamedFunctions)
                {
                    this.EnsureComma();
                    this.Write(namedFunction.Key + ": " + namedFunction.Value);
                    this.Emitter.Comma = true;
                }

                this.WriteNewLine();
                this.EndBlock();
                this.WriteCloseParentheses();
                this.WriteSemiColon();
            }
        }

        protected virtual IEnumerable<string> GetDefineMethods(InitPosition value, Func<MethodDeclaration, IMethod, string> fn)
        {
            var methods = this.TypeInfo.InstanceMethods;

            foreach (var methodGroup in methods)
            {
                foreach (var method in methodGroup.Value)
                {
                    if (method.Symbol.HasInitPosition())
                    {
                        throw new EmitterException(method.Declaration.NameToken, "Instance method cannot be Init method");
                    }
                }
            }

            methods = this.TypeInfo.StaticMethods;
            List<string> list = new List<string>();

            foreach (var methodGroup in methods)
            {
                foreach (var method in methodGroup.Value)
                {
                    var initPosition = method.Symbol.GetInitPosition();
                    if (initPosition.HasValue && initPosition.Value == value)
                    {
                        if (method.Symbol.TypeParameters.Count > 0)
                        {
                            throw new EmitterException(method.Declaration, "Init method cannot be generic");
                        }

                        if (method.Symbol.Parameters.Count > 0)
                        {
                            throw new EmitterException(method.Declaration, "Init method should not have parameters");
                        }

                        if (method.Symbol.ReturnType.Kind != TypeKind.Void
                            && !method.Symbol.ReturnType.IsKnownType(KnownTypeCode.Void))
                        {
                            throw new EmitterException(method.Declaration, "Init method should not return anything");
                        }

                        list.Add(fn(method.Declaration, method.Symbol));
                    }
                }
            }

            return list;
        }

        protected virtual IEnumerable<string> GetBeforeDefineMethods()
        {
            return this.GetDefineMethods(InitPosition.Before,
                (method, rrMethod) =>
                {
                    var level = this.Emitter.Level;

                    this.PushWriter(JS.Types.Bridge.INIT + "(function () {0});");
                    this.ResetLocals();
                    var prevMap = this.BuildLocalsMap();
                    var prevNamesMap = this.BuildLocalsNamesMap();

                    this.Emitter.InitPosition = InitPosition.Before;
                    this.Emitter.ResetLevel();

                    method.Body.AcceptVisitor(this.Emitter);

                    this.Emitter.InitPosition = null;
                    this.Emitter.ResetLevel(level);

                    this.ClearLocalsMap(prevMap);
                    this.ClearLocalsNamesMap(prevNamesMap);

                    return this.PopWriter(true);
                });
        }

        protected virtual IEnumerable<string> GetTopDefineMethods()
        {
            return this.GetDefineMethods(InitPosition.Top,
                (method, rrMethod) =>
                {
                    var prevLevel = this.Emitter.Level;
                    var prevInitialLevel = this.Emitter.InitialLevel;

                    this.PushWriter("{0}");
                    this.ResetLocals();
                    var prevMap = this.BuildLocalsMap();
                    var prevNamesMap = this.BuildLocalsNamesMap();
                    this.Emitter.NoBraceBlock = method.Body;

                    this.Emitter.InitPosition = InitPosition.Top;
                    ((Emitter)this.Emitter).InitialLevel = this.Emitter.InitialLevel > 1 ? this.Emitter.InitialLevel - 1 : 0;
                    this.Emitter.ResetLevel();

                    method.Body.AcceptVisitor(this.Emitter);

                    this.Emitter.InitPosition = null;
                    ((Emitter)this.Emitter).InitialLevel = prevInitialLevel;
                    this.Emitter.ResetLevel(prevLevel);

                    this.ClearLocalsMap(prevMap);
                    this.ClearLocalsNamesMap(prevNamesMap);

                    return this.PopWriter(true);
                });
        }

        protected virtual IEnumerable<string> GetBottomDefineMethods()
        {
            return this.GetDefineMethods(InitPosition.Bottom,
                (method, rrMethod) =>
                {
                    var prevLevel = this.Emitter.Level;
                    var prevInitialLevel = this.Emitter.InitialLevel;

                    this.PushWriter("{0}");
                    this.ResetLocals();
                    var prevMap = this.BuildLocalsMap();
                    var prevNamesMap = this.BuildLocalsNamesMap();
                    this.Emitter.NoBraceBlock = method.Body;

                    this.Emitter.InitPosition = InitPosition.Bottom;
                    ((Emitter)this.Emitter).InitialLevel = this.Emitter.InitialLevel > 1 ? this.Emitter.InitialLevel - 1 : 0;
                    this.Emitter.ResetLevel();

                    method.Body.AcceptVisitor(this.Emitter);

                    this.Emitter.InitPosition = null;
                    ((Emitter)this.Emitter).InitialLevel = prevInitialLevel;
                    this.Emitter.ResetLevel(prevLevel);

                    this.ClearLocalsMap(prevMap);
                    this.ClearLocalsNamesMap(prevNamesMap);

                    return this.PopWriter(true);
                });
        }

        protected virtual IEnumerable<string> GetAfterDefineMethods()
        {
            return this.GetDefineMethods(InitPosition.After,
                (method, rrMethod) =>
                {
                    this.Emitter.InitPosition = InitPosition.After;

                    var callback = JS.Types.Bridge.INIT + "(function () { " + this.Emitter.ToJsName(rrMethod.DeclaringTypeDefinition) + "." +
                           this.Emitter.GetEntityName(method) + "(); });";

                    this.Emitter.InitPosition = null;

                    return callback;
                });
        }
    }
}