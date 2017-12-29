using System;
using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public partial class Translator
    {
        public Stack<string> CurrentAssemblyLocationInspected
        {
            get; set;
        } = new Stack<string>();

        private class CecilAssemblyResolver : DefaultAssemblyResolver
        {
            private readonly ILogger _logger;

            public CecilAssemblyResolver(ILogger logger, string location)
            {
                this._logger = logger;

                this.ResolveFailure += this.OnResolveFailure;

                this.AddSearchDirectory(Path.GetDirectoryName(location));
            }

            private AssemblyDefinition OnResolveFailure(object sender, AssemblyNameReference reference)
            {
                string fullName = reference != null ? reference.FullName : "";
                this._logger.Trace("CecilAssemblyResolver: ResolveFailure " + (fullName ?? ""));

                return null;
            }

            public override AssemblyDefinition Resolve(string fullName)
            {
                this._logger.Trace("CecilAssemblyResolver: Resolve(string) " + (fullName ?? ""));

                return base.Resolve(fullName);
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                string fullName = name != null ? name.FullName : "";

                this._logger.Trace("CecilAssemblyResolver: Resolve(AssemblyNameReference) " + (fullName ?? ""));

                return base.Resolve(name);
            }

            public override AssemblyDefinition Resolve(string fullName, ReaderParameters parameters)
            {
                this._logger.Trace(
                    "CecilAssemblyResolver: Resolve(string, ReaderParameters) "
                    + (fullName ?? "")
                    + ", "
                    + (parameters?.ReadingMode.ToString() ?? "")
                    );

                return base.Resolve(fullName, parameters);
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                string fullName = name != null ? name.FullName : "";

                this._logger.Trace(
                    "CecilAssemblyResolver: Resolve(AssemblyNameReference, ReaderParameters) "
                    + (fullName ?? "")
                    + ", "
                    + (parameters?.ReadingMode.ToString() ?? "")
                    );

                return base.Resolve(name, parameters);
            }
        }

        protected virtual AssemblyDefinition LoadAssembly(string location, List<AssemblyDefinition> references)
        {
            this.Log.Trace("Assembly definition loading " + (location ?? "") + " ...");

            if (this.CurrentAssemblyLocationInspected.Contains(location))
            {
                var message = $"There is a circular reference found for assembly location {location}. To avoid the error, rename your project's assembly to be different from that location.";

                this.Log.Error(message);
                throw new System.InvalidOperationException(message);
            }

            this.CurrentAssemblyLocationInspected.Push(location);

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(
                    location,
                    new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Deferred,
                        AssemblyResolver = new CecilAssemblyResolver(this.Log, this.AssemblyLocation)
                    }
                );

            foreach (var r in assemblyDefinition.MainModule.AssemblyReferences)
            {
                var name = r.Name;

                if (name == SystemAssemblyName || name == "System.Core")
                {
                    continue;
                }

                var fullName = r.FullName;

                if (references.Any(a => a.Name.FullName == fullName))
                {
                    continue;
                }

                var path = Path.Combine(Path.GetDirectoryName(location), name) + ".dll";

                var updateBridgeLocation = name.ToLowerInvariant() == "bridge" && (string.IsNullOrWhiteSpace(this.BridgeLocation) || !File.Exists(this.BridgeLocation));

                if (updateBridgeLocation)
                {
                    this.BridgeLocation = path;
                }

                var reference = this.LoadAssembly(path, references);

                if (reference != null && references.All(a => a.Name.FullName != reference.Name.FullName))
                {
                    references.Add(reference);
                }
            }

            this.Log.Trace("Assembly definition loading " + (location ?? "") + " done");

            var cl = this.CurrentAssemblyLocationInspected.Pop();

            if (cl != location)
            {
                throw new System.InvalidOperationException(
                    $"Current location {location} is not the current location in stack {cl}");
            }

            return assemblyDefinition;
        }

        protected virtual List<AssemblyDefinition> InspectReferences()
        {
            this.Log.Info("Inspecting references...");

            var references = new List<AssemblyDefinition>();
            this.AssemblyDefinition = this.LoadAssembly(this.AssemblyLocation, references);
            this.Types = new BridgeTypes(this);

            if (!this.FolderMode)
            {
                var prefix = Path.GetDirectoryName(this.Location);

                for (int i = 0; i < this.SourceFiles.Count; i++)
                {
                    this.SourceFiles[i] = Path.Combine(prefix, this.SourceFiles[i]);
                }
            }

            this.Log.Info("Inspecting references done");

            return references;
        }

        protected virtual void InspectTypes()
        {
            this.Log.Info("Inspecting types...");

            var inspector = new Inspector(this);
            inspector.ReadTypes();

            foreach (var sourceFile in this.ParsedSourceFiles)
            {
                this.Log.Trace("Visiting syntax tree " + (sourceFile?.ParsedFile?.FileName ?? ""));

                inspector.VisitSyntaxTree(sourceFile.SyntaxTree);
            }

            this.Log.Info("Inspecting types done");
        }
        

        private string[] Rewrite()
        {
            this.Log.Info("Rewriting new C# features...");
            var rewriter = new SharpSixRewriter(this);
            var result = new string[this.SourceFiles.Count];

            Parallel.For(0, this.SourceFiles.Count, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, index =>
            {
                result[index] = new SharpSixRewriter(rewriter).Rewrite(index);
            });

            this.Log.Info("Rewriting new C# features done...");

            return result;
        }

        protected void BuildSyntaxTree()
        {
            this.Log.Info("Building syntax tree...");

            var rewriten = this.Rewrite();

            for (int i = 0; i < this.SourceFiles.Count; i++)
            {
                var fileName = this.SourceFiles[i];

                this.Log.Trace("Source file " + (fileName ?? string.Empty) + " ...");

                var parser = new CSharpParser();

                if (this.AssemblyInfo.DefineConstants != null && this.AssemblyInfo.DefineConstants.Count > 0)
                {
                    foreach (var defineConstant in this.AssemblyInfo.DefineConstants)
                    {
                        parser.CompilerSettings.ConditionalSymbols.Add(defineConstant);
                    }
                }

                var syntaxTree = parser.Parse(rewriten[i], fileName);
                syntaxTree.FileName = fileName;
                //var syntaxTree = parser.Parse(reader, fileName);
                this.Log.Trace("\tParsing syntax tree done");

                if (parser.HasErrors)
                {
                    foreach (var error in parser.Errors)
                    {
                        throw new EmitterException(syntaxTree, string.Format("Parsing error in a file {0} {2}: {1}", fileName, error.Message, error.Region.Begin.ToString()));
                    }
                }

                var expandResult = new QueryExpressionExpander().ExpandQueryExpressions(syntaxTree);
                this.Log.Trace("\tExpanding query expressions done");

                syntaxTree = (expandResult != null ? (SyntaxTree)expandResult.AstNode : syntaxTree);

                var emptyLambdaDetecter = new EmptyLambdaDetecter();
                syntaxTree.AcceptVisitor(emptyLambdaDetecter);
                this.Log.Trace("\tAccepting lambda detector visitor done");

                if (emptyLambdaDetecter.Found)
                {
                    var fixer = new EmptyLambdaFixer();
                    var astNode = syntaxTree.AcceptVisitor(fixer);
                    this.Log.Trace("\tAccepting lambda fixer visitor done");
                    syntaxTree = (astNode != null ? (SyntaxTree)astNode : syntaxTree);
                    syntaxTree.FileName = fileName;
                }

                var f = new ParsedSourceFile(syntaxTree, new CSharpUnresolvedFile
                {
                    FileName = fileName
                });
                this.ParsedSourceFiles.Add(f);

                var tcv = new TypeSystemConvertVisitor(f.ParsedFile);
                f.SyntaxTree.AcceptVisitor(tcv);
                this.Log.Trace("\tAccepting type system convert visitor done");

                this.Log.Trace("Source file " + (fileName ?? string.Empty) + " done");
            }

            this.Log.Info("Building syntax tree done");
        }
    }
}