using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator
{
    public abstract partial class Visitor : IAstVisitor
    {
        public virtual void VisitAttributeSection(AttributeSection attributeSection)
        {
        }

        public virtual void VisitCSharpTokenNode(CSharpTokenNode cSharpTokenNode)
        {
        }

        public virtual void VisitComment(Comment comment)
        {
        }

        public virtual void VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression)
        {
        }

        public virtual void VisitIdentifier(Identifier identifier)
        {
        }

        public virtual void VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression)
        {
        }

        public virtual void VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
        {
        }

        public virtual void VisitTypeParameterDeclaration(TypeParameterDeclaration typeParameterDeclaration)
        {
        }

        public virtual void VisitPrimitiveType(PrimitiveType primitiveType)
        {
        }

        public virtual void VisitSimpleType(SimpleType simpleType)
        {
        }

        public virtual void VisitNullNode(AstNode nullNode)
        {
        }

        public virtual void VisitNewLine(NewLineNode newLineNode)
        {
        }

        public virtual void VisitConstraint(Constraint constraint)
        {
        }

        public virtual void VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
        {
        }

        public virtual void VisitMemberType(MemberType memberType)
        {
        }

        public virtual void VisitUsingAliasDeclaration(UsingAliasDeclaration usingAliasDeclaration)
        {
        }
    }
}