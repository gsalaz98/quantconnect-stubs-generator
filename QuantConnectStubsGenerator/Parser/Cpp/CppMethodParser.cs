using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuantConnectStubsGenerator.Model;
using QuantConnectStubsGenerator.Utility;

namespace QuantConnectStubsGenerator.Parser
{
    public class CppMethodParser : CppParser
    {
        public CppMethodParser(ParseContext<CppType> context, SemanticModel model) : base(context, model)
        {
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            VisitMethod(
                node,
                node.Identifier.Text,
                node.ParameterList.Parameters,
                (CppType)TypeConverter.GetType(node.ReturnType));

            // Make the current class extend from typing.Iterable if this is an applicable GetEnumerator() method
            ExtendIterableIfNecessary(node);

            // Add __contains__ and __len__ methods to containers in System.Collections.Generic
            AddContainerMethodsIfNecessary(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (HasModifier(node, "static"))
            {
                return;
            }

            VisitMethod(node, "__init__", node.ParameterList.Parameters, new CppType("None"));
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            VisitMethod(
                node,
                node.Identifier.Text,
                node.ParameterList.Parameters,
                (CppType)TypeConverter.GetType(node.ReturnType));
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            var type = TypeConverter.GetType(node.Type);

            // Improve the autocompletion on data[symbol] if data is a Slice and symbol a Symbol
            // In C# this is of type dynamic, which by default gets converted to typing.Any
            // To improve the autocompletion a bit we convert it to Union[TradeBar, QuoteBar, List[Tick], Any]
            if (_currentClass?.Type.ToLanguageString() == "QuantConnect.Data.Slice")
            {
                type = new CppType("Union", "typing")
                {
                    TypeParameters =
                    {
                        new CppType("TradeBar", "QuantConnect.Data.Market"),
                        new CppType("QuoteBar", "QuantConnect.Data.Market"),
                        new CppType("List", "System.Collections.Generic")
                        {
                            TypeParameters = {new CppType("Tick", "QuantConnect.Data.Market")}
                        },
                        new CppType("Any", "typing")
                    }
                };
            }

            VisitMethod(node, "__getitem__", node.ParameterList.Parameters, type);

            var symbol = TypeConverter.GetSymbol(node);
            if (symbol is IPropertySymbol propertySymbol && !propertySymbol.IsReadOnly)
            {
                VisitMethod(node, "__setitem__", node.ParameterList.Parameters, new CppType("None"));

                var valueParameter = new Parameter<CppType>("value", type);
                _currentClass.Methods.Last().Parameters.Add(valueParameter);
            }
        }

        private void VisitMethod(
            MemberDeclarationSyntax node,
            string name,
            SeparatedSyntaxList<ParameterSyntax> parameterList,
            CppType returnType)
        {
            if (HasModifier(node, "private") || HasModifier(node, "internal"))
            {
                return;
            }

            if (_currentClass == null)
            {
                return;
            }

            // Some methods in the AST have parameters without names
            // Because these parameters cause syntax errors in the generated Python code we skip those methods
            if (parameterList.Any(parameter => FormatParameterName(parameter.Identifier.Text) == ""))
            {
                return;
            }


            var originalReturnType = returnType;
            var returnTypeIsEnum = false;

            if (returnType.Namespace != null)
            {
                var ns = _context.HasNamespace(returnType.Namespace)
                    ? _context.GetNamespaceByName(returnType.Namespace)
                    : null;

                var cls = ns?.HasClass(returnType) == true ? ns.GetClassByType(returnType) : null;

                // Python.NET converts an enum return type to an int
                if (cls?.IsEnum() == true)
                {
                    returnType = new CppType("int");
                    returnTypeIsEnum = true;
                }
            }

            var method = new Method<CppType>(name, returnType)
            {
                Static = HasModifier(node, "static"),
                File = _model.SyntaxTree.FilePath
            };

            var doc = ParseDocumentation(node);
            if (doc["summary"] != null)
            {
                method.Summary = doc["summary"].GetText();
            }

            if (HasModifier(node, "protected"))
            {
                method.Summary = AppendSummary(method.Summary, "This method is protected.");
            }

            var docStrings = new List<string>();

            foreach (var parameter in parameterList)
            {
                var parsedParameter = ParseParameter(parameter, method.Name);

                if (parsedParameter == null)
                {
                    continue;
                }

                foreach (XmlElement paramNode in doc.GetElementsByTagName("param"))
                {
                    if (paramNode.Attributes["name"]?.Value != parameter.Identifier.Text)
                    {
                        continue;
                    }

                    var text = paramNode.GetText();

                    if (text.Trim().Length == 0)
                    {
                        continue;
                    }

                    if (CheckDocSuggestsPandasDataFrame(text))
                    {
                        parsedParameter.Type = new CppType("DataFrame", "pandas");
                    }

                    docStrings.Add($":param {parsedParameter.Name}: {text}");
                    break;
                }

                method.Parameters.Add(parsedParameter);
            }

            var returnsParts = new List<string>();

            if (doc["returns"] != null)
            {
                var text = doc["returns"].GetText();

                if (text.Trim().Length > 0)
                {
                    returnsParts.Add(text);
                }
            }

            if (returnTypeIsEnum)
            {
                returnsParts.Add(
                    $"This method returns the int value of a member of the {originalReturnType.ToLanguageString()} enum.");
            }

            if (returnsParts.Count > 0)
            {
                var parts = returnsParts.Select(part => part.EndsWith(".") ? part : part + ".");
                var text = string.Join(" ", parts);

                docStrings.Add($":returns: {text}");

                if (CheckDocSuggestsPandasDataFrame(text))
                {
                    method.ReturnType = new CppType("DataFrame", "pandas");
                }
            }

            docStrings = docStrings.Select(str => str.Replace('\n', ' ')).ToList();

            if (docStrings.Count > 0)
            {
                var paramText = string.Join("\n", docStrings);
                method.Summary = method.Summary != null
                    ? method.Summary + "\n\n" + paramText
                    : paramText;
            }

            _currentClass.Methods.Add(method);

            ImprovePythonAccessorIfNecessary(method);
        }

        private Parameter<CppType> ParseParameter(ParameterSyntax syntax, string methodName)
        {
            var originalName = syntax.Identifier.Text;
            var parameter = new Parameter<CppType>(FormatParameterName(originalName), TypeConverter.GetType(syntax.Type));

            if (syntax.Modifiers.Any(modifier => modifier.Text == "params"))
            {
                parameter.VarArgs = true;
                parameter.Type = parameter.Type.TypeParameters[0];
            }

            // Symbol parameters can be both a Symbol or a string in most methods
            if (parameter.Type.Namespace == "QuantConnect" && parameter.Type.Name == "Symbol")
            {
                var unionType = new CppType("Union", "typing");
                unionType.TypeParameters.Add(parameter.Type);
                unionType.TypeParameters.Add(new CppType("str"));
                parameter.Type = unionType;
            }

            // System.Object parameters can accept anything
            if (parameter.Type.Namespace == "System" && parameter.Type.Name == "Object")
            {
                parameter.Type = new CppType("Any", "typing");
            }

            if (syntax.Default != null)
            {
                parameter.Value = FormatValue(syntax.Default.Value.ToString());
            }

            return parameter;
        }

        private string FormatParameterName(string name)
        {
            // Remove "@" prefix
            if (name.StartsWith("@"))
            {
                name = name.Substring(1);
            }

            // Escape keywords
            return name switch
            {
                "from" => "_from",
                "enum" => "_enum",
                "lambda" => "_lambda",
                _ => name
            };
        }

        private void ExtendIterableIfNecessary(MethodDeclarationSyntax node)
        {
            if (node.Identifier.Text != "GetEnumerator")
            {
                return;
            }

            if (_currentClass.InheritsFrom.Any(t => t.Namespace == "typing" && t.Name == "Iterable"))
            {
                return;
            }

            var parsedReturnType = TypeConverter.GetType(node.ReturnType);

            // Some GetEnumerator() methods return an IEnumerator, some return an IEnumerator<T>
            // typing.Iterable requires a type parameter, so we don't extend it if an IEnumerator is returned
            if (parsedReturnType.TypeParameters.Count == 0)
            {
                return;
            }

            _currentClass.InheritsFrom.Add(new CppType("Iterable", "typing")
            {
                TypeParameters = parsedReturnType.TypeParameters
            });
        }

        private void AddContainerMethodsIfNecessary(MethodDeclarationSyntax node)
        {
            if (node.Identifier.Text != "Contains" && node.Identifier.Text != "ContainsKey")
            {
                return;
            }

            if (_currentNamespace.Name != "System.Collections.Generic")
            {
                return;
            }

            VisitMethod(node, "__contains__", node.ParameterList.Parameters, TypeConverter.GetType(node.ReturnType));

            if (_currentClass.Methods.All(m => m.Name != "__len__"))
            {
                _currentClass.Methods.Add(new Method<CppType>("__len__", new CppType("int")));
            }
        }

        /// <summary>
        /// There are several Python-friendly accessors like Slice.Get(Type) instead of Slice.Get&lt;T&gt;().
        /// If we spot such a Python-friendly accessor, we remove the non-Python-friendly accessor and improve
        /// the Python-friendly accessor's definition.
        /// </summary>
        private void ImprovePythonAccessorIfNecessary(Method<CppType> newMethod)
        {
            if (newMethod.Parameters.Count != 1 || newMethod.Parameters[0].Type.ToLanguageString() != "typing.Type")
            {
                return;
            }

            var existingMethod = _currentClass.Methods
                .FirstOrDefault(m => m.Name == newMethod.Name && m.Parameters.Count == 0);

            if (existingMethod == null)
            {
                return;
            }

            var typeParameter = existingMethod.ReturnType;
            while (!typeParameter.IsNamedTypeParameter && typeParameter.TypeParameters.Count > 0)
            {
                typeParameter = typeParameter.TypeParameters[0];
            }

            if (!typeParameter.IsNamedTypeParameter)
            {
                return;
            }

            newMethod.Parameters[0].Type = typeParameter;
            newMethod.ReturnType = existingMethod.ReturnType;

            _currentClass.Methods.Remove(existingMethod);
        }

        /// <summary>
        /// Returns whether the provided documentation string suggests that a certain type is a pandas DataFrame.
        /// </summary>
        private bool CheckDocSuggestsPandasDataFrame(string doc)
        {
            return doc.Contains("pandas DataFrame") || doc.Contains("pandas.DataFrame");
        }
    }
}
