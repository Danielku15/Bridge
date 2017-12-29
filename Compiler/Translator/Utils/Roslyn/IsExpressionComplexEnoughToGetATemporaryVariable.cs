using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bridge.Translator
{
    public static class IsExpressionComplexEnoughToGetATemporaryVariable
    {
        private class Analyzer : CSharpSyntaxWalker
        {
            private readonly SemanticModel _semanticModel;

            public bool IsComplex
            {
                get;
                private set;
            }

            public Analyzer(SemanticModel semanticModel)
            {
                this._semanticModel = semanticModel;
            }

            public void Analyze(SyntaxNode node)
            {
                this.Visit(node);
            }

            public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitArrayCreationExpression(node);
            }

            public override void VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitImplicitArrayCreationExpression(node);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitBinaryExpression(node);
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitInvocationExpression(node);
            }

            public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitObjectCreationExpression(node);
            }

            public override void VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitAnonymousObjectCreationExpression(node);
            }

            public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitPostfixUnaryExpression(node);
            }

            public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitPrefixUnaryExpression(node);
            }

            public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitConditionalExpression(node);
            }

            public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitConditionalAccessExpression(node);
            }

            public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitAnonymousMethodExpression(node);
            }

            public override void VisitIdentifierName(IdentifierNameSyntax node)
            {
                var symbol = this._semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol is IPropertySymbol)
                {
                    this.IsComplex = true;
                }

                base.VisitIdentifierName(node);
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                var symbol = this._semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol is IPropertySymbol)
                {
                    this.IsComplex = true;
                }

                base.VisitMemberAccessExpression(node);
            }

            public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitElementAccessExpression(node);
            }

            public override void VisitCastExpression(CastExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitCastExpression(node);
            }

            public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                this.IsComplex = true;
                base.VisitAssignmentExpression(node);
            }
        }

        public static bool IsComplex(SemanticModel semanticModel, SyntaxNode node)
        {
            var analyzer = new Analyzer(semanticModel);
            analyzer.Analyze(node);
            return analyzer.IsComplex;
        }
    }
}