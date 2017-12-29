using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;
using Object.Net.Utilities;

namespace Bridge.Translator
{
    public abstract partial class Visitor : IAstVisitor
    {
        public virtual void ThrowIfNeeded(AstNode node, string message = null)
        {
            if (this.ThrowException)
            {
                this.Throw(node, message);
            }
        }
        public virtual void Throw(AstNode node, string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = $"Language construction {node.GetType().Name} is not supported";
            }

            throw new EmitterException(node, message);
        }

        public virtual bool ThrowException { get; set; } = true;

        public virtual void VisitAccessor(Accessor accessor)
        {
            this.ThrowIfNeeded(accessor);
        }

        public virtual void VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
        {
            this.ThrowIfNeeded(anonymousMethodExpression);
        }

        public virtual void VisitAnonymousTypeCreateExpression(AnonymousTypeCreateExpression anonymousTypeCreateExpression)
        {
            this.ThrowIfNeeded(anonymousTypeCreateExpression);
        }

        public virtual void VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
        {
            this.ThrowIfNeeded(arrayCreateExpression);
        }

        public virtual void VisitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
        {
            this.ThrowIfNeeded(arrayInitializerExpression);
        }

        public virtual void VisitArraySpecifier(ArraySpecifier arraySpecifier)
        {
            this.ThrowIfNeeded(arraySpecifier);
        }

        public virtual void VisitAsExpression(AsExpression asExpression)
        {
            this.ThrowIfNeeded(asExpression);
        }

