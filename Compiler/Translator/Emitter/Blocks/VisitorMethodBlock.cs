using System.Collections.Generic;
using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public class VisitorMethodBlock : AbstractMethodBlock
    {
        private IMember _member;
        public VisitorMethodBlock(IEmitter emitter, MethodDeclaration methodDeclaration)
            : base(emitter, methodDeclaration)
        {
            this.Emitter = emitter;
            this.MethodDeclaration = methodDeclaration;
        }

        public MethodDeclaration MethodDeclaration
        {
            get;
            set;
        }
        public CompilerRule OldRules { get; private set; }

        protected override void BeginEmit()
        {
            base.BeginEmit();
            this.OldRules = this.Emitter.Rules;

            var rr = this.Emitter.Resolver.ResolveNode(this.MethodDeclaration) as MemberResolveResult;

            if (rr != null)
            {
                _member = rr.Member;
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
            this.VisitMethodDeclaration(this.MethodDeclaration);
        }

        protected void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
        {
            var rr = this.Emitter.Resolver.ResolveNode(methodDeclaration) as MemberResolveResult;
            if (rr != null && rr.Member != null)
            {
                if (rr.Member.IsExternal())
                {
                    return;
                }
                var initPosition = rr.Member.GetInitPosition();
                if (initPosition.HasValue && initPosition.Value > InitPosition.After)
                {
                    return;
                }
            }

            this.EnsureComma();
            this.ResetLocals();

            var prevMap = this.BuildLocalsMap();
            var prevNamesMap = this.BuildLocalsNamesMap();

            this.AddLocals(methodDeclaration.Parameters, methodDeclaration.Body);

            var overloads = OverloadsCollection.Create(this.Emitter, methodDeclaration);
            XmlToJsDoc.EmitComment(this, this.MethodDeclaration);
            var isEntryPoint = Helpers.IsEntryPointMethod(this.Emitter, this.MethodDeclaration);

            string name = overloads.GetOverloadName(false, null, OverloadsCollection.ExcludeTypeParameterForDefinition(_member));

            if (isEntryPoint)
            {
                this.Write(JS.Funcs.ENTRY_POINT_NAME);
            }
            else
            {
                this.Write(name);
            }

            this.WriteColon();

            this.WriteFunction();

            if (isEntryPoint)
            {
                this.Write(name);
                this.WriteSpace();
            }
            else
            {
                var nm = Helpers.GetFunctionName(this.Emitter.AssemblyInfo.NamedFunctions, _member, this.Emitter);
                if (nm != null)
                {
                    this.Write(nm);
                    this.WriteSpace();
                }
            }

            this.EmitMethodParameters(methodDeclaration.Parameters, methodDeclaration.TypeParameters.Count > 0 && (_member != null && _member.IsIgnoreGeneric()) ? null : methodDeclaration.TypeParameters, methodDeclaration);

            this.WriteSpace();

            IEnumerable<string> script = null;
            if (rr != null && rr.Member != null)
            {
                script = rr.Member.GetScript();
            }

            if (script == null)
            {
                if (YieldBlock.HasYield(methodDeclaration.Body))
                {
                    new GeneratorBlock(this.Emitter, methodDeclaration).Emit();
                }
                else if (methodDeclaration.HasModifier(Modifiers.Async) || AsyncBlock.HasGoto(methodDeclaration.Body))
                {
                    new AsyncBlock(this.Emitter, methodDeclaration).Emit();
                }
                else
                {
                    methodDeclaration.Body.AcceptVisitor(this.Emitter);
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