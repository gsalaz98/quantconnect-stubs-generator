using System;
using System.Collections.Generic;

namespace QuantConnectStubsGenerator.Model
{
    public interface ILanguageType<T> : IEquatable<T>
    {
        string Name { get; set; }
        string Namespace { get; set; }
        string Alias { get; set; }
        bool IsNamedTypeParameter { get; set; }
        IList<T> TypeParameters { get; set; }
        bool IsAction { get; set; }
        string GetBaseName();
        string ToLanguageString(bool ignoreAlias = false);
        T New(string name, string ns = null);
    }
}
