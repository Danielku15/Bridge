using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Contract
{
    public interface ITypeInfoCollection
    {
        ITypeInfo Get(IType type);
        ITypeInfo GetOrCreateTypeInfo(ITypeDefinition type);

        IEnumerable<ITypeInfo> AllTypes { get; }
        IEnumerable<ITypeInfo> OutputTypes { get; }
        void AddToOutput(ITypeInfo typeInfo);
        void RemoveFromOutput(ITypeInfo typeInfo);
        void SortOutputTypes();
    }
}