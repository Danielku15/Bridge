using System.Collections.Generic;
using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;

namespace Bridge.Translator
{
    public class OperatorBlock : AbstractMethodBlock
    {
        public OperatorBlock(IEmitter emitter, OperatorDeclaration operatorDeclaration)
            : base(emitter, operatorDeclaration)
        {
            this.Emitter = emitter;
            this.OperatorDeclaration = operatorDeclaration;
        }

        public OperatorDeclaration OperatorDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitOperatorDeclaration(this.OperatorDeclaration);
        }

        protected void EmitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
        {
            var rr = this.Emitter.Resolver.ResolveNode(operatorDeclaration) as MemberResolveResult;
            if (rr != null && rr.Member != null && rr.Member.IsExternal())
            {
                return;
            }

            XmlToJsDoc.EmitComment(this, operatorDeclaration);
            this.EnsureComma();
            this.ResetLocals();
            var prevMap = this.BuildLocalsMap();
            var prevNamesMap = this.BuildLocalsNamesMap();
            this.AddLocals(operatorDeclaration.Parameters, operatorDeclaration.Body);

            var overloads = OverloadsCollection.Create(this.Emitter, operatorDeclaration);

            if (overloads.HasOverloads)
            {
                string name = overloads.GetOverloadName();
                this.Write(name);
            }
            else
            {
                this.Write(this.Emitter.GetEntityName(operatorDeclaration));
            }

            this.WriteColon();

            this.WriteFunction();

            this.EmitMethodParameters(operatorDeclaration.Parameters, null, operatorDeclaration);

            this.WriteSpace();

            IEnumerable<string> script = null;
            if (rr != null && rr.Member != null && rr.Member.IsExternal())
            {
                script = rr.Member.GetScript();
            }


            if (script == null)
            {
                operatorDeclaration.Body.AcceptVisitor(this.Emitter);
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