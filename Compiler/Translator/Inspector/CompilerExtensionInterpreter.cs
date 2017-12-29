using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bridge.CompilerServices;
using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using Expression = ICSharpCode.NRefactory.CSharp.Expression;
using InvocationExpression = ICSharpCode.NRefactory.CSharp.InvocationExpression;
using LambdaExpression = ICSharpCode.NRefactory.CSharp.LambdaExpression;

namespace Bridge.Translator
{
    class CompilerExtensionInterpreter : DepthFirstAstVisitor
    {
        private readonly IMemberResolver _resolver;
        private readonly BlockStatement _statement;

        private Dictionary<string, AttributeBuilderDetails> _attributeBuilderVariables;

        private class AttributeBuilderDetails
        {
            public IEntity Entity { get; set; }
            public IAssembly Assembly { get; set; }
            public IParameter Parameter { get; set; }
            public AttributeTarget Target { get; set; }
        }

        public CompilerExtensionInterpreter(IMemberResolver resolver, BlockStatement statement)
        {
            this._statement = statement;
            this._resolver = resolver;
        }

        public void Execute()
        {
            _attributeBuilderVariables = new Dictionary<string, AttributeBuilderDetails>();
            _statement.AcceptVisitor(this);
        }

        public override void VisitExpressionStatement(ExpressionStatement expressionStatement)
        {
            // we visit the expression of expression statements, allowed expressions are treated separately
            var expression = expressionStatement.Expression;
            expression.AcceptVisitor(this);
        }

        public override void VisitBlockStatement(BlockStatement blockStatement)
        {
            // block statements are simple iterated
            foreach (var statement in blockStatement)
            {
                statement.AcceptVisitor(this);
            }
        }

        public override void VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
        {
            // we allow variable declarations. they might make it easier to invoke compiler extensions 
            // e.g. var attributes = context.Attributes;
            // attributes.Class<Test>()...
            var variableType = this._resolver.ResolveNode(variableDeclarationStatement.Type);
            if (variableType.Type == null)
            {
                throw new EmitterException(variableDeclarationStatement,
                    "Could not infer type on variable declaration");
            }

            if (variableType.Type.FullName == "Bridge.CompilerServices.IAttributesBuilder")
            {
                foreach (var variable in variableDeclarationStatement.Variables)
                {
                    _attributeBuilderVariables[variable.Name] = GetAttributesBuilderVariable(variable.Initializer);
                }
            }
        }

        public override void VisitAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            // we allow/ignore assignments. they might make it easier to invoke compiler extensions 
            var resolvedVariable = this._resolver.ResolveNode(assignmentExpression.Left) as LocalResolveResult;
            if (resolvedVariable == null || resolvedVariable.Variable == null)
            {
                return;
            }
            _attributeBuilderVariables[resolvedVariable.Variable.Name] = GetAttributesBuilderVariable(assignmentExpression.Right);
        }


        private AttributeBuilderDetails GetAttributesBuilderVariable(Expression variableInitializer)
        {
            if (variableInitializer == null)
            {
                return null;
            }

            var invocation = variableInitializer as InvocationExpression;
            if (invocation != null)
            {
                var invocedMethodRR = this._resolver.ResolveNode(invocation) as InvocationResolveResult;
                if (invocedMethodRR == null || invocedMethodRR.Member == null || invocedMethodRR.Member.DeclaringType.FullName != "Bridge.CompilerServices.IAttributesContext")
                {
                    return null;
                }
                var invocedMethod = (IMethod)invocedMethodRR.Member;
                switch (invocedMethod.Name)
                {
                    case "Assembly":
                        {
                            IAssembly assembly;
                            if (invocation.Arguments.Count == 0)
                            {
                                assembly = this._resolver.Compilation.MainAssembly;
                            }
                            else
                            {
                                var assemblyNameArgument = invocedMethodRR.Arguments[0];
                                if (!assemblyNameArgument.IsCompileTimeConstant || assemblyNameArgument.ConstantValue == null)
                                {
                                    throw new EmitterException(invocation, "The assembly name must be compile time constant");
                                }

                                var assemblyName = assemblyNameArgument.ConstantValue.ToString();


                                assembly = this._resolver.Compilation.Assemblies
                                    .FirstOrDefault(a => a.FullAssemblyName == assemblyName);
                                if (assembly == null)
                                {
                                    assembly = this._resolver.Compilation.Assemblies
                                        .FirstOrDefault(a => a.AssemblyName == assemblyName);
                                }

                                if (assembly == null)
                                {
                                    throw new EmitterException(invocation,
                                        "Could not resolve assembly please specify full assembly name");
                                }
                            }

                            return new AttributeBuilderDetails
                            {
                                Assembly = assembly,
                                Target = AttributeTarget.Default
                            };
                        }
                    case "Type":
                        {
                            var memberAccess = invocation.Target as MemberReferenceExpression;
                            if (memberAccess == null || memberAccess.TypeArguments.Count == 0)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve type, please apply type arguments to the Type<>() invocation");
                            }
                            var type = invocedMethod.TypeArguments[0].GetDefinition();
                            return new AttributeBuilderDetails
                            {
                                Entity = type,
                                Target = AttributeTarget.Default
                            };
                        }
                    case "Member":
                        {
                            if (invocation.Arguments.Count == 0)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve type, please ensure all arguments are specified");
                            }

                            var arguments = invocation.Arguments.ToArray();
                            var lambda = arguments[0] as LambdaExpression;
                            if (lambda == null || lambda.Body == null || lambda.Body is BlockStatement)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve member, please ensure to use a simple lambda expression to access the member");
                            }

                            AttributeBuilderDetails details = new AttributeBuilderDetails();
                            var accessedMemberRR =
                                this._resolver.ResolveNode(lambda.Body) as MemberResolveResult;
                            if (accessedMemberRR == null || accessedMemberRR.Member == null)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve member, please ensure to use a simple lambda expression to access the member");
                            }

