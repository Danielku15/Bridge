using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Bridge.Translator
{
    public static class AwaitersCollector
    {
        private class Analyzer : CSharpSyntaxWalker
        {
            public HashSet<AwaitExpressionSyntax> Awaiters { get; }
            
            public Analyzer()
            {
                this.Awaiters = new HashSet<AwaitExpressionSyntax>();
            }

            public void Analyze(SyntaxNode node)
            {
                this.Awaiters.Clear();
                this.Visit(node);
            }

            public override void VisitAwaitExpression(AwaitExpressionSyntax node)
            {
                this.Awaiters.Add(node);
                base.VisitAwaitExpression(node);
            }
        }

        public static bool HasAwaiters(SemanticModel semanticModel, SyntaxNode node)
        {
            var analyzer = new Analyzer();
            analyzer.Analyze(node);
            return analyzer.Awaiters.Count > 0;
        }
    }
}