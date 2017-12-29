using Bridge.Contract;
using Bridge.Contract.Constants;
using Object.Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator.TypeScript
{
    public class ClassBlock : TypeScriptBlock
    {
        public ClassBlock(IEmitter emitter, ITypeInfo typeInfo)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.TypeInfo = typeInfo;
        }

        public ClassBlock(IEmitter emitter, ITypeInfo typeInfo, IEnumerable<ITypeInfo> nestedTypes, IEnumerable<ITypeInfo> allTypes, string ns)
            : this(emitter, typeInfo)
        {
            this.NestedTypes = nestedTypes;
            this.AllTypes = allTypes;
            this.Namespace = ns;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public bool IsGeneric
        {
            get;
            set;
        }

        public string JsName
        {
            get;
            set;
        }

        public string Namespace
        {
            get;
            set;
        }

        public IEnumerable<ITypeInfo> NestedTypes
        {
            get;
            private set;
        }

        public IEnumerable<ITypeInfo> AllTypes
        {
            get;
            private set;
        }

        public int Position;

        protected override void DoEmit()
        {
            XmlToJsDoc.EmitComment(this, this.Emitter.Translator.EmitNode);

            if (this.TypeInfo.IsEnum && this.TypeInfo.ParentType == null)
            {
                new EnumBlock(this.Emitter, this.TypeInfo, this.Namespace).Emit();
            }
            else
            {
                this.EmitClassHeader();
                this.EmitBlock();
                this.EmitClassEnd();
            }
        }

        protected virtual void EmitClassHeader()
        {
            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.GetCustomTypeName(typeDef, false);
            this.IsGeneric = typeDef.TypeParameterCount > 0;

            if (name.IsEmpty())
            {
                name = this.Emitter.ToTypeScriptName(this.TypeInfo.Type, false, true);

                if (this.IsGeneric)
                {
                    this.DefName = this.Emitter.ToTypeScriptName(this.TypeInfo.Type, true, true);
                }
            }

            if (this.Namespace != null)
            {
                this.Write("export ");
            }
            
            this.Write("interface ");

            this.JsName = name;
            this.Write(this.JsName);

            string extend = this.GetTypeHierarchy();

            if (extend.IsNotEmpty() && !this.TypeInfo.IsEnum)
            {
                this.Write(" extends ");
                this.Write(extend);
            }

            this.WriteSpace();
            this.BeginBlock();
            this.Position = this.Emitter.Output.Length;
        }

        public string DefName { get; set; }

        private string GetTypeHierarchy()
        {
            StringBuilder sb = new StringBuilder();

            var list = new List<string>();

            foreach (var t in this.TypeInfo.GetBaseTypes(this.Emitter))
            {
                var name = this.Emitter.ToTypeScriptName(t);

                list.Add(name);
            }

            if (list.Count > 0 && list[0] == JS.Types.System.Object.NAME)
            {
                list.RemoveAt(0);
            }

            if (list.Count == 0)
            {
                return "";
            }

            bool needComma = false;

            foreach (var item in list)
            {
                if (needComma)
                {
                    sb.Append(",");
                }

                needComma = true;
                sb.Append(item);
            }

            return sb.ToString();
        }

        protected virtual void EmitBlock()
        {
            var typeDef = this.Emitter.GetTypeDefinition();

            new MemberBlock(this.Emitter, this.TypeInfo, false).Emit();
            if (this.Emitter.TypeInfo.TypeDeclaration.ClassType != ICSharpCode.NRefactory.CSharp.ClassType.Interface)
            {
                if (this.Position != this.Emitter.Output.Length && !this.Emitter.IsNewLine)
                {
                    this.WriteNewLine();
                }

                this.EndBlock();

                this.WriteNewLine();

                if (this.Namespace != null)
                {
                    this.Write("export ");
                }

                this.Write("interface ");

                this.Write(this.DefName ?? this.JsName);

                this.Write("Func extends Function ");

                if (this.IsGeneric)
                {
                    this.BeginBlock();
                    this.Write("<");
                    var comma = false;
                    foreach (var p in typeDef.TypeParameters)
                    {
                        if (comma)
                        {
                            this.WriteComma();
                        }
                        this.Write(p.Name);
                        comma = true;
                    }
                    this.Write(">");

                    this.WriteOpenParentheses();
                    comma = false;
                    foreach (var p in typeDef.TypeParameters)
                    {
                        if (comma)
                        {
                            this.WriteComma();
                        }
                        this.Write(JS.Vars.D + p.Name);
                        this.WriteColon();
                        this.Write(JS.Types.TypeRef);
                        this.Write("<");
                        this.Write(p.Name);
                        this.Write(">");
                        comma = true;
                    }

                    this.WriteCloseParentheses();
                    this.WriteColon();
                }

                this.BeginBlock();

                this.Write(JS.Fields.PROTOTYPE + ": ");
                this.Write(this.JsName);
                this.WriteSemiColon();
                this.WriteNewLine();
                this.WriteNestedDefs();
                this.Position = this.Emitter.Output.Length;

                if (this.Emitter.TypeInfo.TypeDeclaration.ClassType != ICSharpCode.NRefactory.CSharp.ClassType.Interface)
                {
                    if (!this.TypeInfo.IsEnum)
                    {
                        new ConstructorBlock(this.Emitter, this.TypeInfo).Emit();
                    }
                    new MemberBlock(this.Emitter, this.TypeInfo, true).Emit();
                }
            }
        }

        protected virtual void WriteNestedDefs()
        {
            if (this.NestedTypes != null)
            {
                foreach (var nestedType in this.NestedTypes)
                {
                    var typeDef = nestedType.Type;

                    if (typeDef.Kind == TypeKind.Interface || typeDef.IsObjectLiteral())
                    {
                        continue;
                    }

                    string customName = this.Emitter.GetCustomTypeName(typeDef, false);
                    string defName = customName;

                    if (defName.IsEmpty())
                    {
                        defName = this.Emitter.ToTypeScriptName(nestedType.Type, true);
                        this.Write(this.Emitter.ToTypeScriptName(nestedType.Type, true, true));
                    }
                    else
                    {
                        this.Write(defName);
                    }

                    if (typeDef.Kind == TypeKind.Enum)
                    {
                        var parentTypeDef = this.Emitter.GetTypeDefinition();
                        string parentName = this.Emitter.GetCustomTypeName(parentTypeDef, false);
                        if (parentName.IsEmpty())
                        {
                            parentName = this.TypeInfo.Type.Name;
                        }
                        defName = parentName + "." + this.Emitter.ToTypeScriptName(nestedType.Type, false, true);
                    }

                    this.WriteColon();

                    this.Write(defName + "Func");
                    this.WriteSemiColon();
                    this.WriteNewLine();
                }
            }
        }

        protected virtual void EmitClassEnd()
        {
            if (this.Position != this.Emitter.Output.Length && !this.Emitter.IsNewLine)
            {
                this.WriteNewLine();
            }

            var isInterface = this.Emitter.TypeInfo.TypeDeclaration.ClassType == ICSharpCode.NRefactory.CSharp.ClassType.Interface;
            this.EndBlock();

            if (this.IsGeneric && !isInterface)
            {
                this.WriteNewLine();
                this.EndBlock();
            }
            
            if (this.TypeInfo.ParentType == null && !isInterface)
            {
                string name = this.Emitter.ToTypeScriptName(this.TypeInfo.Type, true, true);
                this.WriteNewLine();

                if (this.Namespace == null)
                {
                    this.Write("declare ");
                }

                this.Write("var ");
                this.Write(name);
                this.WriteColon();

                this.Write(name + "Func");

                this.WriteSemiColon();
            }

            this.WriteNestedTypes();
        }

        protected virtual void WriteNestedTypes()
        {
            if (this.NestedTypes != null && this.NestedTypes.Any())
            {
                if (!this.Emitter.IsNewLine)
                {
                    this.WriteNewLine();
                }

                var typeDef = this.Emitter.GetTypeDefinition();
                string name = this.Emitter.GetCustomTypeName(typeDef, false);
                if (name.IsEmpty())
                {
                    name = this.Emitter.ToJsName(this.TypeInfo.Type, true, true, nomodule: true);
                }

                this.Write("module ");
                this.Write(name);
                this.WriteSpace();
                this.BeginBlock();

                var last = this.NestedTypes.LastOrDefault();
                foreach (var nestedType in this.NestedTypes)
                {
                    this.Emitter.Translator.EmitNode = nestedType.TypeDeclaration;

                    if (nestedType.IsObjectLiteral)
                    {
                        continue;
                    }

                    this.Emitter.TypeInfo = nestedType;

                    var nestedTypes = this.AllTypes.Where(t => t.ParentType == nestedType);
                    new ClassBlock(this.Emitter, this.Emitter.TypeInfo, nestedTypes, this.AllTypes, this.Namespace).Emit();
                    this.WriteNewLine();
                    if (nestedType != last)
                    {
                        this.WriteNewLine();
                    }
                }

                this.EndBlock();
            }
        }
    }
}