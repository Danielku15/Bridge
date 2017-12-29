namespace Bridge.Contract
{
    public class VersionContext
    {
        public class AssemblyVersion
        {
            public string CompanyName
            {
                get; set;
            }

            public string Copyright
            {
                get; set;
            }

            public string Title
            {
                get; set;
            }

            public string Description
            {
                get; set;
            }

            public string Name
            {
                get; set;
            }

            public string Version
            {
                get; set;
            }
        }

        public AssemblyVersion Assembly
        {
            get; set;
        }

        public System.Diagnostics.FileVersionInfo Bridge
        {
            get; set;
        }

        public System.Diagnostics.FileVersionInfo Compiler
        {
            get; set;
        }
    }
 }
