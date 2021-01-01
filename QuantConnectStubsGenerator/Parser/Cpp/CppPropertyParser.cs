using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuantConnectStubsGenerator.Model;
using QuantConnectStubsGenerator.Utility;

namespace QuantConnectStubsGenerator.Parser
{
    public class CppPropertyParser : CppParser
    {
        public CppPropertyParser(ParseContext<CppType> context, SemanticModel model) : base(context, model)
        {
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            VisitProperty(node, TypeConverter.GetType(node.Type), node.Identifier.Text);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            VisitField(node, TypeConverter.GetType(node.Declaration.Type));
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            var type = new CppType("List", "typing")
            {
                TypeParameters = {TypeConverter.GetType(node.Type)}
            };

            VisitProperty(node, type, node.Identifier.Text);
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            var type = new CppType("List", "typing")
            {
                TypeParameters = {TypeConverter.GetType(node.Declaration.Type)}
            };

            VisitField(node, type);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            var property = new Property<CppType>(node.Identifier.Text)
            {
                Value = node.EqualsValue != null
                    ? FormatValue(node.EqualsValue.Value.ToString())
                    : _currentClass.Properties.Count.ToString(),
                Static = true,
                Abstract = _currentClass.Interface || HasModifier(node, "abstract")
            };

            var doc = ParseDocumentation(node);
            if (doc["summary"] != null)
            {
                property.Summary = doc["summary"].GetText();
            }

            _currentClass.Properties.Add(property);
        }

        private void VisitProperty(BasePropertyDeclarationSyntax node, CppType type, string name)
        {
            if (HasModifier(node, "private"))
            {
                return;
            }

            if (_currentClass == null)
            {
                return;
            }

            if (_currentClass.Properties.Any(p => p.Name == name))
            {
                return;
            }

            var originalType = type;
            var typeIsEnum = false;

            if (type.Namespace != null)
            {
                var ns = _context.HasNamespace(type.Namespace)
                    ? _context.GetNamespaceByName(type.Namespace)
                    : null;

                var cls = ns?.HasClass(type) == true ? ns.GetClassByType(type) : null;

                // Python.NET converts an enum return type to an int
                if (cls?.IsEnum() == true)
                {
                    type = new CppType("int");
                    typeIsEnum = true;
                }
            }

            // Security.Data is of type dynamic but can be used like it is of type DynamicSecurityData
            if (_currentClass.Type.ToLanguageString() == "QuantConnect.Securities.Security" && name == "Data")
            {
                type = new CppType("DynamicSecurityData", "QuantConnect.Securities");
            }

            var property = new Property<CppType>(name)
            {
                Type = type,
                ReadOnly = TypeConverter.GetSymbol(node) is IPropertySymbol symbol && symbol.IsReadOnly,
                Static = _currentClass.Static || HasModifier(node, "static"),
                Abstract = _currentClass.Interface || HasModifier(node, "abstract")
            };

            var doc = ParseDocumentation(node);
            if (doc["summary"] != null)
            {
                property.Summary = doc["summary"].GetText();
            }

            if (typeIsEnum)
            {
                property.Summary = AppendSummary(
                    property.Summary,
                    $"This property contains the int value of a member of the {originalType.ToLanguageString()} enum.");
            }

            if (HasModifier(node, "protected"))
            {
                property.Summary = AppendSummary(property.Summary, "This property is protected.");
            }

            _currentClass.Properties.Add(property);
        }

        private void VisitField(BaseFieldDeclarationSyntax node, CppType type)
        {
            if (HasModifier(node, "private"))
            {
                return;
            }

            if (_currentClass == null)
            {
                return;
            }

            foreach (var variable in node.Declaration.Variables)
            {
                var property = new Property<CppType>(variable.Identifier.Text)
                {
                    Type = type,
                    ReadOnly = HasModifier(node, "readonly") || HasModifier(node, "const"),
                    Static = _currentClass.Static || HasModifier(node, "static") || HasModifier(node, "const"),
                    Abstract = _currentClass.Interface || HasModifier(node, "abstract")
                };

                if (variable.Initializer != null)
                {
                    property.Value = FormatValue(variable.Initializer.Value.ToString());
                }

                var doc = ParseDocumentation(node);
                if (doc["summary"] != null)
                {
                    property.Summary = doc["summary"].GetText();
                }

                if (HasModifier(node, "protected"))
                {
                    property.Summary = AppendSummary(property.Summary, "This field is protected.");
                }

                _currentClass.Properties.Add(property);
            }
        }
    }
}
