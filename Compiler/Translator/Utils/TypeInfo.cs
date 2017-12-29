using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Linq;
using Bridge.Contract.Constants;

namespace Bridge.Translator
{
    public class TypeInfo : ITypeInfo
    {
        public TypeInfo()
        {
            this.StaticMethods = new Dictionary<string, List<MethodDeclarationAndSymbol>>();
            this.InstanceMethods = new Dictionary<string, List<MethodDeclarationAndSymbol>>();
            this.StaticProperties = new Dictionary<string, List<EntityDeclaration>>();
            this.InstanceProperties = new Dictionary<string, List<EntityDeclaration>>();
            this.FieldsDeclarations = new Dictionary<string, FieldDeclaration>();
            this.EventsDeclarations = new Dictionary<string, EventDeclaration>();
            this.Dependencies = new List<IPluginDependency>();
            this.Ctors = new List<ConstructorDeclaration>();
            this.Operators = new Dictionary<OperatorType, List<OperatorDeclaration>>();
            this.StaticConfig = new TypeConfigInfo();
            this.InstanceConfig = new TypeConfigInfo();
            this.PartialTypeDeclarations = new List<TypeDeclaration>();
        }

        public bool? IsOutputType
        {
            get;
            set;
        }

        public string Key
        {
            get;
            set;
        }

        public int? Priority
        {
            get;
            set;
        }

        public TypeConfigInfo StaticConfig
        {
            get;
            set;
        }

        public TypeConfigInfo InstanceConfig
        {
            get;
            set;
        }

        public Dictionary<OperatorType, List<OperatorDeclaration>> Operators
        {
            get;
            protected set;
        }

        public Dictionary<string, EventDeclaration> EventsDeclarations
        {
            get;
            set;
        }

        public TypeDeclaration TypeDeclaration
        {
            get;
            set;
        }

        public List<TypeDeclaration> PartialTypeDeclarations
        {
            get;
            set;
        }

        public bool IsStatic
        {
            get;
            set;
        }

        public ClassType ClassType
        {
            get;
            set;
        }

        public string Namespace
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public List<ConstructorDeclaration> Ctors
        {
            get;
            set;
        }

        public ConstructorDeclaration StaticCtor
        {
            get;
            set;
        }

        public Dictionary<string, FieldDeclaration> FieldsDeclarations
        {
            get;
            protected set;
        }

        public Dictionary<string, List<MethodDeclarationAndSymbol>> StaticMethods
        {
            get;
            protected set;
        }

        public Dictionary<string, List<MethodDeclarationAndSymbol>> InstanceMethods
        {
            get;
            protected set;
        }

        public Dictionary<string, List<EntityDeclaration>> StaticProperties
        {
            get;
            protected set;
        }

        public Dictionary<string, List<EntityDeclaration>> InstanceProperties
        {
            get;
            protected set;
        }

        public bool HasStatic
        {
            get
            {
                return this.StaticConfig.HasMembers
                       || this.StaticMethods.Count > 0
                       || this.StaticProperties.Count > 0
                       || this.StaticCtor != null
                       || this.Operators.Count > 0;
            }
        }

        public bool HasRealStatic(IEmitter emitter)
        {
            var result = this.ClassType == ClassType.Struct
                       || this.StaticConfig.HasMembers
                       || this.StaticProperties.Count > 0
                       || this.StaticCtor != null
                       || this.Operators.Count > 0;

            if (result)
            {
                return true;
            }

            if (this.StaticMethods.Any(group =>
            {
                foreach (var declarationAndSymbol in group.Value)
                {
                    var method = declarationAndSymbol.Declaration;
                    if (Helpers.IsEntryPointMethod(emitter, method))
                    {
                        return false;
                    }

                    var init = declarationAndSymbol.Symbol.GetInitPosition();
                    if (init.HasValue && (init.Value == InitPosition.Before || init.Value == InitPosition.Top))
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }))
            {
                return true;
            }

            if (this.Type.GetConstructors().Any(c => c.Parameters.Count == 0 && emitter.GetInline(c) != null))
            {
                return true;
            }

            return false;
        }

        public bool HasRealInstantiable(IEmitter emitter)
        {
            if (this.HasInstantiable)
            {
                return true;
            }

            if (this.StaticMethods.Any(group =>
            {
                return group.Value.Any(method => Helpers.IsEntryPointMethod(emitter, method.Declaration));
            }))
            {
                return true;
            }

            return false;
        }

        public bool HasInstantiable
        {
            get
            {
                return this.InstanceConfig.HasMembers
                       || this.InstanceMethods.Count > 0
                       || this.InstanceProperties.Count > 0
                       || this.Ctors.Count > 0;
            }
        }

        private object enumValue = -1;

        public virtual object LastEnumValue
        {
            get
            {
                return this.enumValue;
            }
            set
            {
                this.enumValue = value;
            }
        }

        public bool IsEnum
        {
            get;
            set;
        }

        public Module Module
        {
            get;
            set;
        }

        public List<IPluginDependency> Dependencies
        {
            get;
            set;
        }

        public ITypeInfo ParentType
        {
            get;
            set;
        }

        public bool IsObjectLiteral
        {
            get;
            set;
        }

        public string FileName
        {
            get;
            set;
        }

        public ITypeDefinition Type
        {
            get;
            set;
        }

        public string JsName
        {
            get;
            set;
        }

        public IType GetBaseClass(IEmitter emitter)
        {
            var types = this.GetBaseTypes(emitter);
            var baseClass = types.FirstOrDefault(t => t.Kind != TypeKind.Interface);
            return baseClass ?? types.First();
        }

        private List<IType> baseTypes;

        public List<IType> GetBaseTypes(IEmitter emitter)
        {
            if (this.baseTypes != null)
            {
                return this.baseTypes;
            }

            return this.baseTypes = new List<IType>(this.Type.DirectBaseTypes
                .Where(t=>!t.GetDefinition().IsNonScriptable() && t.FullName != JS.Types.System.Object.NAME)
                .OrderBy(t => t.Kind)
            );
        }

        public string GetNamespace(IEmitter emitter, bool nons = false)
        {
            if (emitter == null)
            {
                throw new System.ArgumentNullException("emitter");
            }

            var name = this.Namespace;

            var typeName = emitter.GetCustomTypeName(this.Type, false);
            if (typeName != null)
            {
                var i = typeName.LastIndexOf(".");
                name = i >= 0 ? typeName.Substring(0, i) : null;
            }

            if (name == null && !nons)
            {
                name = emitter.Translator.DefaultNamespace;
            }

            return name;
        }

        public override string ToString()
        {
            return this.Type.ToString();
        }

        protected bool Equals(TypeInfo other)
        {
            return Equals(this.Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypeInfo) obj);
        }

        public override int GetHashCode()
        {
            return (this.Type != null ? this.Type.GetHashCode() : 0);
        }
    }
}