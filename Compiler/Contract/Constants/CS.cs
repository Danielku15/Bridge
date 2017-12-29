namespace Bridge.Contract.Constants
{
    public class CS
    {
        public class NS
        {
            public const string GLOBAL = "global::";
            public const string BRIDGE = "Bridge";
            public const string SYSTEM = "System";
        }

        public class Bridge
        {
            public const string DOTNAME = NS.BRIDGE + ".";
        }

        public class System
        {
            public const string DOTNAME = NS.SYSTEM + ".";
        }

        public class Attributes
        {
            public const string COMPILER_GENERATED_NAME = System.DOTNAME + "Runtime.CompilerServices.CompilerGeneratedAttribute";
            public const string FLAGS = System.DOTNAME + "FlagsAttribute";
            public const string ATTRIBUTE_USAGE = System.DOTNAME + "AttributeUsageAttribute";
            public const string CONDITIONAL = System.DOTNAME + "Diagnostics.ConditionalAttribute";
            public const string ASSEMBLY_DESCRIPTION = System.DOTNAME + "Reflection.AssemblyDescriptionAttribute";
            public const string ASSEMBLY_TITLE = System.DOTNAME + "Reflection.AssemblyTitleAttribute";
            public const string STRUCT_LAYOUT = System.DOTNAME + "Runtime.InteropServices.StructLayoutAttribute";
            public const string SERIALIZABLE = System.DOTNAME + "SerializableAttribute";

            public const string ACCESSORS_INDEXER = Bridge.DOTNAME + "AccessorsIndexerAttribute";
            public const string READY = Bridge.DOTNAME + "Html5.ReadyAttribute";
            public const string ENUM = Bridge.DOTNAME + "EnumAttribute";
            public const string NAME = Bridge.DOTNAME + "NameAttribute";
            public const string EXTERNAL = Bridge.DOTNAME + "ExternalAttribute";
            public const string VIRTUAL = Bridge.DOTNAME + "VirtualAttribute";
            public const string OBJECT_LITERAL = Bridge.DOTNAME + "ObjectLiteralAttribute";
            public const string GLOBAL_METHODS = Bridge.DOTNAME + "GlobalMethodsAttribute";
            public const string NON_SCRIPTABLE = Bridge.DOTNAME + "NonScriptableAttribute";
            public const string MIXIN = Bridge.DOTNAME + "MixinAttribute";
            public const string REFLECTABLE = Bridge.DOTNAME + "ReflectableAttribute";
            public const string MODULE = Bridge.DOTNAME + "ModuleAttribute";
            public const string MODULE_DEPENDENCY = Bridge.DOTNAME + "ModuleDependencyAttribute";
            public const string IGNORE_GENERIC = Bridge.DOTNAME + "IgnoreGenericAttribute";
            public const string CONSTRUCTOR = Bridge.DOTNAME + "ConstructorAttribute";
            public const string IGNORE_CAST = Bridge.DOTNAME + "IgnoreCastAttribute";
            public const string SCRIPT = Bridge.DOTNAME + "ScriptAttribute";
            public const string CONVENTION = Bridge.DOTNAME + "ConventionAttribute";
            public const string RULES = Bridge.DOTNAME + "RulesAttribute";
            public const string EXPAND_PARAMS = Bridge.DOTNAME + "ExpandParamsAttribute";
            public const string IGNORE = Bridge.DOTNAME + "IgnoreAttribute";
            public const string INIT = Bridge.DOTNAME + "InitAttribute";
            public const string ADAPTER = Bridge.DOTNAME + "AdapterAttribute";
            public const string CAST = Bridge.DOTNAME + "CastAttribute";
            public const string TEMPLATE = Bridge.DOTNAME + "TemplateAttribute";
            public const string TEMPLATE_PROPERTY_FN = "Fn";
            public const string UNBOX = Bridge.DOTNAME + "UnboxAttribute";
            public const string INLINE_CONST = Bridge.DOTNAME + "InlineConstAttribute";
            public const string FILENAME = Bridge.DOTNAME + "FileNameAttribute";
            public const string NAMESPACE = Bridge.DOTNAME + "NamespaceAttribute";
            public const string GLOBAL_TARGET = Bridge.DOTNAME + "GlobalTargetAttribute";
            public const string PRIORITY = Bridge.DOTNAME + "PriorityAttribute";
            public const string EXTERNAL_INTERFACE = Bridge.DOTNAME + "ExternalInterfaceAttribute";
            public const string IMMUTABLE = Bridge.DOTNAME + "ImmutableAttribute";
            public const string FIELD = Bridge.DOTNAME + "FieldAttribute";
            public const string COMPILER_EXTENSION = Bridge.DOTNAME + "CompilerExtensionAttribute";
            public const string OUTPUT = Bridge.DOTNAME + "OutputAttribute";
            public const string OUTPUT_BY = Bridge.DOTNAME + "OutputByAttribute";
            
        }

        public class Methods
        {
            public const string AUTO_STARTUP_METHOD_NAME = "Main";
            public const string EQUALS = "Equals";
            public const string GETHASHCODE = "GetHashCode";
            public const string TOSTRING = "ToString";
        }

        public class Ops
        {
            public const string CAST = "cast";
            public const string AS = "as";
            public const string IS = "is";
        }

        public class Types
        {
            public const string System_Byte = CS.System.DOTNAME + "Byte";
            public const string System_SByte = System.DOTNAME + "SByte";
            public const string System_Int16 = System.DOTNAME + "Int16";
            public const string System_UInt16 = System.DOTNAME + "UInt16";
            public const string System_Int32 = System.DOTNAME + "Int32";
            public const string System_UInt32 = System.DOTNAME + "UInt32";
            public const string System_Single = System.DOTNAME + "Single";
            public const string System_Double = System.DOTNAME + "Double";
            public const string System_Exception = System.DOTNAME + "Exception";

            public const string Bridge_ObjectCreateMode = Bridge.DOTNAME + "ObjectCreateMode";
            public const string Bridge_ObjectInitializationMode = Bridge.DOTNAME + "ObjectInitializationMode";
            public const string Bridge_TypeAccessibility = Bridge.DOTNAME + "TypeAccessibility";

        }

        public class Wrappers
        {
            public const string CONSTRUCTORWRAPPER = "$ctorWrapper";

            public class Params
            {
                public const string BODY = "{body}";
            }
        }
    }
}