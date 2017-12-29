using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Bridge.Contract.Constants;
using Bridge.Translator.Utils;
// ReSharper disable InconsistentNaming

namespace Bridge.Translator
{
    public partial class Translator
    {
        public static class ProjectPropertyNames
        {
            public const string OUTPUT_TYPE_PROP = "OutputType";
            public const string ASSEMBLY_NAME_PROP = "AssemblyName";
            public const string DEFINE_CONSTANTS_PROP = "DefineConstants";
            public const string ROOT_NAMESPACE_PROP = "RootNamespace";
            public const string OUTPUT_PATH_PROP = "OutputPath";
            public const string OUT_DIR_PROP = "OutDir";
            public const string CONFIGURATION_PROP = "Configuration";
            public const string PLATFORM_PROP = "Platform";
        }

        private bool ShouldReadProjectFile
        {
            get; set;
        }

        internal virtual void EnsureProjectProperties()
        {
            this.Log.Trace("EnsureProjectProperties at " + (this.Location ?? "") + " ...");

            this.ShouldReadProjectFile = !this.FromTask;

            var doc = XDocument.Load(this.Location, LoadOptions.SetLineInfo);

            this.ValidateProject(doc);

            this.EnsureOverflowMode(doc);

            this.EnsureDefaultNamespace(doc);

            this.EnsureAssemblyName(doc);

            this.EnsureAssemblyLocation(doc);

            this.SourceFiles = this.GetSourceFiles(doc);
            this.ParsedSourceFiles = new List<ParsedSourceFile>();

            this.EnsureDefineConstants(doc);

            this.Log.Trace("EnsureProjectProperties done");
        }

        internal virtual void ReadFolderFiles()
        {
            this.Log.Trace("Reading folder files...");

            this.SourceFiles = this.GetSourceFiles(this.Location);
            this.ParsedSourceFiles = new List<ParsedSourceFile>();

            this.Log.Trace("Reading folder files done");
        }

        /// <summary>
        /// Validates project and namespace names against conflicts with Bridge.NET namespaces.
        /// </summary>
        /// <param name="doc">XDocument reference of the .csproj file.</param>
        private void ValidateProject(XDocument doc)
        {
            var valid = true;
            var failList = new HashSet<string>();
            var failNodeList = new List<XElement>();
            var combined_tags = from x in doc.Descendants()
                                where x.Name.LocalName == ProjectPropertyNames.ROOT_NAMESPACE_PROP || x.Name.LocalName == ProjectPropertyNames.ASSEMBLY_NAME_PROP
                                select x;

            // Replace '\' with '/' in any occurrence of <OutputPath><path></OutputPath>
            foreach (var ope in doc.Descendants().Where(e => e.Name.LocalName == ProjectPropertyNames.OUTPUT_PATH_PROP && e.Value.Contains("\\")))
            {
                ope.SetValue(ope.Value.Replace("\\", "/"));
            }

            // Replace '\' with '/' in any occurrence of <OutDir><path></OutDir>
            foreach (var ope in doc.Descendants().Where(e => e.Name.LocalName == ProjectPropertyNames.OUT_DIR_PROP && e.Value.Contains("\\")))
            {
                ope.SetValue(ope.Value.Replace("\\", "/"));
            }

            // Replace now for <Compile Include="<path>" />
            foreach (var ope in doc.Descendants().Where(e =>
                e.Name.LocalName == "Compile" &&
                e.Attributes().Any(a => a.Name.LocalName == "Include") &&
                e.Attribute("Include").Value.Contains("\\")))
            {
                var incAtt = ope.Attribute("Include");
                incAtt.SetValue(incAtt.Value.Replace("\\", "/"));
            }

            if (!this.AssemblyInfo.Assembly.EnableReservedNamespaces)
            {
                foreach (var tag in combined_tags)
                {
                    if (tag.Value == CS.NS.BRIDGE)
                    {
                        valid = false;
                        if (!failList.Contains(tag.Value))
                        {
                            failList.Add(tag.Value);
                            failNodeList.Add(tag);
                        }
                    }
                }
            }

            if (!valid)
            {
                var offendingSettings = "";
                foreach (var tag in failNodeList)
                {
                    offendingSettings += "Line " + ((IXmlLineInfo)tag).LineNumber + ": <" + tag.Name.LocalName + ">" +
                        tag.Value + "</" + tag.Name.LocalName + ">\n";
                }

                throw new TranslatorException("'Bridge' name is reserved and may not " +
                    "be used as project names or root namespaces.\n" +
                    "Please verify your project settings and rename where it applies.\n" +
                    "Project file: " + this.Location + "\n" +
                    "Offending settings:\n" + offendingSettings
                );
            }

            var outputType = this.ProjectProperties.OutputType;

            if (outputType == null && this.ShouldReadProjectFile)
            {
                var projectType = (from n in doc.Descendants()
                                   where n.Name.LocalName == ProjectPropertyNames.OUTPUT_TYPE_PROP
                                   select n).ToArray();

                if (projectType.Length > 0)
                {
                    outputType = projectType[0].Value;
                }
            }

            if (outputType != null && String.Compare(outputType, SupportedProjectType, StringComparison.InvariantCultureIgnoreCase) != 0)
            {
                TranslatorException.Throw("Project type ({0}) is not supported, please use Library instead of {0}", outputType);
            }
        }

