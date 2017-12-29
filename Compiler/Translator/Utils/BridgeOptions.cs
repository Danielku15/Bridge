using System;
using System.Collections.Generic;
using System.Linq;
using Bridge.Contract;

namespace Bridge.Translator
{
    public class BridgeOptions
    {
        public string Name { get; set; }

        public ProjectProperties ProjectProperties { get; set; }

        public string ProjectLocation { get; set; }
        public string OutputLocation { get; set; }
        public string DefaultFileName { get; set; }
        public string BridgeLocation { get; set; }
        public bool Rebuild { get; set; }
        public bool ExtractCore { get; set; }
        public string Folder { get; set; }
        public bool Recursive { get; set; }
        public string Lib { get; set; }
        public bool NoCompilation { get; set; }
        public bool Run { get; set; }
        public bool? NoTimeStamp { get; set; }
        public bool FromTask { get; set; }
        public bool NoLoggerSetUp { get; set; }
        public string Sources { get; set; }
        public string ReferencesPath { get; set; }

        public bool IsFolderMode { get { return string.IsNullOrWhiteSpace(this.ProjectLocation); } }

        public BridgeOptions()
        {
            this.ExtractCore = true;
            this.Folder = Environment.CurrentDirectory;
        }

        public override string ToString()
        {
            return string.Join(", ", this.GetValues().Select(x => x.Key + ":" + x.Value));
        }

        protected Dictionary<string, string> GetValues()
        {
            var r = new Dictionary<string, string>()
            {
                {this.WrapProperty("Name"), this.GetString(this.Name) },
                {this.WrapProperty("ProjectProperties"), this.GetString(this.ProjectProperties) },
                {this.WrapProperty("ProjectLocation"), this.GetString(this.ProjectLocation) },
                {this.WrapProperty("OutputLocation"), this.GetString(this.OutputLocation) },
                {this.WrapProperty("DefaultFileName"), this.GetString(this.DefaultFileName) },
                {this.WrapProperty("BridgeLocation"), this.GetString(this.BridgeLocation) },
                {this.WrapProperty("Rebuild"), this.GetString(this.Rebuild) },
                {this.WrapProperty("ExtractCore"), this.GetString(this.ExtractCore) },
                {this.WrapProperty("Folder"), this.GetString(this.Folder) },
                {this.WrapProperty("Recursive"), this.GetString(this.Recursive) },
                {this.WrapProperty("Lib"), this.GetString(this.Lib) },
                {this.WrapProperty("Help"), this.GetString(this.NoCompilation) },
                {this.WrapProperty("NoTimeStamp"), this.GetString(this.NoTimeStamp) },
                {this.WrapProperty("Run"), this.GetString(this.Run) },
                {this.WrapProperty("FromTask"), this.GetString(this.FromTask) },
                {this.WrapProperty("NoLoggerSetUp"), this.GetString(this.NoLoggerSetUp) },
                {this.WrapProperty("Sources"), this.GetString(this.Sources) },
                {this.WrapProperty("ReferencesPath"), this.GetString(this.ReferencesPath) }
            };

            return r;
        }

        protected string WrapProperty(string name)
        {
            return name;
        }

        protected string GetString(string s)
        {
            return s ?? "";
        }

        protected string GetString(ProjectProperties p)
        {
            return p?.ToString() ?? "";
        }

        protected string GetString(bool? b)
        {
            return b.HasValue ? this.GetString(b.Value) : this.GetString((string)null);
        }

        protected string GetString(bool b)
        {
            return b.ToString().ToLowerInvariant();
        }
    }
}