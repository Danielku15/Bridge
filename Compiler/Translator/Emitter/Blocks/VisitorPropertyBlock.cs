using System.Collections.Generic;
using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public class VisitorPropertyBlock : AbstractMethodBlock
    {
        public VisitorPropertyBlock(IEmitter emitter, PropertyDeclaration propertyDeclaration)
            : base(emitter, propertyDeclaration)
        {
            this.Emitter = emitter;
            this.PropertyDeclaration = propertyDeclaration;
        }

        public PropertyDeclaration PropertyDeclaration
        {
            get;
            set;
        }
        public CompilerRule OldRules { get; private set; }

        protected override void BeginEmit()
        {
            base.BeginEmit();
            this.OldRules = this.Emitter.Rules;

            var rr = this.Emitter.Resolver.ResolveNode(this.PropertyDeclaration) as MemberResolveResult;

            if (rr != null)
            {
                this.Emitter.Rules = Rules.Get(this.Emitter, rr.Member);
            }
        }

        protected override void EndEmit()
        {
            base.EndEmit();
            this.Emitter.Rules = this.OldRules;
        }

        protected override void DoEmit()
        {
            var memberResult = this.Emitter.Resolver.ResolveNode(this.PropertyDeclaration) as MemberResolveResult;
            if (memberResult != null && memberResult.Member.IsExternal())
            {
                return;
            }

            this.EmitPropertyMethod(this.PropertyDeclaration, this.PropertyDeclaration.Getter, ((IProperty)memberResult.Member).Getter, false, false);
            this.EmitPropertyMethod(this.PropertyDeclaration, this.PropertyDeclaration.Setter, ((IProperty)memberResult.Member).Setter, true, false);
        }

        public virtual void EmitPropertyMethod(PropertyDeclaration propertyDeclaration, Accessor accessor, IMethod method, bool setter, bool isObjectLiteral)
        {
            if ((!accessor.IsNull || method != null && method.IsScript()) && this.Emitter.GetInline(accessor) == null)
            {
                this.EnsureComma();

                this.ResetLocals();

                var prevMap = this.BuildLocalsMap();
                var prevNamesMap = this.BuildLocalsNamesMap();

                if (setter)
                {
                    this.AddLocals(new ParameterDeclaration[] { new ParameterDeclaration { Name = "value" } }, accessor.Body);
                }
                else
                {
                    this.AddLocals(new ParameterDeclaration[0], accessor.Body);
                }

                //XmlToJsDoc.EmitComment(this, this.PropertyDeclaration);

                this.Write(setter ? JS.Funcs.Property.SET : JS.Funcs.Property.GET);

                this.WriteColon();
                this.WriteFunction();

                var m_rr = (MemberResolveResult)this.Emitter.Resolver.ResolveNode(propertyDeclaration);
                var nm = Helpers.GetFunctionName(this.Emitter.AssemblyInfo.NamedFunctions, m_rr.Member, this.Emitter, setter);
                if (nm != null)
                {
                    this.Write(nm);
                }

                this.WriteOpenParentheses();
                this.Write(setter ? "value" : "");
                this.WriteCloseParentheses();
                this.WriteSpace();

                var accessorMethod = setter ? ((IProperty)m_rr.Member).Setter : ((IProperty)m_rr.Member).Getter;
                IEnumerable<string> script = null;
                if (accessorMethod != null)
                {
                    script = accessorMethod.GetScript();
                }

                if (script == null)
                {
                    if(YieldBlock.HasYield(accessor.Body))
                    {
                        new GeneratorBlock(this.Emitter, accessor).Emit();
                    }
                    else
                    {
                        accessor.Body.AcceptVisitor(this.Emitter);
                    }
                }
                else
                {
                    this.BeginBlock();

                    this.WriteLines(script);

                    this.EndBlock();
                }

                this.ClearLocalsMap(prevMap);
                this.ClearLocalsNamesMap(prevNamesMap);
                this.Emitter.Comma = true;
            }
        }
    }
}