        private void EnsureOverflowMode(XDocument doc)
        {
            if (!this.ShouldReadProjectFile)
            {
                return;
            }

            if (this.OverflowMode.HasValue)
            {
                return;
            }

            var node = (from n in doc.Descendants()
                        where n.Name.LocalName == "CheckForOverflowUnderflow"
                        select n).LastOrDefault();

            if (node != null)
            {
                var value = node.Value;
                if (bool.TryParse(value, out var boolValue))
                {
                    this.OverflowMode = boolValue ? Contract.OverflowMode.Checked : Contract.OverflowMode.Unchecked;
                }
            }
        }

        protected virtual void EnsureAssemblyLocation(XDocument doc)
        {
            this.Log.Trace("BuildAssemblyLocation...");

            if (string.IsNullOrEmpty(this.AssemblyLocation))
            {
                var fullOutputPath = this.GetOutputPaths(doc);

                this.Log.Trace("    FullOutputPath:" + fullOutputPath);

                this.AssemblyLocation = Path.Combine(fullOutputPath, this.ProjectProperties.AssemblyName + ".dll");
            }

            this.Log.Trace("    OutDir:" + this.ProjectProperties.OutDir);
            this.Log.Trace("    OutputPath:" + this.ProjectProperties.OutputPath);
            this.Log.Trace("    AssemblyLocation:" + this.AssemblyLocation);

            this.Log.Trace("BuildAssemblyLocation done");
        }

