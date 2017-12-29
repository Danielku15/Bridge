using Bridge.Contract.Constants;
using Object.Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bridge.Contract;

namespace Bridge.Translator
{
    public partial class Emitter
    {
        protected virtual void WrapToModules()
        {
            this.Log.Trace("Wrapping to modules...");

            foreach (var outputPair in this.Outputs)
            {
                var output = outputPair.Value;

                foreach (var moduleOutputPair in output.ModuleOutput)
                {
                    var module = moduleOutputPair.Key;
                    var moduleOutput = moduleOutputPair.Value;

                    this.Log.Trace("Module " + module.Name + " ...");

                    AbstractEmitterBlock.RemovePenultimateEmptyLines(moduleOutput, true);

                    switch (module.Type)
                    {
                        case ModuleType.CommonJS:
                            this.WrapToCommonJS(moduleOutput, module, output);
                            break;
                        case ModuleType.UMD:
                            this.WrapToUMD(moduleOutput, module, output);
                            break;
                        case ModuleType.ES6:
                            this.WrapToES6(moduleOutput, module, output);
                            break;
                        case ModuleType.AMD:
                        default:
                            this.WrapToAMD(moduleOutput, module, output);
                            break;
                    }


                }
            }

            this.Log.Trace("Wrapping to modules done");
        }

        protected virtual void WrapToAMD(StringBuilder moduleOutput, Module module, IEmitterOutput output)
        {
            var str = moduleOutput.ToString();
            moduleOutput.Length = 0;

            this.WriteIndent(moduleOutput, this.InitialLevel);
            moduleOutput.Append(JS.Funcs.DEFINE + "(");

            if (!module.NoName)
            {
                moduleOutput.Append(module.Name.ToJavaScript());
                moduleOutput.Append(", ");
            }

            var enabledDependecies = this.GetEnabledDependecies(module, output);

            if (enabledDependecies.Count > 0)
            {
                moduleOutput.Append("[");
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(md.DependencyName.ToJavaScript());
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
                moduleOutput.Append("], ");
            }

            moduleOutput.Append("function (");

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(md.VariableName.IsNotEmpty() ? md.VariableName : md.DependencyName);
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
            }

            this.WriteNewLine(moduleOutput, ") {");

            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, INDENT + "var " + module.Name + " = { };");
            moduleOutput.Append(str);

