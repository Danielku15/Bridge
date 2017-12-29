using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Microsoft.Ajax.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory.TypeSystem;
using AstNode = ICSharpCode.NRefactory.CSharp.AstNode;

namespace Bridge.Translator
{
    public partial class Translator : ITranslator
    {
        public const string BridgeResourcesPlusSeparatedFormatList = "Bridge.Resources.list";
        public const string BridgeResourcesJsonFormatList = "Bridge.Resources.json";
        public const string BridgeResourcesCombinedPrefix = "Bridge.Resources.Parts.";
        public const string LocalesPrefix = "Bridge.Resources.Locales.";
        public const string DefaultLocalesOutputName = "Bridge.Locales.js";
        public const string BridgeConsoleName = "bridge.console.js";
        public const string SupportedProjectType = "Library";
        public const string DefaultRootNamespace = "ClassLibrary";
        public const string SystemAssemblyName = "mscorlib";

        public static readonly Encoding OutputEncoding = new UTF8Encoding(false);
        private static readonly string[] MinifierCodeSettingsInternalFileNames = { "bridge.js", "bridge.min.js", "bridge.collections.js", "bridge.collections.min.js" };

        private char[] _invalidPathChars;
        public char[] InvalidPathChars
        {
            get
            {
                if (this._invalidPathChars == null)
                {
                    var l = new List<char>(Path.GetInvalidPathChars());
                    l.AddRange(new[] { '<', '>', ':', '"', '|', '?', '*' });
                    this._invalidPathChars = l.Distinct().ToArray();
                }

                return this._invalidPathChars;
            }
        }

        public FileHelper FileHelper
        {
            get;
        }

        private static readonly CodeSettings MinifierCodeSettingsSafe = new CodeSettings
        {
            EvalTreatment = EvalTreatment.MakeAllSafe,
            LocalRenaming = LocalRenaming.KeepAll,
            TermSemicolons = true,
            StrictMode = false
        };

        private static readonly CodeSettings MinifierCodeSettingsInternal = new CodeSettings
        {
            TermSemicolons = true,
            StrictMode = false
        };

        private static readonly CodeSettings MinifierCodeSettingsLocales = new CodeSettings
        {
            TermSemicolons = true
        };

        protected Translator(ILogger log, string location)
        {
            this.Location = location;
            this.ProjectProperties = new ProjectProperties();
            this.FileHelper = new FileHelper();
            this.Outputs = new TranslatorOutput();
            this.Log = log;
        }

        public Translator(ILogger log, string location, string source, bool fromTask = false) : this(log, location)
        {
            this.FromTask = fromTask;
            this.Source = source;
        }

        public Translator(ILogger log, string location, string source, bool recursive, string lib) : this(log, location, source)
        {
            this.Recursive = recursive;
            this.AssemblyLocation = lib;
            this.FolderMode = true;
            this.Outputs = new TranslatorOutput();
        }

