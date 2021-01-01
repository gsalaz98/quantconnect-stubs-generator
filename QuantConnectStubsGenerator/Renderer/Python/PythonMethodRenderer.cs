using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuantConnectStubsGenerator.Model;
using QuantConnectStubsGenerator.Utility;

namespace QuantConnectStubsGenerator.Renderer
{
    public class PythonMethodRenderer : PythonObjectRenderer<Method<PythonType>>
    {
        public PythonMethodRenderer(StreamWriter writer, int indentationLevel) : base(writer, indentationLevel)
        {
        }

        public override void Render(Method<PythonType> method)
        {
            if (method.Static)
            {
                WriteLine("@staticmethod");
            }

            if (method.Overload)
            {
                WriteLine("@typing.overload");
            }

            // In C# abstract methods and method overloads can be mixed freely
            // In Python this is not the case, overloaded abstract methods or
            // overloaded methods of which only some are abstract are not parsed the same in Python
            // For this reason @abc.abstractmethod is not added to abstract methods

            var args = new List<string>();

            if (!method.Static)
            {
                args.Add("self");
            }

            args.AddRange(method.Parameters.Select(ParameterToString));
            var argsStr = string.Join(", ", args);

            WriteLine($"def {method.Name}({argsStr}) -> {method.ReturnType.ToLanguageString()}:");
            WriteSummary(method.Summary, true);
            WriteLine("...".Indent());

            WriteLine();
        }

        private string ParameterToString(Parameter<PythonType> parameter)
        {
            var str = $"{parameter.Name}: {parameter.Type.ToLanguageString()}";

            if (parameter.VarArgs)
            {
                str = "*" + str;
            }

            if (parameter.Value != null)
            {
                str += $" = {parameter.Value}";
            }

            return str;
        }
    }
}
