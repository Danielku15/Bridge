using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public partial class Inspector
    {
        private void ExecuteCompilerExtension(MethodDeclaration methodDeclaration, IMethod method)
        {
            // validate signature 
            if (method.Parameters.Count != 1 ||
                method.Parameters[0].Type.FullName != "Bridge.CompilerServices.ICompilerContext")
            {
                throw new EmitterException(methodDeclaration, "[CompilerExtension] methods must accept exactly one parameter of type Bridge.CompilerServices.ICompilerContext");
            }
            if (!method.IsStatic)
            {
                throw new EmitterException(methodDeclaration, "[CompilerExtension] methods must be static");
            }
            if (methodDeclaration.Body == null)
            {
                throw new EmitterException(methodDeclaration, "[CompilerExtension] methods must have a body");
            }

            var interpreter = new CompilerExtensionInterpreter(this.Translator.Resolver, methodDeclaration.Body);
            interpreter.Execute();
        }
    }
}