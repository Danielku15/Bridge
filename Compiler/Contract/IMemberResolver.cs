using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Contract
{
    public interface IMemberResolver
    {
        ResolveResult ResolveNode(AstNode node);
        T ResolveNode<T>(AstNode node) where T: ResolveResult;

        CSharpAstResolver Resolver
        {
            get;
        }

        ICompilation Compilation
        {
            get;
        }
    }
}