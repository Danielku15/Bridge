using Bridge.Contract;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator
{
    public partial class Emitter : IEmitter
    {
        public Emitter(ITranslator translator)
        {
            this.InitialLevel = 1;
            this.Translator = translator;
            this.AssignmentType = AssignmentOperatorType.Any;
            this.UnaryOperatorType = UnaryOperatorType.Any;
            this.JsDoc = new JsDoc();
            this.AnonymousTypes = new Dictionary<AnonymousType, IAnonymousTypeConfig>();
            this.AutoStartupMethods = new List<string>();
            this.Cache = new EmitterCache();
            this.AssemblyNameRuleCache = new Dictionary<IAssembly, NameRule[]>();
            this.ClassNameRuleCache = new Dictionary<ITypeDefinition, NameRule[]>();
            this.AssemblyCompilerRuleCache = new Dictionary<IAssembly, CompilerRule[]>();
            this.ClassCompilerRuleCache = new Dictionary<ITypeDefinition, CompilerRule[]>();
        }

        public virtual List<TranslatorOutputItem> Emit()
        {
            this.Log.Info("Emitting...");

            var blocks = this.GetBlocks();
            foreach (var block in blocks)
            {
                this.JsDoc.Init();

                this.Log.Trace("Emitting block " + block.GetType());

                block.Emit();
            }

            if (this.AutoStartupMethods.Count > 1)
            {
                var autoMethods = string.Join(", ", this.AutoStartupMethods);

                throw (TranslatorException) TranslatorException.Create(
                    "Program has more than one entry point defined - {0}", autoMethods);
            }

            var output = this.TransformOutputs();

            this.Log.Info("Emitting done");

            return output;
        }

        private IEnumerable<IAbstractEmitterBlock> GetBlocks()
        {
            yield return new EmitBlock(this);

            if (this.AssemblyInfo.GenerateTypeScript)
            {
                yield return new TypeScript.EmitBlock(this);
            }
        }
    }
}