        protected virtual string GetOutputPaths(XDocument doc)
        {
            var configHelper = new Contract.ConfigHelper();

            var outputPath = this.ProjectProperties.OutputPath;

            if (outputPath == null && this.ShouldReadProjectFile)
            {
                // Read OutputPath if not defined already
                // Throw exception if not found
                outputPath = this.ReadProperty(doc, ProjectPropertyNames.OUTPUT_PATH_PROP, false, configHelper);
            }

            if (outputPath == null)
            {
                outputPath = string.Empty;
            }

            this.ProjectProperties.OutputPath = outputPath;

            var outDir = this.ProjectProperties.OutDir;

            if (outDir == null && this.ShouldReadProjectFile)
            {
                // Read OutDir if not defined already
                outDir = this.ReadProperty(doc, ProjectPropertyNames.OUT_DIR_PROP, true, configHelper);
            }

            // If OutDir value is not found then use OutputPath value
            this.ProjectProperties.OutDir = outDir ?? outputPath;

            var fullPath = this.ProjectProperties.OutDir;

            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(this.Location), fullPath));
            }

            fullPath = configHelper.ConvertPath(fullPath);

            return fullPath;
        }

        private string ReadProperty(XDocument doc, string name, bool safe, Contract.ConfigHelper configHelper)
        {
            var nodes = from n in doc.Descendants()
                        where String.Compare(n.Name.LocalName, name, StringComparison.InvariantCultureIgnoreCase) == 0 && this.EvaluateCondition(n.Parent.Attribute("Condition")?.Value)
                        select n;

            if (nodes.Count() != 1)
            {
                if (safe)
                {
                    return null;
                }

                TranslatorException.Throw(
                    "Unable to determine "
                    + name
                    + " in the project file with conditions " + this.EvaluationConditionsAsString());
            }

            var value = nodes.First().Value;
            value = configHelper.ConvertPath(value);

            return value;
        }

        private Dictionary<string, string> GetEvaluationConditions()
        {
            var properties = new Dictionary<string, string>();

            if (this.ProjectProperties.Configuration != null)
            {
                properties.Add(ProjectPropertyNames.CONFIGURATION_PROP, this.ProjectProperties.Configuration);
            }

            if (this.ProjectProperties.Platform != null)
            {
                properties.Add(ProjectPropertyNames.PLATFORM_PROP, this.ProjectProperties.Platform);
            }

            return properties;
        }

        private string EvaluationConditionsAsString()
        {
            var conditions = string.Join(", ", this.GetEvaluationConditions().Select(x => x.Key + ": " + x.Value));

            return conditions;
        }

        private bool EvaluateCondition(string condition)
        {
            if (condition == null)
            {
                return true;
            }

            var properties = this.GetEvaluationConditions();

            return MsBuildConditionEvaluator.EvaluateCondition(condition, properties);
        }

        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        protected virtual IList<string> GetSourceFiles(XDocument doc)
        {
            this.Log.Trace("Getting source files by xml...");

            IList<string> sourceFiles = new List<string>();

            if (this.Source == null)
            {
                var isOnMono = IsRunningOnMono();
                Project project;
                if (isOnMono)
                {
                    // Using XmlReader here addresses a Mono issue logged as #38224 at Mono's official BugZilla.
                    // Bridge issue #860
                    // This constructor below works on Linux and DOES break #531
                    project = new Project(XmlReader.Create(this.Location), null, null, new ProjectCollection());
                }
                else
                {
                    // Using XmlReader above breaks feature #531 - referencing linked files in csproj (Compiler test 18 fails)
                    // To avoid it at least on Windows, use different Project constructors
                    // This constructor below works on Windows and does NOT break #531
                    project = new Project(this.Location, null, null, new ProjectCollection());
                }

                foreach (var projectItem in project.GetItems("Compile"))
                {
                    sourceFiles.Add(projectItem.EvaluatedInclude);
                }

                if (isOnMono)
                {
                    // This UnloadProject overload should be used if the project created by new Project(XmlReader.Create(this.Location)...)
                    // Otherwise it does NOT work either on Windows or Linux
                    project.ProjectCollection.UnloadProject(project.Xml);
                }
                else
                {
                    // This UnloadProject overload should be used if the project created by new Project(this.Location...)
                    // Otherwise it does NOT work either on Windows or Linux
                    project.ProjectCollection.UnloadProject(project);
                }

                if (!sourceFiles.Any())
                {
                    throw new TranslatorException("Unable to get source file list from project file '" +
                        this.Location + "'. In order to use bridge, you have to have at least one source code file " +
                        "with the 'compile' property set (usually .cs files have it by default in C# projects).");
                };
            }
            else
            {
                sourceFiles = this.GetSourceFiles(Path.GetDirectoryName(this.Location));
            }

            this.Log.Trace("Getting source files by xml done");

            return sourceFiles;
        }

        protected virtual void EnsureDefineConstants(XDocument doc)
        {
            this.Log.Trace("EnsureDefineConstants...");

            if (this.AssemblyInfo.DefineConstants == null)
            {
                this.AssemblyInfo.DefineConstants = new List<string>();
            }

            if (this.ProjectProperties.DefineConstants == null && this.ShouldReadProjectFile)
            {
                this.Log.Trace("Reading define constants...");

                var nodeList = doc.Descendants().Where(n =>
                {
                    if (n.Name.LocalName != "PropertyGroup")
                    {
                        return false;
                    }

                    var attr = n.Attribute("Condition");
                    return attr == null || this.EvaluateCondition(attr.Value);
                });

                this.ProjectProperties.DefineConstants = "";

                foreach (var node in nodeList)
                {
                    var constants = string.Join(";", from n in node.Descendants()
                                    where n.Name.LocalName == ProjectPropertyNames.DEFINE_CONSTANTS_PROP
                                    select n.Value);

                    if (!string.IsNullOrEmpty(constants))
                    {
                        if (this.ProjectProperties.DefineConstants.Length > 0)
                        {
                            this.ProjectProperties.DefineConstants += ";";
                        }

                        this.ProjectProperties.DefineConstants += constants;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(this.ProjectProperties.DefineConstants))
            {
                this.AssemblyInfo.DefineConstants.AddRange(
                    this.ProjectProperties.DefineConstants.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            }
            this.AssemblyInfo.DefineConstants.Add("BRIDGE");
            this.AssemblyInfo.DefineConstants = this.AssemblyInfo.DefineConstants.Distinct().ToList();

            this.Log.Trace("EnsureDefineConstants done");
        }

        protected virtual void EnsureAssemblyName(XDocument doc)
        {
            if (this.ProjectProperties.AssemblyName == null && this.ShouldReadProjectFile)
            {
                var node = (from n in doc.Descendants()
                            where n.Name.LocalName == ProjectPropertyNames.ASSEMBLY_NAME_PROP
                            select n).FirstOrDefault();

                if (node != null)
                {
                    this.ProjectProperties.AssemblyName = node.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(this.ProjectProperties.AssemblyName))
            {
                TranslatorException.Throw("Unable to determine assembly name");
            }
        }

        protected virtual void EnsureDefaultNamespace(XDocument doc)
        {
            if (this.ProjectProperties.RootNamespace == null && this.ShouldReadProjectFile)
            {
                var node = (from n in doc.Descendants()
                            where n.Name.LocalName == ProjectPropertyNames.ROOT_NAMESPACE_PROP
                            select n).FirstOrDefault();

                if (node != null)
                {
                    this.ProjectProperties.RootNamespace = node.Value;
                }
            }

            this.DefaultNamespace = this.ProjectProperties.RootNamespace;

            if (string.IsNullOrWhiteSpace(this.DefaultNamespace))
            {
                this.DefaultNamespace = DefaultRootNamespace;
            }

            this.Log.Trace("DefaultNamespace:" + this.DefaultNamespace);
        }

        protected virtual IList<string> GetSourceFiles(string location)
        {
            this.Log.Trace("Getting source files by location...");

            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(this.Source))
            {
                this.Log.Trace("Source is not defined, will use *.cs mask");
                this.Source = "*.cs";
            }

            string[] parts = this.Source.Split(';');
            var searchOption = this.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var part in parts)
            {
                int index = part.LastIndexOf(Path.DirectorySeparatorChar);
                string folder = index > -1 ? Path.Combine(location, part.Substring(0, index + 1)) : location;
                string mask = index > -1 ? part.Substring(index + 1) : part;

                string[] allfiles = Directory.GetFiles(folder, mask, searchOption);
                result.AddRange(allfiles);
            }

            result = result.Distinct().ToList();

            this.Log.Trace("Getting source files by location done (found " + result.Count + " items)");

            return result;
        }
    }
}