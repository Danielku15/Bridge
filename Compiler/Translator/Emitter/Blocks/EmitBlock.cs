using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object.Net.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Bridge.Translator
{
    public class EmitBlock : AbstractEmitterBlock
    {
        protected FileHelper FileHelper
        {
            get; set;
        }

        public EmitBlock(IEmitter emitter)
            : base(emitter, null)
        {
            this.Emitter = emitter;
            this.FileHelper = new FileHelper();
        }

        protected virtual StringBuilder GetOutputForType(ITypeInfo typeInfo, string name, bool isMeta = false)
        {
            Module module = null;

            if (typeInfo != null && typeInfo.Module != null)
            {
                module = typeInfo.Module;
            }
            else if (this.Emitter.AssemblyInfo.Module != null)
            {
                module = this.Emitter.AssemblyInfo.Module;
            }

            var fileName = typeInfo != null ? typeInfo.FileName : name;

            if (fileName.IsEmpty() && this.Emitter.AssemblyInfo.OutputBy != OutputBy.Project)
            {
                switch (this.Emitter.AssemblyInfo.OutputBy)
                {
                    case OutputBy.ClassPath:
                        fileName = typeInfo.Type.FullName;
                        break;

                    case OutputBy.Class:
                        fileName = this.GetIteractiveClassPath(typeInfo);
                        break;

                    case OutputBy.Module:
                        fileName = module != null ? module.Name : null;
                        break;

                    case OutputBy.NamespacePath:
                    case OutputBy.Namespace:
                        fileName = typeInfo.GetNamespace(this.Emitter);
                        break;

                    default:
                        break;
                }

                var isPathRelated = this.Emitter.AssemblyInfo.OutputBy == OutputBy.ClassPath ||
                                    this.Emitter.AssemblyInfo.OutputBy == OutputBy.NamespacePath;

                if (fileName.IsNotEmpty() && isPathRelated)
                {
                    fileName = fileName.Replace('.', System.IO.Path.DirectorySeparatorChar);

                    if (this.Emitter.AssemblyInfo.StartIndexInName > 0)
                    {
                        fileName = fileName.Substring(this.Emitter.AssemblyInfo.StartIndexInName);
                    }
                }
            }

            if (fileName.IsEmpty() && this.Emitter.AssemblyInfo.FileName != null)
            {
                fileName = this.Emitter.AssemblyInfo.FileName;
            }

            if (fileName.IsEmpty() && this.Emitter.Translator.ProjectProperties.AssemblyName != null)
            {
                fileName = this.Emitter.Translator.ProjectProperties.AssemblyName;
            }

            if (fileName.IsEmpty())
            {
                fileName = AssemblyInfo.DEFAULT_FILENAME;
            }

            // Apply lowerCamelCase to filename if set up in bridge.json (or left default)
            if (this.Emitter.AssemblyInfo.FileNameCasing == FileNameCaseConvert.CamelCase)
            {
                var sepList = new string[] { ".", System.IO.Path.DirectorySeparatorChar.ToString(), "\\", "/" };

                // Populate list only with needed separators, as usually we will never have all four of them
                var neededSepList = new List<string>();

                foreach (var separator in sepList)
                {
                    if (fileName.Contains(separator.ToString()) && !neededSepList.Contains(separator))
                    {
                        neededSepList.Add(separator);
                    }
                }

                // now, separating the filename string only by the used separators, apply lowerCamelCase
                if (neededSepList.Count > 0)
                {
                    foreach (var separator in neededSepList)
                    {
                        var stringList = new List<string>();

                        foreach (var str in fileName.Split(separator[0]))
                        {
                            stringList.Add(str.ToLowerCamelCase());
                        }

                        fileName = stringList.Join(separator);
                    }
                }
                else
                {
                    fileName = fileName.ToLowerCamelCase();
                }
            }

            // Append '.js' extension to file name at translator.Outputs level: this aids in code grouping on files
            // when filesystem is not case sensitive.
            if (!FileHelper.IsJS(fileName))
            {
                fileName += Contract.Constants.Files.Extensions.JS;
            }

            switch (this.Emitter.AssemblyInfo.FileNameCasing)
            {
                case FileNameCaseConvert.Lowercase:
                    fileName = fileName.ToLower();
                    break;

                default:
                    var lcFileName = fileName.ToLower();

                    // Find a file name that matches (case-insensitive) and use it as file name (if found)
                    // The used file name will use the same casing of the existing one.
                    foreach (var existingFile in this.Emitter.Outputs.Keys)
                    {
                        if (lcFileName == existingFile.ToLower())
                        {
                            fileName = existingFile;
                        }
                    }

                    break;
            }

            IEmitterOutput output = null;

            if (this.Emitter.Outputs.ContainsKey(fileName))
            {
                output = this.Emitter.Outputs[fileName];
            }
            else
            {
                output = new EmitterOutput(fileName) { IsMetadata = isMeta };
                this.Emitter.Outputs.Add(fileName, output);
            }

            this.Emitter.EmitterOutput = output;

            if (module == null)
            {
                if (output.NonModuleDependencies == null)
                {
                    output.NonModuleDependencies = new List<IPluginDependency>();
                }
                this.Emitter.CurrentDependencies = output.NonModuleDependencies;
                return output.NonModuletOutput;
            }

            if (module.Name == "")
            {
                module.Name = Bridge.Translator.AssemblyInfo.DEFAULT_FILENAME;
            }

            if (output.ModuleOutput.ContainsKey(module))
            {
                this.Emitter.CurrentDependencies = output.ModuleDependencies[module.Name];
                return output.ModuleOutput[module];
            }

            StringBuilder moduleOutput = new StringBuilder();
            output.ModuleOutput.Add(module, moduleOutput);
            var dependencies = new List<IPluginDependency>();
            output.ModuleDependencies.Add(module.Name, dependencies);

            if (typeInfo != null && typeInfo.Dependencies.Count > 0)
            {
                dependencies.AddRange(typeInfo.Dependencies);
            }

            this.Emitter.CurrentDependencies = dependencies;

            return moduleOutput;
        }

        /// <summary>
        /// Gets class path iterating until its root class, writing something like this on a 3-level nested class:
        /// RootClass.Lv1ParentClass.Lv2ParentClass.Lv3NestedClass
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <returns></returns>
        private string GetIteractiveClassPath(ITypeInfo typeInfo)
        {
            var fullClassName = typeInfo.Name;
            var maxIterations = 100;
            var curIterations = 0;
            var parent = typeInfo.ParentType;

            while (parent != null && curIterations++ < maxIterations)
            {
                fullClassName = parent.Name + "." + fullClassName;
                parent = parent.ParentType;
            }

            // This should never happen but, just to be sure...
            if (curIterations >= maxIterations)
            {
                throw new EmitterException(typeInfo.TypeDeclaration, "Iteration count for class '" + typeInfo.Type.FullName + "' exceeded " +
                    maxIterations + " depth iterations until root class!");
            }

            return fullClassName;
        }

        protected override void DoEmit()
        {
            this.Emitter.Tag = "JS";
            this.Emitter.Writers = new Stack<IWriter>();
            this.Emitter.Outputs = new EmitterOutputs();
            var metas = new Dictionary<int, Dictionary<ITypeInfo, string>>();
            Action<ITypeInfo, JObject> addMeta = (type, metaData) =>
            {
                string typeArgs = "";
                if (type.Type.TypeArguments.Count > 0 && !type.Type.IsIgnoreGeneric())
                {
                    StringBuilder arr_sb = new StringBuilder();
                    var comma = false;
                    foreach (var typeArgument in type.Type.TypeArguments)
                    {
                        if (comma)
                        {
                            arr_sb.Append(", ");
                        }

                        arr_sb.Append(typeArgument.Name);
                        comma = true;
                    }

                    typeArgs = arr_sb.ToString();
                }

                int? namespaceKey;
                var meta = string.Format("$m({0}, function ({2}) {{ return {1}; }});", MetadataUtils.GetTypeName(type.Type, this.Emitter, false, out namespaceKey, true), metaData.ToString(Formatting.None), typeArgs);

                if (!namespaceKey.HasValue)
                {
                    namespaceKey = int.MinValue;
                }

                Dictionary<ITypeInfo, string> perTypeMeta;
                if (!metas.TryGetValue(namespaceKey.Value, out perTypeMeta))
                {
                    metas[namespaceKey.Value] = perTypeMeta = new Dictionary<ITypeInfo, string>();
                }

                perTypeMeta.Add(type, meta);
            };

            var outputTypes = this.Emitter.Translator.Types.OutputTypes;
            this.Emitter.Translator.Plugins.BeforeTypesEmit(this.Emitter.Translator, outputTypes);
            this.Emitter.ReflectableTypes = this.GetReflectableTypes();
            var reflectedTypes = this.Emitter.ReflectableTypes;
            var tmpBuffer = new StringBuilder();
            StringBuilder currentOutput = null;
            this.Emitter.NamedBoxedFunctions = new Dictionary<IType, Dictionary<string, string>>();

            foreach (var type in outputTypes)
            {
                this.Emitter.Translator.Plugins.BeforeTypeEmit(this.Emitter.Translator, type);

                this.Emitter.Translator.EmitNode = type.TypeDeclaration;
                var typeDef = type.Type.GetDefinition();
                this.Emitter.Rules = Rules.Get(this.Emitter, typeDef);

                bool isNative;
                if (typeDef.Kind == TypeKind.Interface && typeDef.IsExternalInterface(out isNative))
                {
                    this.Emitter.Translator.Plugins.AfterTypeEmit(this.Emitter.Translator, type);
                    continue;
                }

                if (type.IsObjectLiteral)
                {
                    var mode = type.Type.GetDefinition().GetObjectCreateMode();
                    var ignore = mode == 0 && !type.Type.GetMethods(null, GetMemberOptions.IgnoreInheritedMembers).Any(m => !m.IsConstructor && !m.IsAccessor);

                    if (typeDef.IsExternal() || ignore)
                    {
                        this.Emitter.Translator.Plugins.AfterTypeEmit(this.Emitter.Translator, type);
                        continue;
                    }
                }

                this.Emitter.InitEmitter();

                this.Emitter.SourceFileName = type.TypeDeclaration.GetParent<SyntaxTree>().FileName;
                this.Emitter.SourceFileNameIndex = this.Emitter.Translator.SourceFiles.IndexOf(this.Emitter.SourceFileName);
                this.Emitter.Output = this.GetOutputForType(type, null);
                this.Emitter.TypeInfo = type;
                type.JsName = this.Emitter.ToJsName(type.Type, true, removeScope: false);

                if (this.Emitter.Output.Length > 0)
                {
                    this.WriteNewLine();
                }

                tmpBuffer.Length = 0;
                currentOutput = this.Emitter.Output;
                this.Emitter.Output = tmpBuffer;

                if (this.Emitter.TypeInfo.Module != null)
                {
                    this.Indent();
                }

                var name = this.Emitter.ToJsName(type.Type, true, true, true);
                if (type.Type.DeclaringType != null && JS.Reserved.StaticNames.Any(n => String.Equals(name, n, StringComparison.InvariantCulture)))
                {
                    throw new EmitterException(type.TypeDeclaration, "Nested class cannot have such name: " + name + ". Please rename it.");
                }

                new ClassBlock(this.Emitter, this.Emitter.TypeInfo).Emit();
                this.Emitter.Translator.Plugins.AfterTypeEmit(this.Emitter.Translator, type);

                currentOutput.Append(tmpBuffer.ToString());
                this.Emitter.Output = currentOutput;
            }

            this.Emitter.DisableDependencyTracking = true;
            this.EmitNamedBoxedFunctions();

            this.Emitter.NamespacesCache = new Dictionary<string, int>();
            foreach (var type in outputTypes)
            {
                var typeDef = type.Type;
                bool isGlobal = false;
                if (typeDef != null)
                {
                    isGlobal = typeDef.HasGlobalMethodsAttribute().HasValue || typeDef.GetMixin() != null;
                }

                if (typeDef.FullName != "System.Object")
                {
                    var name = this.Emitter.ToJsName(typeDef);

                    if (name == "Object")
                    {
                        continue;
                    }
                }

                var isObjectLiteral = typeDef.IsObjectLiteral();
                var isPlainMode = isObjectLiteral && typeDef.GetObjectCreateMode() == 0;

                if (isPlainMode)
                {
                    continue;
                }

                if (isGlobal || this.Emitter.TypeInfo.Module != null || reflectedTypes.Any(t => t == type))
                {
                    continue;
                }

                var meta = MetadataUtils.ConstructTypeMetadata(typeDef, this.Emitter, true, type.TypeDeclaration.GetParent<SyntaxTree>());

                if (meta != null)
                {
                    addMeta(type, meta);
                }
            }

            foreach (var reflectedType in reflectedTypes)
            {
                JObject meta = null;
                SyntaxTree tree = null;

                if (reflectedType.TypeDeclaration != null)
                {
                    tree = reflectedType.TypeDeclaration.GetParent<SyntaxTree>();
                }

                if (reflectedType != null && reflectedType.Module != null)
                {
                    continue;
                }

                meta = MetadataUtils.ConstructTypeMetadata(reflectedType.Type, this.Emitter, false, tree);
               

                if (meta != null)
                {
                    addMeta(reflectedType, meta);
                }
            }

            var lastOutput = this.Emitter.Output;
            var output = this.Emitter.AssemblyInfo.Reflection.Output;

            if (this.Emitter.AssemblyInfo.Reflection.Target == MetadataTarget.File)
            {
                if (string.IsNullOrEmpty(output))
                {
                    if (!string.IsNullOrWhiteSpace(this.Emitter.AssemblyInfo.FileName) &&
                        this.Emitter.AssemblyInfo.FileName != AssemblyInfo.DEFAULT_FILENAME)
                    {
                        output = System.IO.Path.GetFileNameWithoutExtension(this.Emitter.AssemblyInfo.FileName) + ".meta.js";
                    }
                    else
                    {
                        output = this.Emitter.Translator.ProjectProperties.AssemblyName + ".meta.js";
                    }
                }

                this.Emitter.Output = this.GetOutputForType(null, output, true);
                this.Emitter.MetaDataOutputName = this.Emitter.EmitterOutput.FileName;
            }
            var scriptableAttributes = this.Emitter.Resolver.Compilation.MainAssembly.GetScriptableAttributes().ToArray();
            bool hasMeta = metas.Count > 0 || scriptableAttributes.Any();

            if (hasMeta)
            {
                this.WriteNewLine();
                if (metas.Count > 0)
                {
                    this.Write("var $m = " + JS.Types.Bridge.SET_METADATA + ",");
                    this.WriteNewLine();
                    this.Write(Bridge.Translator.Emitter.INDENT + "$n = ");

                    var sorted = this.Emitter.NamespacesCache.OrderBy(key => key.Value).ToArray();
                    Write(sorted.Select(item => new JRaw(item.Key)).ToArray().ToJavaScript());
                    this.Write(";");
                    this.WriteNewLine();

                    foreach (var ns in sorted)
                    {
                        Dictionary<ITypeInfo, string> nsMeta;
                        if (metas.TryGetValue(ns.Value, out nsMeta))
                        {
                            foreach (var meta in nsMeta)
                            {
                                this.Write(meta.Value);
                                this.WriteNewLine();
                            }
                            metas.Remove(ns.Value);
                        }
                    }

                    foreach (var nsMeta in metas.Values)
                    {
                        foreach (var meta in nsMeta)
                        {
                            this.Write(meta.Value);
                            this.WriteNewLine();
                        }
                    }

                    this.Emitter.NamespacesCache = null;
                }

                if (scriptableAttributes.Any())
                {
                    JArray attrArr = new JArray();
                    foreach (var a in scriptableAttributes)
                    {
                        attrArr.Add(MetadataUtils.ConstructAttribute(a, null, this.Emitter));
                    }

                    this.Write(string.Format("$asm.attr= {0};", attrArr.ToString(Formatting.None)));
                    this.WriteNewLine();
                }
            }

            this.Emitter.Output = lastOutput;

            //this.RemovePenultimateEmptyLines(true);

            this.Emitter.Translator.Plugins.AfterTypesEmit(this.Emitter.Translator, outputTypes);
        }

        protected virtual void EmitNamedBoxedFunctions()
        {
            if (this.Emitter.NamedBoxedFunctions.Count > 0)
            {
                this.Emitter.Comma = false;

                this.WriteNewLine();
                this.Write("var " + JS.Vars.DBOX_ + " = { };");
                this.WriteNewLine();

                foreach (var boxedFunction in this.Emitter.NamedBoxedFunctions)
                {
                    var name = this.Emitter.ToJsName(boxedFunction.Key, true);

                    this.WriteNewLine();
                    this.Write(JS.Funcs.BRIDGE_NS);
                    this.WriteOpenParentheses();
                    this.WriteScript(name);
                    this.Write(", " + JS.Vars.DBOX_ + ")");
                    this.WriteSemiColon();

                    this.WriteNewLine();
                    this.WriteNewLine();
                    this.Write(JS.Types.Bridge.APPLY + "(" + JS.Vars.DBOX_ + ".");
                    this.Write(name);
                    this.Write(", ");
                    this.BeginBlock();

                    this.Emitter.Comma = false;
                    foreach (KeyValuePair<string, string> namedFunction in boxedFunction.Value)
                    {
                        this.EnsureComma();
                        this.Write(namedFunction.Key.ToLowerCamelCase() + ": " + namedFunction.Value);
                        this.Emitter.Comma = true;
                    }

                    this.WriteNewLine();
                    this.EndBlock();
                    this.WriteCloseParentheses();
                    this.WriteSemiColon();
                    this.WriteNewLine();
                }
            }
        }

        private bool SkipFromReflection(ITypeDefinition typeDef)
        {
            var isObjectLiteral = typeDef.IsObjectLiteral();
            var isPlainMode = isObjectLiteral && typeDef.GetObjectCreateMode() == 0;

            if (isPlainMode)
            {
                return true;
            }

            var skip = typeDef.HasGlobalMethodsAttribute().HasValue
                       || typeDef.IsNonScriptable()
                       || typeDef.GetMixin() != null;

            if (!skip && typeDef.FullName != "System.Object")
            {
                var name = this.Emitter.ToJsName(typeDef);

                if (name == "Object" || name == "System.Object" || name == "Function")
                {
                    return true;
                }
            }

            return skip;
        }

        public ITypeInfo[] GetReflectableTypes()
        {
            var config = this.Emitter.AssemblyInfo.Reflection;
            //bool? enable = config.Disabled.HasValue ? !config.Disabled : (configInternal.Disabled.HasValue ? !configInternal.Disabled : true);
            bool? enable = null;
            if (config.Disabled.HasValue)
            {
                enable = !config.Disabled.Value;
            }
            else if (!config.Disabled.HasValue)
            {
                enable = true;
            }

            TypeAccessibility? typeAccessibility = config.TypeAccessibility.HasValue ? config.TypeAccessibility : null;
            string filter = !string.IsNullOrEmpty(config.Filter) ? config.Filter : null;

            var hasSettings = !string.IsNullOrEmpty(config.Filter) ||
                              config.MemberAccessibility != null ||
                              config.TypeAccessibility.HasValue;

            if (enable.HasValue && !enable.Value)
            {
                return new ITypeInfo[0];
            }

            if (enable.HasValue && enable.Value && !hasSettings)
            {
                this.Emitter.IsAnonymousReflectable = true;
            }

            if (typeAccessibility.HasValue)
            {
                this.Emitter.IsAnonymousReflectable = typeAccessibility.Value.HasFlag(TypeAccessibility.Anonymous);
            }

            List<ITypeInfo> reflectTypes = new List<ITypeInfo>();
            foreach (var bridgeType in this.Emitter.Translator.Types.AllTypes)
            {
                var result = false;
                var type = bridgeType.Type;
                var typeDef = type.GetDefinition();
                //var thisAssembly = bridgeType.Value.TypeInfo != null;
                var thisAssembly = bridgeType.Type.ParentAssembly.FullAssemblyName
                    == this.Emitter.Resolver.Compilation.MainAssembly.FullAssemblyName;
                var external = typeDef != null && typeDef.IsExternal();

                if (enable.HasValue && enable.Value && !hasSettings && thisAssembly)
                {
                    result = true;
                }

                if (typeDef != null)
                {
                    var skip = this.SkipFromReflection(typeDef);

                    if (skip)
                    {
                        continue;
                    }

                    if (typeDef.IsReflectable(false, null) && thisAssembly)
                    {
                        reflectTypes.Add(bridgeType);
                        continue;
                    }

                    if (external)
                    {
                        if (!string.IsNullOrWhiteSpace(filter) && EmitBlock.MatchFilter(type, filter, thisAssembly, result))
                        {
                            reflectTypes.Add(bridgeType);
                        }
                        continue;
                    }
                }

                if (typeAccessibility.HasValue && thisAssembly)
                {
                    result = false;

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.All))
                    {
                        result = true;
                    }

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.Anonymous) && type.Kind == TypeKind.Anonymous)
                    {
                        result = true;
                    }

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.NonAnonymous) && type.Kind != TypeKind.Anonymous)
                    {
                        result = true;
                    }

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.NonPrivate) && (typeDef == null || !typeDef.IsPrivate))
                    {
                        result = true;
                    }

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.Public) && (typeDef == null || typeDef.IsPublic || typeDef.IsInternal))
                    {
                        result = true;
                    }

                    if (typeAccessibility.Value.HasFlag(TypeAccessibility.None))
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(filter))
                {
                    result = EmitBlock.MatchFilter(type, filter, thisAssembly, result);

                    if (!result)
                    {
                        continue;
                    }
                }

                if (result)
                {
                    reflectTypes.Add(bridgeType);
                }
            }

            return reflectTypes.ToArray();
        }

        private static bool MatchFilter(IType type, string filters, bool thisAssembly, bool def)
        {
            var fullName = type.FullName;
            var parts = filters.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = def;

            foreach (var part in parts)
            {
                string pattern;
                string filter = part;
                bool exclude = filter.StartsWith("!");

                if (exclude)
                {
                    filter = filter.Substring(1);
                }

                if (filter == "this")
                {
                    result = !exclude && thisAssembly;
                }
                else
                {
                    if (filter.StartsWith("regex:"))
                    {
                        pattern = filter.Substring(6);
                    }
                    else
                    {
                        pattern = "^" + Regex.Escape(filter).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    }

                    if (Regex.IsMatch(fullName, pattern))
                    {
                        result = !exclude;
                    }
                }
            }
            return result;
        }
    }
}