        public void Translate()
        {
            var logger = this.Log;
            logger.Info("Translating...");            

            if (this.Rebuild)
            {
                logger.Info("Building assembly as Rebuild option is enabled");
                this.BuildAssembly();
            }
            else if (!File.Exists(this.AssemblyLocation))
            {
                logger.Info("Building assembly as it is not found at " + this.AssemblyLocation);
                this.BuildAssembly();
            }

            this.Emitter = new Emitter(this);
            this.Outputs.Report = new TranslatorOutputItem
            {
                Content = new StringBuilder(),
                OutputKind = TranslatorOutputKind.Report,
                OutputType = TranslatorOutputType.None,
                Name = this.AssemblyInfo.Report.FileName ?? "bridge.report.log",
                Location = this.AssemblyInfo.Report.Path
            };

            AttributeRegistry.Reset(this);

            this.References = this.InspectReferences();

            this.LogProductInfo();

            this.Plugins = Bridge.Translator.Plugins.GetPlugins(this, this.AssemblyInfo);

            logger.Info("Reading plugin configs...");
            this.Plugins.OnConfigRead(this.AssemblyInfo);
            logger.Info("Reading plugin configs done");

            var beforeBuild = this.AssemblyInfo.BeforeBuild;
            if (!string.IsNullOrWhiteSpace(beforeBuild))
            {
                try
                {
                    logger.Info("Running BeforeBuild event " + beforeBuild + " ...");
                    this.RunEvent(beforeBuild);
                    logger.Info("Running BeforeBuild event done");
                }
                catch (Exception exc)
                {
                    var message = "Error: Unable to run beforeBuild event command: " + exc.Message + "\nStack trace:\n" + exc.StackTrace;

                    logger.Error("Exception occurred. Message: " + message);

                    throw new TranslatorException(message);
                }
            }

            this.BuildSyntaxTree();
            this.Preconvert();
            this.InspectTypes();

            this.Types.SortOutputTypes();

            if (!this.AssemblyInfo.OverflowMode.HasValue)
            {
                this.AssemblyInfo.OverflowMode = this.OverflowMode;
            }

            logger.Info("Before emitting...");
            this.Plugins.BeforeEmit(this);
            logger.Info("Before emitting done");

            this.AddMainOutputs(this.Emitter.Emit());

            logger.Info("After emitting...");
            this.Plugins.AfterEmit(this);
            logger.Info("After emitting done");

            logger.Info("Translating done");
        }

        protected virtual void Preconvert()
        {
            var resolver = new MemberResolver(this, this.ParsedSourceFiles, this.References, this.ProjectProperties.AssemblyName);

            bool needRecompile = false;
            foreach (var sourceFile in this.ParsedSourceFiles)
            {
                this.Log.Trace("Preconvert " + sourceFile.ParsedFile.FileName);
                var syntaxTree = sourceFile.SyntaxTree;
                var detecter = new PreconverterDetecter(resolver, this.Emitter);
                syntaxTree.AcceptVisitor(detecter);

                if (detecter.Found)
                {
                    var fixer = new PreconverterFixer(resolver, this.Emitter, this.Log);
                    var astNode = syntaxTree.AcceptVisitor(fixer);
                    syntaxTree = astNode != null ? (SyntaxTree)astNode : syntaxTree;
                    sourceFile.SyntaxTree = syntaxTree;
                    needRecompile = true;
                }
            }

            if (needRecompile)
            {
                resolver = new MemberResolver(this, this.ParsedSourceFiles, resolver.References, this.ProjectProperties.AssemblyName);
            }

            this.Resolver = resolver;
        }

        private static void NewLine(StringBuilder sb, string line = null)
        {
            if (line != null)
            {
                sb.Append(line);
            }

            sb.Append(Bridge.Translator.Emitter.NEW_LINE);
        }

        private static void NewLine(MemoryStream sb, string line = null)
        {
            if (line != null)
            {
                var b = OutputEncoding.GetBytes(line);
                sb.Write(b, 0, b.Length);
            }

            var nl = OutputEncoding.GetBytes(Bridge.Translator.Emitter.NEW_LINE);
            sb.Write(nl, 0, nl.Length);
        }

        public bool CheckIfRequiresSourceMap(TranslatorOutputItem output)
        {
            return !output.IsEmpty
                && output.OutputType == TranslatorOutputType.JavaScript
                && output.OutputKind.HasFlag(TranslatorOutputKind.ProjectOutput)
                && !output.OutputKind.HasFlag(TranslatorOutputKind.Locale)
                && !output.OutputKind.HasFlag(TranslatorOutputKind.PluginOutput)
                && !output.OutputKind.HasFlag(TranslatorOutputKind.Reference)
                && !output.OutputKind.HasFlag(TranslatorOutputKind.Resource)
                && !output.OutputKind.HasFlag(TranslatorOutputKind.Metadata);
        }

        public bool CheckIfRequiresSourceMap(BridgeResourceInfoPart resourcePart)
        {
            var fileHelper = new FileHelper();

            return resourcePart != null
                && resourcePart.Assembly == null // i.e. this assembly output
                && fileHelper.IsJS(resourcePart.Name);
        }

