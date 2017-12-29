using Bridge.Contract;
using System.Linq;
using Bridge.Contract.Constants;
using Mono.Cecil;

namespace Bridge.Translator
{
    public partial class Translator
    {
        private string GetProductVersionFromVersionInfo(System.Diagnostics.FileVersionInfo versionInfo)
        {
            string version = null;

            if (versionInfo?.ProductVersion != null)
            {
                version = versionInfo.ProductVersion.Trim();
            }

            // If version contains only 0 and dots like 0.0.0.0 then set it to default string.Empty
            // This helps get compatibility with Mono when it returns empty (whitespace) when AssemblyVersion is not set
            if (version == null || version.All(x => x == '0' || x == '.'))
            {
                version = JS.Types.System.Reflection.Assembly.Config.DEFAULT_VERSION;
            }

            return version;
        }

        private System.Diagnostics.FileVersionInfo GetAssemblyVersionByPath(string path)
        {
            System.Diagnostics.FileVersionInfo fileVerionInfo = null;
            try
            {
                fileVerionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            }
            catch (System.Exception ex)
            {
                this.Log.Error("Could not load " + path + " to get the assembly info");
                this.Log.Error(ex.ToString());
            }

            return fileVerionInfo;
        }

        private VersionContext _versionContext;

        public VersionContext GetVersionContext()
        {
            if (this._versionContext == null)
            {
                this._versionContext = new VersionContext
                {
                    Assembly = this.GetVersionFromFileVersionInfo(this.GetAssemblyVersionByPath(this.AssemblyLocation)),
                    Bridge = this.GetAssemblyVersionByPath(this.BridgeLocation),
                    Compiler = this.GetAssemblyVersionByPath(System.Reflection.Assembly.GetExecutingAssembly().Location)
                };

                this._versionContext.Assembly.Description = GetAssemblyDescription(this.AssemblyDefinition);
                this._versionContext.Assembly.Title = GetAssemblyTitle(this.AssemblyDefinition);
            }

            return this._versionContext;
        }

        private static string GetAssemblyDescription(AssemblyDefinition provider)
        {
            string assemblyDescription = null;

            var assemblyDescriptionAttribute = provider.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == CS.Attributes.ASSEMBLY_DESCRIPTION);

            if (assemblyDescriptionAttribute != null && assemblyDescriptionAttribute.HasConstructorArguments)
            {
                assemblyDescription = assemblyDescriptionAttribute.ConstructorArguments[0].Value as string;
            }

            assemblyDescription = assemblyDescription?.Trim();

            return assemblyDescription;
        }

        private static string GetAssemblyTitle(AssemblyDefinition provider)
        {
            string assemblyDescription = null;

            var assemblyDescriptionAttribute = provider.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == CS.Attributes.ASSEMBLY_TITLE);

            if (assemblyDescriptionAttribute != null
                && assemblyDescriptionAttribute.HasConstructorArguments)
            {
                assemblyDescription = assemblyDescriptionAttribute.ConstructorArguments[0].Value as string;
            }

            assemblyDescription = assemblyDescription?.Trim();

            return assemblyDescription;
        }


        private VersionContext.AssemblyVersion GetVersionFromFileVersionInfo(System.Diagnostics.FileVersionInfo versionInfo)
        {
            return versionInfo == null
                    ? new VersionContext.AssemblyVersion()
                    : new VersionContext.AssemblyVersion()
                    {
                        CompanyName = versionInfo.CompanyName?.Trim(),
                        Copyright = versionInfo.LegalCopyright?.Trim(),
                        Version = this.GetProductVersionFromVersionInfo(versionInfo),
                        Name = versionInfo.ProductName?.Trim()
                    };
        }

      

        private void LogProductInfo()
        {
            var version = this.GetVersionContext();
            var compilerInfo = version.Compiler;
            var bridgeInfo = version.Bridge;

            this.Log.Info("Product info:");
            if (compilerInfo != null)
            {
                this.Log.Info($"\t{compilerInfo.ProductName} version {compilerInfo.ProductVersion}");
            }
            else
            {
                this.Log.Info("Not found");
            }

            if (bridgeInfo != null)
            {
                this.Log.Info($"\t[{bridgeInfo.ProductName} Framework, version {bridgeInfo.ProductVersion}]");
            }

            if (compilerInfo != null)
            {
                this.Log.Info("\t" + compilerInfo.LegalCopyright);
            }
        }
    }
}