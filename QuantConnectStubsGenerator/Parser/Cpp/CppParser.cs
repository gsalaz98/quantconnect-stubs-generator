using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Parser
{
    /// <summary>
    /// Python parser
    /// </summary>
    public class CppParser : BaseParser<CppType>
    {
        protected CppParser(ParseContext<CppType> context, SemanticModel model) : base(context, model)
        {
            TypeConverter = new CppTypeConverter(model);
        }

        protected override string FormatValue(string value)
        {
            // null to None
            if (value == "null")
            {
                return "None";
            }

            // Boolean true
            if (value == "true")
            {
                return "True";
            }

            // Boolean false
            if (value == "false")
            {
                return "False";
            }

            // Numbers
            if (Regex.IsMatch(value, "^-?[0-9.]+m?$"))
            {
                // If the value is a number, remove a potential suffix like "m" in 1.0m
                if (value.EndsWith("m"))
                {
                    return value.Substring(0, value.Length - 1);
                }

                return value;
            }

            // Strings
            if (Regex.IsMatch(value, "^@?\"[^\"]+\"$"))
            {
                if (value.StartsWith("@"))
                {
                    value = value.Substring(1);
                }

                // Escape backslashes
                value = value.Replace("\\", "\\\\");

                return value;
            }

            return "...";
        }
    }
}