        public virtual void VisitAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            this.ThrowIfNeeded(assignmentExpression);
        }

        public virtual void VisitAttribute(ICSharpCode.NRefactory.CSharp.Attribute attribute)
        {
            this.ThrowIfNeeded(attribute);
        }

        public virtual void VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression)
        {
            this.ThrowIfNeeded(baseReferenceExpression);
        }

        public virtual void VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
        {
            this.ThrowIfNeeded(binaryOperatorExpression);
        }

        public virtual void VisitBlockStatement(BlockStatement blockStatement)
        {
            this.ThrowIfNeeded(blockStatement);
        }

        public virtual void VisitBreakStatement(BreakStatement breakStatement)
        {
            this.ThrowIfNeeded(breakStatement);
        }

        public virtual void VisitCaseLabel(CaseLabel caseLabel)
        {
            this.ThrowIfNeeded(caseLabel);
        }

        public virtual void VisitCastExpression(CastExpression castExpression)
        {
            this.ThrowIfNeeded(castExpression);
        }

        public virtual void VisitCatchClause(CatchClause catchClause)
        {
            this.ThrowIfNeeded(catchClause);
        }

        public virtual void VisitCheckedExpression(CheckedExpression checkedExpression)
        {
            this.ThrowIfNeeded(checkedExpression);
        }

        public virtual void VisitCheckedStatement(CheckedStatement checkedStatement)
        {
            this.ThrowIfNeeded(checkedStatement);
        }

        public virtual void VisitComposedType(ComposedType composedType)
        {
            this.ThrowIfNeeded(composedType);
        }

        public virtual void VisitConditionalExpression(ConditionalExpression conditionalExpression)
        {
            this.ThrowIfNeeded(conditionalExpression);
        }

        public virtual void VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
        {
            this.ThrowIfNeeded(constructorDeclaration);
        }

        public virtual void VisitConstructorInitializer(ConstructorInitializer constructorInitializer)
        {
            this.ThrowIfNeeded(constructorInitializer);
        }

        public virtual void VisitContinueStatement(ContinueStatement continueStatement)
        {
            this.ThrowIfNeeded(continueStatement);
        }

        public virtual void VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
        {
            this.ThrowIfNeeded(customEventDeclaration);
        }

        public virtual void VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
        {
            this.ThrowIfNeeded(delegateDeclaration);
        }

        public virtual void VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
        {
            this.ThrowIfNeeded(destructorDeclaration);
        }

        public virtual void VisitDirectionExpression(DirectionExpression directionExpression)
        {
            this.ThrowIfNeeded(directionExpression);
        }

        public virtual void VisitDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            this.ThrowIfNeeded(doWhileStatement);
        }

        public virtual void VisitDocumentationReference(DocumentationReference documentationReference)
        {
            this.ThrowIfNeeded(documentationReference);
        }

        public virtual void VisitEmptyStatement(EmptyStatement emptyStatement)
        {
            this.ThrowIfNeeded(emptyStatement);
        }

        public virtual void VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
        {
            this.ThrowIfNeeded(enumMemberDeclaration);
        }

        public virtual void VisitEventDeclaration(EventDeclaration eventDeclaration)
        {
            this.ThrowIfNeeded(eventDeclaration);
        }

        public virtual void VisitExpressionStatement(ExpressionStatement expressionStatement)
        {
            this.ThrowIfNeeded(expressionStatement);
        }

        public virtual void VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
        {
            this.ThrowIfNeeded(externAliasDeclaration);
        }

        public virtual void VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
        {
            this.ThrowIfNeeded(fieldDeclaration);
        }

        public virtual void VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
        {
            this.ThrowIfNeeded(fixedFieldDeclaration);
        }

        public virtual void VisitFixedStatement(FixedStatement fixedStatement)
        {
            this.ThrowIfNeeded(fixedStatement);
        }

        public virtual void VisitFixedVariableInitializer(FixedVariableInitializer fixedVariableInitializer)
        {
            this.ThrowIfNeeded(fixedVariableInitializer);
        }

        public virtual void VisitForStatement(ForStatement forStatement)
        {
            this.ThrowIfNeeded(forStatement);
        }

        public virtual void VisitForeachStatement(ForeachStatement foreachStatement)
        {
            this.ThrowIfNeeded(foreachStatement);
        }

        public virtual void VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement)
        {
            this.ThrowIfNeeded(gotoCaseStatement);
        }

        public virtual void VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement)
        {
            this.ThrowIfNeeded(gotoDefaultStatement);
        }

        public virtual void VisitGotoStatement(GotoStatement gotoStatement)
        {
            this.ThrowIfNeeded(gotoStatement);
        }

        public virtual void VisitIdentifierExpression(IdentifierExpression identifierExpression)
        {
            this.ThrowIfNeeded(identifierExpression);
        }

        public virtual void VisitIfElseStatement(IfElseStatement ifElseStatement)
        {
            this.ThrowIfNeeded(ifElseStatement);
        }

        public virtual void VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
        {
            this.ThrowIfNeeded(indexerDeclaration);
        }

        public virtual void VisitIndexerExpression(IndexerExpression indexerExpression)
        {
            this.ThrowIfNeeded(indexerExpression);
        }

        public virtual void VisitInvocationExpression(InvocationExpression invocationExpression)
        {
            this.ThrowIfNeeded(invocationExpression);
        }

        public virtual void VisitIsExpression(IsExpression isExpression)
        {
            this.ThrowIfNeeded(isExpression);
        }

        public virtual void VisitLabelStatement(LabelStatement labelStatement)
        {
            this.ThrowIfNeeded(labelStatement);
        }

        public virtual void VisitLambdaExpression(LambdaExpression lambdaExpression)
        {
            this.ThrowIfNeeded(lambdaExpression);
        }

        public virtual void VisitLockStatement(LockStatement lockStatement)
        {
            this.ThrowIfNeeded(lockStatement);
        }

        public virtual void VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
        {
            this.ThrowIfNeeded(memberReferenceExpression);
        }

        public virtual void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
        {
            this.ThrowIfNeeded(methodDeclaration);
        }

        public virtual void VisitNamedArgumentExpression(NamedArgumentExpression namedArgumentExpression)
        {
            this.ThrowIfNeeded(namedArgumentExpression);
        }

        public virtual void VisitNamedExpression(NamedExpression namedExpression)
        {
            this.ThrowIfNeeded(namedExpression);
        }

        public virtual void VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
        {
            this.ThrowIfNeeded(namespaceDeclaration);
        }

        public virtual void VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
        {
            this.ThrowIfNeeded(objectCreateExpression);
        }

        public virtual void VisitParameterDeclaration(ParameterDeclaration parameterDeclaration)
        {
            this.ThrowIfNeeded(parameterDeclaration);
        }

        public virtual void VisitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression)
        {
            this.ThrowIfNeeded(parenthesizedExpression);
        }

        public virtual void VisitPatternPlaceholder(AstNode placeholder, ICSharpCode.NRefactory.PatternMatching.Pattern pattern)
        {
            this.ThrowIfNeeded(placeholder);
        }

        public virtual void VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
        {
            this.ThrowIfNeeded(pointerReferenceExpression);
        }

        public virtual void VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
        {
            this.ThrowIfNeeded(primitiveExpression);
        }

        public virtual void VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
        {
            this.ThrowIfNeeded(propertyDeclaration);
        }

        public virtual void VisitQueryContinuationClause(QueryContinuationClause queryContinuationClause)
        {
            this.ThrowIfNeeded(queryContinuationClause);
        }

        public virtual void VisitQueryExpression(QueryExpression queryExpression)
        {
            this.ThrowIfNeeded(queryExpression);
        }

        public virtual void VisitQueryFromClause(QueryFromClause queryFromClause)
        {
            this.ThrowIfNeeded(queryFromClause);
        }

        public virtual void VisitQueryGroupClause(QueryGroupClause queryGroupClause)
        {
            this.ThrowIfNeeded(queryGroupClause);
        }

        public virtual void VisitQueryJoinClause(QueryJoinClause queryJoinClause)
        {
            this.ThrowIfNeeded(queryJoinClause);
        }

        public virtual void VisitQueryLetClause(QueryLetClause queryLetClause)
        {
            this.ThrowIfNeeded(queryLetClause);
        }

        public virtual void VisitQueryOrderClause(QueryOrderClause queryOrderClause)
        {
            this.ThrowIfNeeded(queryOrderClause);
        }

        public virtual void VisitQueryOrdering(QueryOrdering queryOrdering)
        {
            this.ThrowIfNeeded(queryOrdering);
        }

        public virtual void VisitQuerySelectClause(QuerySelectClause querySelectClause)
        {
            this.ThrowIfNeeded(querySelectClause);
        }

        public virtual void VisitQueryWhereClause(QueryWhereClause queryWhereClause)
        {
            this.ThrowIfNeeded(queryWhereClause);
        }

        public virtual void VisitReturnStatement(ReturnStatement returnStatement)
        {
            this.ThrowIfNeeded(returnStatement);
        }

        public virtual void VisitSizeOfExpression(SizeOfExpression sizeOfExpression)
        {
            this.ThrowIfNeeded(sizeOfExpression);
        }

        public virtual void VisitStackAllocExpression(StackAllocExpression stackAllocExpression)
        {
            this.ThrowIfNeeded(stackAllocExpression);
        }

        public virtual void VisitSwitchSection(SwitchSection switchSection)
        {
            this.ThrowIfNeeded(switchSection);
        }

        public virtual void VisitSwitchStatement(SwitchStatement switchStatement)
        {
            this.ThrowIfNeeded(switchStatement);
        }

        public virtual void VisitSyntaxTree(SyntaxTree syntaxTree)
        {
            this.ThrowIfNeeded(syntaxTree);
        }

        public virtual void VisitText(TextNode textNode)
        {
            this.ThrowIfNeeded(textNode);
        }

        public virtual void VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression)
        {
            this.ThrowIfNeeded(thisReferenceExpression);
        }

        public virtual void VisitThrowStatement(ThrowStatement throwStatement)
        {
            this.ThrowIfNeeded(throwStatement);
        }

        public virtual void VisitTryCatchStatement(TryCatchStatement tryCatchStatement)
        {
            this.ThrowIfNeeded(tryCatchStatement);
        }

        public virtual void VisitTypeDeclaration(TypeDeclaration typeDeclaration)
        {
            this.ThrowIfNeeded(typeDeclaration);
        }

        public virtual void VisitTypeOfExpression(TypeOfExpression typeOfExpression)
        {
            this.ThrowIfNeeded(typeOfExpression);
        }

        public virtual void VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
        {
            this.ThrowIfNeeded(typeReferenceExpression);
        }

        public virtual void VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
        {
            this.ThrowIfNeeded(unaryOperatorExpression);
        }

        public virtual void VisitUncheckedExpression(UncheckedExpression uncheckedExpression)
        {
            this.ThrowIfNeeded(uncheckedExpression);
        }

        public virtual void VisitUncheckedStatement(UncheckedStatement uncheckedStatement)
        {
            this.ThrowIfNeeded(uncheckedStatement);
        }

        public virtual void VisitUndocumentedExpression(UndocumentedExpression undocumentedExpression)
        {
            this.ThrowIfNeeded(undocumentedExpression);
        }

        public virtual void VisitUnsafeStatement(UnsafeStatement unsafeStatement)
        {
            this.ThrowIfNeeded(unsafeStatement);
        }

        public virtual void VisitUsingDeclaration(UsingDeclaration usingDeclaration)
        {
            this.ThrowIfNeeded(usingDeclaration);
        }

        public virtual void VisitUsingStatement(UsingStatement usingStatement)
        {
            this.ThrowIfNeeded(usingStatement);
        }

        public virtual void VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
        {
            this.ThrowIfNeeded(variableDeclarationStatement);
        }

        public virtual void VisitVariableInitializer(VariableInitializer variableInitializer)
        {
            this.ThrowIfNeeded(variableInitializer);
        }

        public virtual void VisitWhileStatement(WhileStatement whileStatement)
        {
            this.ThrowIfNeeded(whileStatement);
        }

        public virtual void VisitWhitespace(WhitespaceNode whitespaceNode)
        {
            this.ThrowIfNeeded(whitespaceNode);
        }

        public virtual void VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement)
        {
            this.ThrowIfNeeded(yieldBreakStatement);
        }

        public virtual void VisitYieldReturnStatement(YieldReturnStatement yieldReturnStatement)
        {
            this.ThrowIfNeeded(yieldReturnStatement);
        }

        public virtual void VisitErrorNode(AstNode errorNode)
        {
            this.ThrowIfNeeded(errorNode);
        }
    }
}