            if (!str.Trim().EndsWith(NEW_LINE))
            {
                this.WriteNewLine(moduleOutput);
            }

            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, INDENT + "return " + module.Name + ";");
            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, "});");
        }

        private List<IPluginDependency> GetEnabledDependecies(Module module, IEmitterOutput output)
        {
            var dependencies = output.ModuleDependencies;
            var loader = this.AssemblyInfo.Loader;

            if (dependencies.ContainsKey(module.Name) && dependencies[module.Name].Count > 0)
            {
                return dependencies[module.Name].Where(d => !loader.IsManual(d.DependencyName)).ToList();
            }
            return new List<IPluginDependency>();
        }

        protected virtual void WrapToCommonJS(StringBuilder moduleOutput, Module module, IEmitterOutput output)
        {
            var str = moduleOutput.ToString();
            moduleOutput.Length = 0;

            moduleOutput.Append(INDENT);
            moduleOutput.Append("(function (");

            var enabledDependecies = this.GetEnabledDependecies(module, output);

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(md.VariableName.IsNotEmpty() ? md.VariableName : md.DependencyName);
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
            }

            this.WriteNewLine(moduleOutput, ") {");
            moduleOutput.Append(INDENT);
            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, "var " + module.Name + " = { };");
            moduleOutput.Append(str);

            if (!str.Trim().EndsWith(NEW_LINE))
            {
                this.WriteNewLine(moduleOutput);
            }

            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, INDENT + "module.exports." + module.Name + " = " + module.Name + ";");
            this.WriteIndent(moduleOutput, this.InitialLevel);
            moduleOutput.Append("}) (");

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append("require(" + md.DependencyName.ToJavaScript() + "),");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
            }

            this.WriteNewLine(moduleOutput, ");");
        }

        protected virtual void WrapToUMD(StringBuilder moduleOutput, Module module, IEmitterOutput output)
        {
            var str = moduleOutput.ToString();
            moduleOutput.Length = 0;

            this.WriteIndent(moduleOutput, 1);
            this.WriteNewLine(moduleOutput, "(function (root, factory) {");
            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "if (typeof define === 'function' && define.amd) {");
            this.WriteIndent(moduleOutput, 3);
            moduleOutput.Append(JS.Funcs.DEFINE + "(");
            if (!module.NoName)
            {
                moduleOutput.Append(module.Name.ToJavaScript());
                moduleOutput.Append(", ");
            }

            var enabledDependecies = this.GetEnabledDependecies(module, output);

            if (enabledDependecies.Count > 0)
            {
                moduleOutput.Append("[");
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(md.DependencyName.ToJavaScript());
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
                moduleOutput.Append("], ");
            }
            this.WriteNewLine(moduleOutput, "factory);");

            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "} else if (typeof module === 'object' && module.exports) {");
            this.WriteIndent(moduleOutput, 3);
            moduleOutput.Append("module.exports = factory(");
            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append("require(" + md.DependencyName.ToJavaScript() + "),");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1);
            }

            this.WriteNewLine(moduleOutput, ");");

            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "} else {");
            this.WriteIndent(moduleOutput, 3);
            moduleOutput.Append("root." + module.Name + " = factory(");

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append("root." + md.DependencyName);
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
            }

            this.WriteNewLine(moduleOutput, ");");
            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "}");

            this.WriteIndent(moduleOutput, 1);
            moduleOutput.Append("}(this, function (");

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(md.VariableName ?? md.DependencyName);
                    moduleOutput.Append(",");
                });
                moduleOutput.Remove(moduleOutput.Length - 1, 1); // remove trailing comma
            }

            moduleOutput.Append(") {");
            this.WriteNewLine(moduleOutput);

            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "var " + module.Name + " = { };");
            moduleOutput.Append(str);

            if (!str.Trim().EndsWith(NEW_LINE))
            {
                this.WriteNewLine(moduleOutput);
            }

            this.WriteIndent(moduleOutput, 2);
            this.WriteNewLine(moduleOutput, "return " + module.Name + ";");

            this.WriteIndent(moduleOutput, 1);
            this.WriteNewLine(moduleOutput, "}));");
        }

        protected virtual void WrapToES6(StringBuilder moduleOutput, Module module, IEmitterOutput output)
        {
            var str = moduleOutput.ToString();
            moduleOutput.Length = 0;

            moduleOutput.Append(INDENT);
            this.WriteNewLine(moduleOutput, "(function () {");

            moduleOutput.Append(INDENT);
            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, "var " + module.Name + " = { };");

            var enabledDependecies = this.GetEnabledDependecies(module, output);

            if (enabledDependecies.Count > 0)
            {
                enabledDependecies.Each(md =>
                {
                    moduleOutput.Append(INDENT);
                    this.WriteIndent(moduleOutput, this.InitialLevel);
                    this.WriteNewLine(moduleOutput, "import " + (md.VariableName.IsNotEmpty() ? md.VariableName : md.DependencyName) + " from " + md.DependencyName.ToJavaScript() + ";");
                });
            }

            moduleOutput.Append(str);

            if (!str.Trim().EndsWith(NEW_LINE))
            {
                this.WriteNewLine(moduleOutput);
            }

            this.WriteIndent(moduleOutput, this.InitialLevel);
            this.WriteNewLine(moduleOutput, INDENT + "export {" + module.Name + "};");
            this.WriteIndent(moduleOutput, this.InitialLevel);
            moduleOutput.Append("}) (");

            this.WriteNewLine(moduleOutput, ");");
        }
    }
}