                            details.Entity = accessedMemberRR.Member.MemberDefinition;

                            string targetName = null;
                            for (int i = 1; i < invocedMethodRR.Arguments.Count; i++)
                            {
                                var argument = invocedMethodRR.Arguments[i];
                                if (argument.Type.FullName == "Bridge.CompilerServices.AttributeTarget")
                                {
                                    if (!argument.IsCompileTimeConstant)
                                    {
                                        throw new EmitterException(invocation,
                                            "The target parameter must be compile time constant");

                                    }
                                    details.Target = (AttributeTarget)(int)argument.ConstantValue;
                                }
                                else if (argument.Type.IsKnownType(KnownTypeCode.String))
                                {
                                    if (!argument.IsCompileTimeConstant)
                                    {
                                        throw new EmitterException(invocation,
                                            "The targetName parameter must be compile time constant");

                                    }
                                    targetName = (string)argument.ConstantValue;
                                }
                            }

                            if (targetName != null)
                            {
                                if (details.Target == AttributeTarget.Parameter)
                                {
                                    var method = details.Entity as IMethod;
                                    if (method != null)
                                    {
                                        details.Parameter =
                                            method.Parameters.FirstOrDefault(p => p.Name == targetName);
                                    }
                                }
                            }

                            switch (details.Target)
                            {
                                case AttributeTarget.Default:
                                    break;
                                case AttributeTarget.ReturnValue:
                                    if (!(details.Entity is IMethod))
                                    {
                                        throw new EmitterException(invocation,
                                            "Return type targets are only allowed for properties");
                                    }
                                    break;
                                case AttributeTarget.Parameter:
                                    if (details.Parameter == null)
                                    {
                                        throw new EmitterException(invocation, string.Format(
                                            "Could not find parameter with name '{0}' in method '{1}'",
                                            targetName ?? "<empty>", details.Entity.FullName));
                                    }
                                    break;
                                case AttributeTarget.Getter:
                                    {
                                        var property = details.Entity as IProperty;
                                        if (property == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Getter targets are only allowed for properties");
                                        }
                                        details.Entity = property.Getter;
                                    }
                                    break;
                                case AttributeTarget.Setter:
                                    {
                                        var property = details.Entity as IProperty;
                                        if (property == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Setter targets are only allowed for properties");
                                        }
                                        details.Entity = property.Setter;
                                    }
                                    break;
                                case AttributeTarget.Adder:
                                    {
                                        var evt = details.Entity as IEvent;
                                        if (evt == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Adder targets are only allowed for events");
                                        }
                                        details.Entity = evt.AddAccessor;
                                    }
                                    break;
                                case AttributeTarget.Remover:
                                    {
                                        var evt = details.Entity as IEvent;
                                        if (evt == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Remover targets are only allowed for events");
                                        }
                                        details.Entity = evt.RemoveAccessor;
                                    }
                                    break;
                            }

