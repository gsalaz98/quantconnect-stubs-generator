using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Parser
{
    /// <summary>
    /// The TypeConverter is responsible for converting AST nodes into CppType instances.
    /// </summary>
    public class CppTypeConverter : ITypeConverter<CppType>
    {
        private readonly SemanticModel _model;

        public CppTypeConverter(SemanticModel model)
        {
            _model = model;
        }

        /// <summary>
        /// Returns the symbol of the given node.
        /// Returns null if the semantic model does not contain a symbol for the node.
        /// </summary>
        public ISymbol GetSymbol(SyntaxNode node)
        {
            // ReSharper disable once ConstantNullCoalescingCondition
            return _model.GetDeclaredSymbol(node) ?? _model.GetSymbolInfo(node).Symbol;
        }

        /// <summary>
        /// Returns the Python type of the given node.
        /// Returns an aliased typing.Any if there is no Python type for the given symbol.
        /// </summary>
        public CppType GetType(SyntaxNode node, bool skipCppTypeCheck = false)
        {
            var symbol = GetSymbol(node);

            if (symbol == null)
            {
                return node.ToFullString().Trim() switch
                {
                    "PyList" => new CppType("List", "typing")
                    {
                        TypeParameters = new List<CppType> {new CppType("Any", "typing")}
                    },
                    "PyDict" => new CppType("Dict", "typing")
                    {
                        TypeParameters = new List<CppType>
                        {
                            new CppType("Any", "typing"), new CppType("Any", "typing")
                        }
                    },
                    _ => new CppType("Any", "typing")
                };
            }

            return GetType(symbol, skipCppTypeCheck);
        }

        /// <summary>
        /// Returns the Python type of the given symbol.
        /// Returns an aliased typing.Any if there is no Python type for the given symbol.
        /// </summary>
        public CppType GetType(ISymbol symbol, bool skipCppTypeCheck = false)
        {
            // Handle arrays
            if (symbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                var listType = new CppType("List", "typing");
                listType.TypeParameters.Add((CppType)GetType(arrayTypeSymbol.ElementType));
                return listType;
            }

            // Use typing.Any as fallback if there is no type information in the given symbol
            if (symbol == null || symbol.Name == "" || symbol.ContainingNamespace == null)
            {
                return new CppType("Any", "typing");
            }

            var name = GetTypeName(symbol);
            var ns = symbol.ContainingNamespace.ToDisplayString();

            var type = new CppType(name, ns);

            // Process type parameters
            if (symbol is ITypeParameterSymbol typeParameterSymbol)
            {
                type.IsNamedTypeParameter = true;
            }

            // Process named type parameters
            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                // Delegates are not supported
                if (namedTypeSymbol.DelegateInvokeMethod != null
                    && !namedTypeSymbol.DelegateInvokeMethod.ToDisplayString().StartsWith("System.Func")
                    && !namedTypeSymbol.DelegateInvokeMethod.ToDisplayString().StartsWith("System.Action"))
                {
                    return new CppType("Any", "typing")
                    {
                        Alias = type.Namespace.Replace('.', '_') + "_" + type.Name.Replace('.', '_')
                    };
                }

                foreach (var typeParameter in namedTypeSymbol.TypeArguments)
                {
                    var paramType = GetType(typeParameter);

                    if (typeParameter is ITypeParameterSymbol)
                    {
                        paramType.IsNamedTypeParameter = true;
                    }

                    type.TypeParameters.Add((CppType)paramType);
                }
            }

            return (CppType)TypeToTargetLanguageType(type, skipCppTypeCheck);
        }

        public string GetTypeName(ISymbol symbol)
        {
            var nameParts = new List<string>();

            var currentSymbol = symbol;
            while (currentSymbol != null)
            {
                nameParts.Add(currentSymbol.Name);
                currentSymbol = currentSymbol.ContainingType;
            }

            nameParts.Reverse();

            if (symbol is ITypeParameterSymbol typeParameterSymbol)
            {
                if (typeParameterSymbol.DeclaringMethod != null)
                {
                    nameParts.Insert(1, typeParameterSymbol.DeclaringMethod.Name);
                }
            }

            return string.Join(".", nameParts);
        }

        /// <summary>
        /// Converts a C# type to a Python type.
        /// This method handles conversions like the one from System.String to str.
        /// If the Type object doesn't need to be converted, the originally provided type is returned.
        /// </summary>
        public CppType TypeToTargetLanguageType(CppType type, bool skipCppTypeCheck = false)
        {
            if (type.Namespace == "System" && !skipCppTypeCheck)
            {
                switch (type.Name)
                {
                    case "Char":
                    case "String":
                        return new CppType("str");
                    case "Byte":
                    case "SByte":
                    case "Int16":
                    case "Int32":
                    case "Int64":
                    case "UInt16":
                    case "UInt32":
                    case "UInt64":
                        return new CppType("int");
                    case "Single":
                    case "Double":
                    case "Decimal":
                        return new CppType("float");
                    case "Boolean":
                        return new CppType("bool");
                    case "Void":
                        return new CppType("None");
                    case "DateTime":
                        return new CppType("datetime", "datetime");
                    case "TimeSpan":
                        return new CppType("timedelta", "datetime");
                    case "Nullable":
                        type.Name = "Optional";
                        type.Namespace = "typing";
                        break;
                    case "Func":
                    case "Action":
                        if (type.TypeParameters.Count > 0)
                        {
                            type.IsAction = type.Name == "Action";
                            type.Name = "Callable";
                            type.Namespace = "typing";
                        }

                        break;
                    case "Type":
                        type.Name = "Type";
                        type.Namespace = "typing";
                        break;
                }
            }

            // C# types that don't have a Python-equivalent or that we don't parse are converted to an aliased Any
            if (type.Namespace == "<global namespace>")
            {
                var alias = type.Name.Replace('.', '_');
                if (type.Namespace.StartsWith("System"))
                {
                    alias = $"{type.Namespace.Replace('.', '_')}_{alias}";
                }

                return new CppType("Any", "typing")
                {
                    Alias = alias
                };
            }

            return type;
        }
    }
}
