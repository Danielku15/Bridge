using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Object.Net.Utilities;
using System.Linq;
using ICSharpCode.NRefactory.Semantics;

namespace Bridge.Translator.TypeScript
{
    public class EnumBlock : TypeScriptBlock
    {
        public EnumBlock(IEmitter emitter, ITypeInfo typeInfo, string ns)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.TypeInfo = typeInfo;
            this.Namespace = ns;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public string Namespace
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.GetCustomTypeName(typeDef, false);

            if (name.IsEmpty())
            {
                name = this.Emitter.ToTypeScriptName(this.TypeInfo.Type, false, true);
            }

            if (this.Namespace != null)
            {
                this.Write("export ");
            }

            this.Write("enum ");
            this.Write(name);

            this.WriteSpace();
            this.BeginBlock();

            if (this.TypeInfo.StaticConfig.Fields.Count > 0)
            {
                var lastField = this.TypeInfo.StaticConfig.Fields.Last();
                foreach (var field in this.TypeInfo.StaticConfig.Fields)
                {
                    this.Write(EnumBlock.GetEnumItemName(this.Emitter, field));

                    var initializer = field.Initializer;
                    if (initializer != null && initializer is PrimitiveExpression)
                    {
                        this.Write(" = ");
                        if (Helpers.IsStringNameEnum(this.TypeInfo.Type))
                        {
                            this.WriteScript(((PrimitiveExpression)initializer).Value);
                        }
                        else
                        {
                            this.Write(((PrimitiveExpression)initializer).Value);
                        }
                        
                    }

                    if (field != lastField)
                    {
                        this.Write(",");
                    }

                    this.WriteNewLine();
                }
            }

            this.EndBlock();
        }

        public static string GetEnumItemName(IEmitter emitter, TypeConfigItem field)
        {
            var memeber_rr = (MemberResolveResult)emitter.Resolver.ResolveNode(field.Entity);
            var mname = emitter.GetEntityName(memeber_rr.Member);
            return mname;
        }
    }
}