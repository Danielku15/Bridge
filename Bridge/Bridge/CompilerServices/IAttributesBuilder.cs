using System;
using System.Linq.Expressions;

namespace Bridge.CompilerServices
{
    [ExternalInterface]
    [NonScriptable]
    public interface IAttributesBuilder
    {
        void Add(params Attribute[] attributes);
    }
}