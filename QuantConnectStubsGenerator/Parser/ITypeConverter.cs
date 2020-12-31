using Microsoft.CodeAnalysis;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Parser
{
    public interface ITypeConverter<T>
        where T : ILanguageType<T>
    {
        /// <summary>
        /// Returns the symbol of the given node.
        /// Returns null if the semantic model does not contain a symbol for the node.
        /// </summary>
        ISymbol GetSymbol(SyntaxNode node);

        /// <summary>
        /// Returns the Python type of the given node.
        /// Returns an aliased typing.Any if there is no Python type for the given symbol.
        /// </summary>
        PythonType GetType(SyntaxNode node, bool skipPythonTypeCheck = false);

        /// <summary>
        /// Returns the Python type of the given symbol.
        /// Returns an aliased typing.Any if there is no Python type for the given symbol.
        /// </summary>
        PythonType GetType(ISymbol symbol, bool skipPythonTypeCheck = false);

        string GetTypeName(ISymbol symbol);

        /// <summary>
        /// Converts a C# type to a Python type.
        /// This method handles conversions like the one from System.String to str.
        /// If the Type object doesn't need to be converted, the originally provided type is returned.
        /// </summary>
        ILanguageType<T> TypeToTargetLanguageType(ILanguageType<T> type, bool skipPythonTypeCheck = false);
    }
}
