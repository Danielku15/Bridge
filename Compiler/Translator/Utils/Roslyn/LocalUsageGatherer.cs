using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Bridge.Translator
{
    public static class LocalUsageGatherer
    {
        private class Analyzer : CSharpSyntaxWalker
        {
            private readonly SemanticModel _semanticModel;

            public bool UsesThis { get; private set; }

            public HashSet<ISymbol> UsedVariables { get; } = new HashSet<ISymbol>();

            public List<string> UsedVariablesNames { get; } = new List<string>();

            public Analyzer(SemanticModel semanticModel)
            {
                this._semanticModel = semanticModel;
            }

            public void Analyze(SyntaxNode node)
            {
                this.UsesThis = false;
                this.UsedVariables.Clear();

                if (node is SimpleLambdaExpressionSyntax simpleLambda)
                {
                    this.Visit(simpleLambda.Body);
                }
                else if (node is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
                {
                    this.Visit(parenthesizedLambda.Body);
                }
                else if (node is AnonymousMethodExpressionSyntax anonymousMethod)
                {
                    this.Visit(anonymousMethod.Block);
                }
                else
                {
                    this.Visit(node);
                }
            }

            public override void VisitThisExpression(ThisExpressionSyntax syntax)
            {
                this.UsesThis = true;
            }

            public override void VisitBaseExpression(BaseExpressionSyntax syntax)
            {
                this.UsesThis = true;
            }

            public override void VisitIdentifierName(IdentifierNameSyntax syntax)
            {
                var symbol = this._semanticModel.GetSymbolInfo(syntax).Symbol;

                if (symbol is ILocalSymbol || symbol is IParameterSymbol || symbol is IRangeVariableSymbol)
                {
                    this.UsedVariables.Add(symbol);
                }
                else if ((symbol is IFieldSymbol || symbol is IEventSymbol || symbol is IPropertySymbol || symbol is IMethodSymbol) && !symbol.IsStatic)
                {
                    this.UsesThis = true;
                }
            }

            public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
            {
                var name = node.Identifier.Value.ToString();

                if (!this.UsedVariablesNames.Contains(name))
                {
                    this.UsedVariablesNames.Add(name);
                }

                base.VisitVariableDeclarator(node);
            }

            public override void VisitNameEquals(NameEqualsSyntax node)
            {
            }

            public override void VisitNameColon(NameColonSyntax node)
            {
            }

            public override void VisitGenericName(GenericNameSyntax node)
            {
                var symbol = this._semanticModel.GetSymbolInfo(node).Symbol;

                if ((symbol is IFieldSymbol || symbol is IEventSymbol || symbol is IPropertySymbol || symbol is IMethodSymbol) && !symbol.IsStatic)
                {
                    this.UsesThis = true;
                }
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                this.Visit(node.Expression);
            }

            public override void VisitParameter(ParameterSyntax node)
            {
                var name = node.Identifier.Value.ToString();

                if (!this.UsedVariablesNames.Contains(name))
                {
                    this.UsedVariablesNames.Add(name);
                }
                base.VisitParameter(node);
            }
            public override void VisitTypeParameter(TypeParameterSyntax node)
            {
                var name = node.Identifier.Value.ToString();

                if (!this.UsedVariablesNames.Contains(name))
                {
                    this.UsedVariablesNames.Add(name);
                }
                base.VisitTypeParameter(node);
            }
        }

        public static LocalUsageData GatherInfo(SemanticModel semanticModel, SyntaxNode node)
        {
            var analyzer = new Analyzer(semanticModel);
            analyzer.Analyze(node);
            return new LocalUsageData(analyzer.UsesThis, analyzer.UsedVariables, analyzer.UsedVariablesNames);
        }
    }

    public class IdentifierReplacer : CSharpSyntaxRewriter
    {
        private readonly string _name;
        private readonly ExpressionSyntax _replacer;

        public IdentifierReplacer(string name, ExpressionSyntax replacer)
        {
            this._name = name;
            this._replacer = replacer;
        }

        public ExpressionSyntax Replace(ExpressionSyntax expr)
        {
            return (ExpressionSyntax) this.Visit(expr);
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax syntax)
        {
            if (syntax.Identifier.Value.ToString() == this._name)
            {
                return this._replacer;
            }

            return syntax;
        }
    }

    public class LocalUsageData
    {
        public bool DirectlyOrIndirectlyUsesThis
        {
            get;
            private set;
        }

        public ISet<ISymbol> DirectlyOrIndirectlyUsedLocals
        {
            get;
            private set;
        }

        public IList<string> Names
        {
            get;
            private set;
        }

        public LocalUsageData(bool directlyOrIndirectlyUsesThis, ISet<ISymbol> directlyOrIndirectlyUsedVariables, IList<string> names)
        {
            this.DirectlyOrIndirectlyUsesThis = directlyOrIndirectlyUsesThis;
            this.DirectlyOrIndirectlyUsedLocals = new HashSet<ISymbol>(directlyOrIndirectlyUsedVariables);
            this.Names = new List<string>(names);
        }
    }
}