using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator.TypeScript
{
    public class TypeScriptBlock : AbstractEmitterBlock
    {
        public TypeScriptBlock(IEmitter emitter, AstNode operatorDeclaration)
            : base(emitter, operatorDeclaration)
        {
        }

        public override int Level => base.Level - this.Emitter.InitialLevel;

        protected override void DoEmit()
        {
        }
    }
}