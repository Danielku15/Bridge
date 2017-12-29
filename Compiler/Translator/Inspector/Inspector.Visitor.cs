using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bridge.Translator
{
    public partial class Inspector : Visitor
    {
        public override void VisitSyntaxTree(SyntaxTree node)
        {
            node.AcceptChildren(this);
        }

        public override void VisitUsingDeclaration(UsingDeclaration usingDeclaration)
        {
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
        {
            if (!this.Translator.AssemblyInfo.Assembly.EnableReservedNamespaces)
            {
                this.ValidateNamespace(namespaceDeclaration);
            }

            var prevNamespace = this._namespace;

            this._namespace = namespaceDeclaration.Name;

            namespaceDeclaration.AcceptChildren(this);

            this._namespace = prevNamespace;
        }

        public override void VisitTypeDeclaration(TypeDeclaration typeDeclaration)
        {
            if (this._currentType != null)
            {
                this._nestedTypes = this._nestedTypes ?? new List<Tuple<TypeDeclaration, ITypeInfo>>();
                this._nestedTypes.Add(new Tuple<TypeDeclaration, ITypeInfo>(typeDeclaration, this._currentType));
                return;
            }

            var rr = this.Translator.Resolver.ResolveNode(typeDeclaration);
            var add = true;
            var typeInfo = this.Translator.Types.Get(rr.Type);
            var ignored = typeInfo.IsOutputType.HasValue && !typeInfo.IsOutputType.Value;

            var typeDef = typeInfo.Type;

            if (!this.Translator.AssemblyInfo.Assembly.EnableReservedNamespaces)
            {
                this.ValidateNamespace(typeInfo, typeDeclaration);
            }

            this.CheckObjectLiteral(typeInfo.Type, typeDeclaration);

            if ((typeDef.IsExternal() || ignored || typeDef.IsNonScriptable()) && !typeDef.IsObjectLiteral())
            {
                if (typeInfo.IsOutputType.HasValue && typeInfo.IsOutputType.Value)
                {
                    this.Translator.Types.RemoveFromOutput(typeInfo);
                }

                if (!typeInfo.IsOutputType.HasValue)
                {
                    typeInfo.IsOutputType = false;
                }

                return;
            }

            if (!typeInfo.IsOutputType.HasValue)
            {
                typeInfo.TypeDeclaration = typeDeclaration;
            }
            else
            {
                typeInfo.PartialTypeDeclarations.Add(typeDeclaration);
                add = false;
            }

            this._currentType = typeInfo;

            typeDeclaration.AcceptChildren(this);

            if (add)
            {
                this.Translator.Types.AddToOutput(typeInfo);
            }

            if (typeDeclaration.ClassType != ClassType.Interface)
            {
                this.AddMissingAliases(typeDeclaration);
            }

            this._currentType = null;

            while (this._nestedTypes != null && this._nestedTypes.Count > 0)
            {
                var types = this._nestedTypes;
                this._nestedTypes = null;
                foreach (var nestedType in types)
                {
                    this.VisitTypeDeclaration(nestedType.Item1);
                }
            }
        }

        private void AddMissingAliases(TypeDeclaration typeDeclaration)
        {
            var type = this.Translator.Resolver.ResolveNode(typeDeclaration).Type;
            var interfaces = type.DirectBaseTypes.Where(t => t.Kind == TypeKind.Interface).ToArray();
            var members = type.GetMembers(null, GetMemberOptions.IgnoreInheritedMembers).ToArray();
            var baseTypes = type.GetNonInterfaceBaseTypes().Reverse().ToArray();

            if (interfaces.Length > 0)
            {
                foreach (var baseInterface in interfaces)
                {
                    var interfaceMembers = baseInterface.GetMembers().Where(m => m.DeclaringTypeDefinition.Kind == TypeKind.Interface);
                    foreach (var interfaceMember in interfaceMembers)
                    {
                        var isDirectlyImplemented = members.Any(m => m.ImplementedInterfaceMembers.Contains(interfaceMember));
                        if (!isDirectlyImplemented)
                        {
                            foreach (var baseType in baseTypes)
                            {
                                //var derivedMember = InheritanceHelper.GetDerivedMember(interfaceMember, baseType.GetDefinition());
                                IMember derivedMember = null;
                                var baseMembers = interfaceMember.SymbolKind == SymbolKind.Accessor 
                                    ? baseType.GetAccessors(m => m.Name == interfaceMember.Name && !m.IsExplicitInterfaceImplementation, GetMemberOptions.IgnoreInheritedMembers) 
                                    : baseType.GetMembers(m => m.Name == interfaceMember.Name && !m.IsExplicitInterfaceImplementation, GetMemberOptions.IgnoreInheritedMembers);

                                foreach (IMember baseMember in baseMembers)
                                {
                                    if (baseMember.IsPrivate)
                                    {
                                        continue;
                                    }
                                    if (SignatureComparer.Ordinal.Equals(interfaceMember, baseMember))
                                    {
                                        derivedMember = baseMember.Specialize(interfaceMember.Substitution);
                                        break;
                                    }
                                }

                                if (derivedMember != null && !derivedMember.ImplementedInterfaceMembers.Contains(interfaceMember))
                                {
                                    this._currentType.InstanceConfig.Alias.Add(new TypeConfigItem { Entity = typeDeclaration, InterfaceMember = interfaceMember, DerivedMember = derivedMember });
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
        {
            bool isStatic = this._currentType.ClassType == ClassType.Enum
                || fieldDeclaration.HasModifier(Modifiers.Static)
                || fieldDeclaration.HasModifier(Modifiers.Const);

            foreach (var item in fieldDeclaration.Variables)
            {
                var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(item);
                if (fieldDeclaration.HasModifier(Modifiers.Const) && rr != null && rr.Member.IsInlineConst())
                {
                    continue;
                }

                Expression initializer = item.Initializer;

                if (initializer.IsNull)
                {
                    if (this._currentType.ClassType == ClassType.Enum)
                    {
                        this.Throw(fieldDeclaration, "Enum items must be explicitly numbered");
                    }

                    initializer = this.GetDefaultFieldInitializer(fieldDeclaration.ReturnType);
                }

                this._currentType.FieldsDeclarations.Add(item.Name, fieldDeclaration);

                string prefix = SharpSixRewriter.AutoInitFieldPrefix;
                bool autoInitializer = item.Name.StartsWith(prefix);
                string name = autoInitializer ? item.Name.Substring(prefix.Length) : item.Name;

                if (isStatic)
                {
                    var collection = this._currentType.StaticConfig.Fields;
                    if (autoInitializer)
                    {
                        collection = this._currentType.StaticConfig.AutoPropertyInitializers;
                        var prop = this._currentType.StaticConfig.Properties.FirstOrDefault(p => p.Name == name)
                            ?? this._currentType.StaticConfig.Fields.FirstOrDefault(p => p.Name == name);

                        if (prop != null)
                        {
                            prop.Initializer = initializer;
                            prop.IsPropertyInitializer = true;
                        }
                    }

                    collection.Add(new TypeConfigItem
                    {
                        Name = name,
                        Entity = fieldDeclaration,
                        IsConst = fieldDeclaration.HasModifier(Modifiers.Const),
                        VarInitializer = item,
                        Initializer = initializer
                    });
                }
                else
                {
                    var collection = this._currentType.InstanceConfig.Fields;
                    if (autoInitializer)
                    {
                        collection = this._currentType.InstanceConfig.AutoPropertyInitializers;
                        var prop = this._currentType.InstanceConfig.Properties.FirstOrDefault(p => p.Name == name) ??
                                   this._currentType.InstanceConfig.Fields.FirstOrDefault(p => p.Name == name);

                        if (prop != null)
                        {
                            prop.Initializer = initializer;
                            prop.IsPropertyInitializer = true;
                        }
                    }

                    collection.Add(new TypeConfigItem
                    {
                        Name = name,
                        Entity = fieldDeclaration,
                        VarInitializer = item,
                        Initializer = initializer
                    });
                }

                if (OverloadsCollection.NeedCreateAlias(rr))
                {
                    var config = isStatic
                    ? this._currentType.StaticConfig
                    : this._currentType.InstanceConfig;
                    config.Alias.Add(new TypeConfigItem { Entity = fieldDeclaration, VarInitializer = item });
                }
            }
        }

        public override void VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
        {
            var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(constructorDeclaration);
            if (rr.Member.HasTemplate() || constructorDeclaration.HasModifier(Modifiers.Extern) && !rr.Member.HasScript())
            {
                return;
            }

            bool isStatic = constructorDeclaration.HasModifier(Modifiers.Static);

            this.FixMethodParameters(constructorDeclaration.Parameters, constructorDeclaration.Body);

            if (isStatic)
            {
                this._currentType.StaticCtor = constructorDeclaration;
            }
            else
            {
                this._currentType.Ctors.Add(constructorDeclaration);
            }
        }

        public override void VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
        {
            var rr = this.Translator.Resolver.ResolveNode< MemberResolveResult>(operatorDeclaration);
            if (rr.Member.HasTemplate())
            {
                return;
            }

            this.FixMethodParameters(operatorDeclaration.Parameters, operatorDeclaration.Body);

            Dictionary<OperatorType, List<OperatorDeclaration>> dict = this._currentType.Operators;

            var key = operatorDeclaration.OperatorType;
            if (dict.ContainsKey(key))
            {
                dict[key].Add(operatorDeclaration);
            }
            else
            {
                dict.Add(key, new List<OperatorDeclaration>(new[] { operatorDeclaration }));
            }
        }

        public override void VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
        {
            if (indexerDeclaration.HasModifier(Modifiers.Abstract))
            {
                return;
            }

            IDictionary<string, List<EntityDeclaration>> dict = this._currentType.InstanceProperties;

            var key = indexerDeclaration.Name;

            if (dict.ContainsKey(key))
            {
                dict[key].Add(indexerDeclaration);
            }
            else
            {
                dict.Add(key, new List<EntityDeclaration>(new[] { indexerDeclaration }));
            }

            var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(indexerDeclaration);
            if (OverloadsCollection.NeedCreateAlias(rr))
            {
                var config = rr.Member.IsStatic
                ? this._currentType.StaticConfig
                : this._currentType.InstanceConfig;
                config.Alias.Add(new TypeConfigItem { Entity = indexerDeclaration });
            }
        }

        public override void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
        {
            var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(methodDeclaration);

            if (rr.Member.IsCompilerExtension())
            {
                this.ExecuteCompilerExtension(methodDeclaration, (IMethod)rr.Member);
                return;
            }

            if (methodDeclaration.HasModifier(Modifiers.Abstract) || rr.Member.HasTemplate())
            {
                return;
            }

            this.FixMethodParameters(methodDeclaration.Parameters, methodDeclaration.Body);

            bool isStatic = methodDeclaration.HasModifier(Modifiers.Static);

            var dict = isStatic
                ? this._currentType.StaticMethods
                : this._currentType.InstanceMethods;

            var key = methodDeclaration.Name;
            var memberrr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(methodDeclaration);

            if (!dict.TryGetValue(key, out var list))
            {
                dict[key] = list = new List<MethodDeclarationAndSymbol>(); 
            }
            list.Add(new MethodDeclarationAndSymbol(memberrr.Member as IMethod, methodDeclaration));

            if (OverloadsCollection.NeedCreateAlias(memberrr))
            {
                var config = isStatic
                ? this._currentType.StaticConfig
                : this._currentType.InstanceConfig;
                config.Alias.Add(new TypeConfigItem { Entity = methodDeclaration });
            }
        }

        public override void VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
        {
            if (customEventDeclaration.HasModifier(Modifiers.Abstract))
            {
                return;
            }

            bool isStatic = customEventDeclaration.HasModifier(Modifiers.Static);

            IDictionary<string, List<EntityDeclaration>> dict = isStatic
                ? this._currentType.StaticProperties
                : this._currentType.InstanceProperties;

            var key = customEventDeclaration.Name;

            if (dict.ContainsKey(key))
            {
                dict[key].Add(customEventDeclaration);
            }
            else
            {
                dict.Add(key, new List<EntityDeclaration>(new[] { customEventDeclaration }));
            }

            var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(customEventDeclaration);
            if (OverloadsCollection.NeedCreateAlias(rr))
            {
                var config = rr.Member.IsStatic
                ? this._currentType.StaticConfig
                : this._currentType.InstanceConfig;
                config.Alias.Add(new TypeConfigItem { Entity = customEventDeclaration });
            }
        }

        public override void VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
        {
            if (propertyDeclaration.HasModifier(Modifiers.Abstract))
            {
                return;
            }

            bool isStatic = propertyDeclaration.HasModifier(Modifiers.Static);

            IDictionary<string, List<EntityDeclaration>> dict = isStatic
                ? this._currentType.StaticProperties
                : this._currentType.InstanceProperties;

            var key = propertyDeclaration.Name;

            if (dict.ContainsKey(key))
            {
                dict[key].Add(propertyDeclaration);
            }
            else
            {
                dict.Add(key, new List<EntityDeclaration>(new[] { propertyDeclaration }));
            }

            var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(propertyDeclaration);
            if (OverloadsCollection.NeedCreateAlias(rr))
            {
                var config = rr.Member.IsStatic
                ? this._currentType.StaticConfig
                : this._currentType.InstanceConfig;
                config.Alias.Add(new TypeConfigItem { Entity = propertyDeclaration });
            }

            if (!rr.Member.IsExternal() && !((IProperty)rr.Member).Getter.HasTemplate())
            {
                Expression initializer = this.GetDefaultFieldInitializer(propertyDeclaration.ReturnType);
                TypeConfigInfo info = isStatic ? this._currentType.StaticConfig : this._currentType.InstanceConfig;

                bool autoPropertyToField = false;
                if (rr.Member != null && Helpers.IsAutoProperty((IProperty)rr.Member))
                {
                    var rules = Rules.Get(this.Translator.Emitter, rr.Member);

                    if (rules.AutoProperty.HasValue)
                    {
                        autoPropertyToField = rules.AutoProperty.Value == AutoPropertyRule.Plain;
                    }
                    else
                    {
                        autoPropertyToField = rr.Member.HasFieldAttribute();

                        if (!autoPropertyToField && rr.Member.ImplementedInterfaceMembers.Count > 0)
                        {
                            foreach (var interfaceMember in rr.Member.ImplementedInterfaceMembers)
                            {
                                autoPropertyToField = interfaceMember.HasFieldAttribute(false);

                                if (autoPropertyToField)
                                {
                                    break;
                                }
                            }
                        }
                    }                    
                }

                var autoInitializer = info.AutoPropertyInitializers.FirstOrDefault(f => f.Name == key);

                if (autoInitializer != null)
                {
                    initializer = autoInitializer.Initializer;
                }

                if (!autoPropertyToField)
                {
                    info.Properties.Add(new TypeConfigItem
                    {
                        Name = key,
                        Entity = propertyDeclaration,
                        Initializer = initializer,
                        IsPropertyInitializer = autoInitializer != null
                    });
                }
                else
                {
                    info.Fields.Add(new TypeConfigItem
                    {
                        Name = key,
                        Entity = propertyDeclaration,
                        Initializer = initializer
                    });
                }
            }
        }

        public override void VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
        {
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
        {
            Expression initializer = enumMemberDeclaration.Initializer;
            var member = this.Translator.Resolver.ResolveNode<MemberResolveResult>(enumMemberDeclaration);
            var initializerIsString = false;
            if (member != null)
            {
                var enumMode = member.Member.DeclaringTypeDefinition.EnumEmitMode();

                if (enumMode >= 3 && enumMode < 7)
                {
                    initializerIsString = true;
                    var attrName = member.Member.GetNameAttribute();

                    if (attrName != null)
                    {
                        initializer = new PrimitiveExpression(attrName);
                    }
                    else
                    {
                        string enumStringName = member.Member.Name;
                        switch (enumMode)
                        {
                            case 3:
                                enumStringName = Object.Net.Utilities.StringUtils.ToLowerCamelCase(member.Member.Name);
                                break;

                            case 4:
                                break;

                            case 5:
                                enumStringName = enumStringName.ToLowerInvariant();
                                break;

                            case 6:
                                enumStringName = enumStringName.ToUpperInvariant();
                                break;
                        }

                        initializer = new PrimitiveExpression(enumStringName);
                    }
                }
            }

            if (!initializerIsString)
            {
                if (enumMemberDeclaration.Initializer.IsNull)
                {
                    dynamic i = this._currentType.LastEnumValue;
                    ++i;
                    this._currentType.LastEnumValue = i;

                    if (member != null && member.Member.DeclaringTypeDefinition.EnumUnderlyingType.IsKnownType(KnownTypeCode.Int64))
                    {
                        initializer = new PrimitiveExpression(Convert.ToInt64(this._currentType.LastEnumValue));
                    }
                    else if (member != null && member.Member.DeclaringTypeDefinition.EnumUnderlyingType.IsKnownType(KnownTypeCode.UInt64))
                    {
                        initializer = new PrimitiveExpression(Convert.ToUInt64(this._currentType.LastEnumValue));
                    }
                    else
                    {
                        initializer = new PrimitiveExpression(this._currentType.LastEnumValue);
                    }
                }
                else
                {
                    var rr = this.Translator.Resolver.ResolveNode<ConstantResolveResult>(enumMemberDeclaration.Initializer);
                    if (rr != null)
                    {
                        if (member != null && member.Member.DeclaringTypeDefinition.EnumUnderlyingType.IsKnownType(KnownTypeCode.Int64))
                        {
                            initializer = new PrimitiveExpression(Convert.ToInt64(rr.ConstantValue));
                        }
                        else if (member != null && member.Member.DeclaringTypeDefinition.EnumUnderlyingType.IsKnownType(KnownTypeCode.UInt64))
                        {
                            initializer = new PrimitiveExpression(Convert.ToUInt64(rr.ConstantValue));
                        }
                        else
                        {
                            initializer = new PrimitiveExpression(rr.ConstantValue);
                        }
                        this._currentType.LastEnumValue = rr.ConstantValue;
                    }
                }
            }

            this._currentType.StaticConfig.Fields.Add(new TypeConfigItem
            {
                Name = enumMemberDeclaration.Name,
                Entity = enumMemberDeclaration,
                Initializer = initializer
            });
        }

        public override void VisitEventDeclaration(EventDeclaration eventDeclaration)
        {
            bool isStatic = eventDeclaration.HasModifier(Modifiers.Static);
            foreach (var item in eventDeclaration.Variables)
            {
                Expression initializer = item.Initializer;
                this._currentType.EventsDeclarations.Add(item.Name, eventDeclaration);
                if (isStatic)
                {
                    this._currentType.StaticConfig.Events.Add(new TypeConfigItem
                    {
                        Name = item.Name,
                        Entity = eventDeclaration,
                        Initializer = initializer,
                        VarInitializer = item
                    });
                }
                else
                {
                    this._currentType.InstanceConfig.Events.Add(new TypeConfigItem
                    {
                        Name = item.Name,
                        Entity = eventDeclaration,
                        Initializer = initializer,
                        VarInitializer = item
                    });
                }

                var rr = this.Translator.Resolver.ResolveNode<MemberResolveResult>(item);
                if (OverloadsCollection.NeedCreateAlias(rr))
                {
                    var config = rr.Member.IsStatic
                    ? this._currentType.StaticConfig
                    : this._currentType.InstanceConfig;
                    config.Alias.Add(new TypeConfigItem { Entity = eventDeclaration, VarInitializer = item });
                }
            }
        }
    }
}