using System;
using System.Linq;
using System.Collections.Generic;

using Bridge.Contract;
using Bridge.Translator.Tests.Helpers;


using NUnit.Framework;
using NSubstitute;

namespace Bridge.Translator.Tests.Helpers
{
    class EmitterHelper
    {
        public static Emitter GetEmitter(BridgeTypes bridgeTypes = null, IAssemblyInfo assemblyInfo = null)
        {
            var translator = Substitute.For<ITranslator>();
            translator.AssemblyInfo.Returns(assemblyInfo);
            var emitter = new Emitter(translator);

            emitter.DisableDependencyTracking = false;

            emitter.CurrentDependencies = new List<IPluginDependency>();

            return emitter;
        }
    }
}