        public TranslatorOutputItem FindTranslatorOutputItem(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            foreach (var output in this.Outputs.GetOutputs())
            {
                if (output.FullPath.LocalPath == filePath)
                {
                    return output;
                }
            }

            return null;
        }

        public string GenerateSourceMap(string fileName, string content, Action<SourceMapBuilder> before = null)
        {
            if (this.AssemblyInfo.SourceMap.Enabled)
            {
                var projectPath = Path.GetDirectoryName(this.Location);

                SourceMapGenerator.Generate(fileName, projectPath, ref content,
                    before,
                    (sourceRelativePath) =>
                    {
                        string path = null;

                        try
                        {
                            path = Path.Combine(projectPath, sourceRelativePath);
                            var sourceFile = this.ParsedSourceFiles.First(pf => pf.ParsedFile.FileName == path);

                            return sourceFile.SyntaxTree.TextSource ?? sourceFile.SyntaxTree.ToString(GetFormatter());
                        }
                        catch (Exception ex)
                        {
                            throw (TranslatorException)TranslatorException.Create(
                                "Could not get ParsedSourceFile for SourceMap. Exception: {0}; projectPath: {1}; sourceRelativePath: {2}; path: {3}.",
                                ex.ToString(), projectPath, sourceRelativePath, path);
                        }

                    },
                    new string[0], this.SourceFiles, this.AssemblyInfo.SourceMap.Eol, this.Log
                );
            }

            return content;
        }

        private static CSharpFormattingOptions GetFormatter()
        {
            var formatter = FormattingOptionsFactory.CreateSharpDevelop();
            formatter.AnonymousMethodBraceStyle = BraceStyle.NextLine;
            formatter.MethodBraceStyle = BraceStyle.NextLine;
            formatter.StatementBraceStyle = BraceStyle.NextLine;
            formatter.PropertyBraceStyle = BraceStyle.NextLine;
            formatter.ConstructorBraceStyle = BraceStyle.NextLine;
            formatter.NewLineAfterConstructorInitializerColon = NewLinePlacement.NewLine;
            formatter.NewLineAferMethodCallOpenParentheses = NewLinePlacement.NewLine;
            formatter.ClassBraceStyle = BraceStyle.NextLine;
            formatter.ArrayInitializerBraceStyle = BraceStyle.NextLine;
            formatter.IndentPreprocessorDirectives = false;

            return formatter;
        }

        public void RunAfterBuild()
        {
            this.Log.Info("Checking AfterBuild event...");

            if (!string.IsNullOrWhiteSpace(this.AssemblyInfo.AfterBuild))
            {
                try
                {
                    this.Log.Trace("Run AfterBuild event");
                    this.RunEvent(this.AssemblyInfo.AfterBuild);
                }
                catch (Exception ex)
                {
                    var message = "Error: Unable to run afterBuild event command: " + ex;

                    this.Log.Error(message);
                    throw new TranslatorException(message);
                }
            }
            else
            {
                this.Log.Trace("No AfterBuild event specified");
            }

            this.Log.Info("Done checking AfterBuild event...");
        }

        public EmitterException CreateExceptionFromLastNode()
        {
            return this.EmitNode != null ? new EmitterException(this.EmitNode) : null;
        }

        public ITypeInfo GetTypeInfo(AstNode type)
        {
            var rr = this.Resolver.ResolveNode(type);
            return this.Types.Get(rr.Type);
        }

        public bool IsCurrentAssembly(IAssembly assembly)
        {
            return assembly.FullAssemblyName.Equals(this.Resolver.Compilation.MainAssembly.FullAssemblyName);
        }

        public bool IsInCurrentAssembly(IEntity entity)
        {
            return entity.ParentAssembly.FullAssemblyName.Equals(this.Resolver.Compilation.MainAssembly.FullAssemblyName);
        }
    }
}