                            return details;
                        }
                    case "Constructor":
                        {
                            if (invocation.Arguments.Count == 0)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve type, please ensure all arguments are specified");
                            }

                            var arguments = invocation.Arguments.ToArray();
                            var lambda = arguments[0] as LambdaExpression;
                            if (lambda == null || lambda.Body == null || lambda.Body is BlockStatement)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve member, please ensure to use a simple lambda expression to create an instance");
                            }

                            AttributeBuilderDetails details = new AttributeBuilderDetails();
                            var accessedMemberRR =
                                this._resolver.ResolveNode(lambda.Body) as InvocationResolveResult;
                            if (accessedMemberRR == null || accessedMemberRR.Member == null || !(accessedMemberRR.Member is IMethod) || !((IMethod)accessedMemberRR.Member).IsConstructor)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve member, please ensure to use a simple lambda expression to create an instance");
                            }

                            details.Entity = accessedMemberRR.Member.MemberDefinition;

                            return details;
                        }
                    case "Event":
                        {
                            var memberAccess = invocation.Target as MemberReferenceExpression;
                            if (memberAccess == null || memberAccess.TypeArguments.Count == 0)
                            {
                                throw new EmitterException(invocation,
                                    "Could not resolve type, please apply type arguments to the Event<>() invocation");
                            }

                            var eventNameArgument = invocedMethodRR.Arguments[0];
                            if (!eventNameArgument.IsCompileTimeConstant || eventNameArgument.ConstantValue == null)
                            {
                                throw new EmitterException(invocation, "The event name must be compile time constant");
                            }

                            var eventName = eventNameArgument.ConstantValue.ToString();

                            var type = invocedMethod.TypeArguments[0].GetDefinition();
                            var eventInType = type.Events.FirstOrDefault(e => e.Name == eventName);
                            if (eventInType == null)
                            {
                                throw new EmitterException(invocation, string.Format("Could not find event with name '{0}' in type '{1}", eventName, type.FullName));
                            }

                            AttributeBuilderDetails details = new AttributeBuilderDetails();
                            details.Entity = eventInType.MemberDefinition;

                            for (int i = 1; i < invocedMethodRR.Arguments.Count; i++)
                            {
                                var argument = invocedMethodRR.Arguments[1];
                                if (argument.Type.FullName == "Bridge.CompilerServices.AttributeTarget")
                                {
                                    if (!argument.IsCompileTimeConstant)
                                    {
                                        throw new EmitterException(invocation,
                                            "The target parameter must be compile time constant");

                                    }
                                    details.Target = (AttributeTarget)(int)argument.ConstantValue;
                                }
                            }

                            switch (details.Target)
                            {
                                case AttributeTarget.Adder:
                                    {
                                        var evt = details.Entity as IEvent;
                                        if (evt == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Adder targets are only allowed for events");
                                        }
                                        details.Entity = evt.AddAccessor;
                                    }
                                    break;
                                case AttributeTarget.Remover:
                                    {
                                        var evt = details.Entity as IEvent;
                                        if (evt == null)
                                        {
                                            throw new EmitterException(invocation,
                                                "Remover targets are only allowed for events");
                                        }
                                        details.Entity = evt.RemoveAccessor;
                                    }
                                    break;
                                case AttributeTarget.Default:
                                    break;
                                default:
                                    throw new EmitterException(invocation,
                                        "Invalid AttributeTarget specified for event, allowed values are Default, Adder, Remover");
                            }

                            return details;
                        }
                }
            }

            return null;
        }


        public override void VisitInvocationExpression(InvocationExpression invocationExpression)
        {
            var member = this._resolver.ResolveNode(invocationExpression) as MemberResolveResult;
            if (member == null || member.Member == null || !(member.Member is IMethod))
            {
                throw new EmitterException(invocationExpression, "Could not resolve invoked method, please simplify method invocation.");
            }

            var method = (IMethod)member.Member;

            if (method.DeclaringTypeDefinition.FullName == "Bridge.CompilerServices.IAttributesBuilder")
            {
                HandleAttributeBuilderInvocation(invocationExpression, method);
            }
            else
            {
                throw new EmitterException(invocationExpression, "Invalid method invocation, only invocations to ICompilerContext related members is allowed");
            }
        }

        private void HandleAttributeBuilderInvocation(InvocationExpression invocationExpression, IMethod method)
        {
            // add attribute
            if (method.Name == "Add" && invocationExpression.Target is MemberReferenceExpression)
            {
                var memberReference = (MemberReferenceExpression)invocationExpression.Target;
                AttributeBuilderDetails variableDetails = null;
                if (memberReference.Target is InvocationExpression)
                {
                    // chained method calls
                    variableDetails = GetAttributesBuilderVariable(memberReference.Target);
                }
                else
                {
                    var targetRR = this._resolver.ResolveNode(memberReference.Target);
                    if (targetRR == null || targetRR.IsError)
                    {
                        throw new EmitterException(invocationExpression,
                            "Could not resolve invoked method, please simplify method invocation.");
                    }
                    if (targetRR is LocalResolveResult)
                    {
                        // variable access
                        var localRR = targetRR as LocalResolveResult;
                        if (localRR.Variable == null ||
                            !_attributeBuilderVariables.TryGetValue(localRR.Variable.Name, out variableDetails))
                        {
                            throw new EmitterException(invocationExpression,
                                "Could not resolve local member.");
                        }
                    }
                }

                if (variableDetails == null)
                {
                    throw new EmitterException(invocationExpression,
                        "Could not resolve details of IAttributeContext on which the method was invoked, please simplify method invocation.");
                }

                var arguments = invocationExpression.Arguments.ToArray();
                foreach (var argument in arguments)
                {
                    if (!(argument is ObjectCreateExpression))
                    {
                        throw new EmitterException(invocationExpression,
                            "Attributes must be directly created via new expression and object initializers");
                    }

                    var newAttribute = (ObjectCreateExpression) argument;
                    var attributeRR = this._resolver.ResolveNode(argument) as InvocationResolveResult;
                    if (attributeRR == null || attributeRR.Type == null || !(attributeRR.Member is IMethod) ||
                        !((IMethod) attributeRR.Member).IsConstructor)
                    {
                        throw new EmitterException(invocationExpression,
                            "Could not resolve attribute type constructor invocation");
                    }

                    var constructorArguments = new List<ResolveResult>();
                    var namedArguments = new Dictionary<IMember, ResolveResult>();

                    foreach (var ctorArgument in attributeRR.Arguments)
                    {
                        if (!ctorArgument.IsCompileTimeConstant)
                        {
                            throw new EmitterException(newAttribute,
                                "All attribute parameters must be compile time constant");
                        }
                        constructorArguments.Add(ctorArgument);
                    }

                    foreach (var initializerStatement in attributeRR.InitializerStatements)
                    {
                        var assignOperator = initializerStatement as OperatorResolveResult;
                        if (assignOperator == null || assignOperator.OperatorType != ExpressionType.Assign)
                        {
                            throw new EmitterException(newAttribute,
                                "Unexpected object initializer content, please only use property assignments.");
                        }

                        var left = assignOperator.Operands[0] as MemberResolveResult;
                        if (left == null)
                        {
                            throw new EmitterException(newAttribute,
                                "The assigned attribute property value must be compile time constant.");
                        }

                        var right = assignOperator.Operands[1];
                        if (!right.IsCompileTimeConstant)
                        {
                            throw new EmitterException(newAttribute,
                                "The assigned attribute property value must be compile time constant.");
                        }

                        namedArguments[left.Member] = right;
                    }

                    this.RegisterAttribute(variableDetails,
                        new DefaultAttribute((IMethod) attributeRR.Member, constructorArguments,
                            namedArguments.ToList(), DomRegion.Empty));
                }
            }
            else
            {
                throw new EmitterException(invocationExpression,
                    "Unknown method called on IAttributesBuider, please simplify method invocation.");
            }
        }

        private void RegisterAttribute(AttributeBuilderDetails variableDetails, DefaultAttribute attribute)
        {
            if (variableDetails.Assembly != null)
            {
                AttributeRegistry.RegisterAttribute(variableDetails.Assembly, attribute);

            }
            switch (variableDetails.Target)
            {
                case AttributeTarget.Parameter:
                    AttributeRegistry.RegisterAttribute(variableDetails.Parameter, attribute);
                    break;
                case AttributeTarget.ReturnValue:
                    AttributeRegistry.RegisterReturnValueAttribute(variableDetails.Entity, attribute);
                    break;
                default:
                    AttributeRegistry.RegisterAttribute(variableDetails.Entity, attribute);
                    break;
            }
        }

        protected override void VisitChildren(AstNode node)
        {
            throw new EmitterException(node, "Unsupported statement or expression. Compiler extensions only support very simple invocations to the ICompilerContext and members");
        }
    }
}
