using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Renderer
{
    public class PythonClassRenderer : PythonObjectRenderer<Class<PythonType>>
    {
        public PythonClassRenderer(StreamWriter writer, int indentationLevel) : base(writer, indentationLevel)
        {
        }

        public override void Render(Class<PythonType> cls)
        {
            RenderClassHeader(cls);
            RenderInnerClasses(cls);
            RenderProperties(cls);
            RenderMethods(cls);
        }

        private void RenderClassHeader(Class<PythonType> cls)
        {
            Write($"class {cls.Type.Name.Split(".").Last()}");

            var inherited = new List<string>();

            if (cls.Type.TypeParameters.Count > 0)
            {
                var types = cls.Type.TypeParameters.Select(type => type.ToLanguageString());
                inherited.Add($"typing.Generic[{string.Join(", ", types)}]");
            }

            foreach (var inheritedType in cls.InheritsFrom)
            {
                inherited.Add(inheritedType.ToLanguageString());
            }

            if (cls.MetaClass != null)
            {
                inherited.Add($"metaclass={cls.MetaClass.ToLanguageString()}");
            }

            if (inherited.Count > 0)
            {
                Write($"({string.Join(", ", inherited)})");
            }

            WriteLine(":");

            WriteSummary(cls.Summary ?? "This class has no documentation.", true);
            WriteLine();
        }

        private void RenderInnerClasses(Class<PythonType> cls)
        {
            var classRenderer = CreateRenderer<PythonClassRenderer>();

            foreach (var innerClass in cls.InnerClasses)
            {
                classRenderer.Render(innerClass);
            }
        }

        private void RenderProperties(Class<PythonType> cls)
        {
            var propertyRenderer = CreateRenderer<PythonPropertyRenderer>();

            foreach (var property in cls.Properties)
            {
                propertyRenderer.Render(property);
            }
        }

        private void RenderMethods(Class<PythonType> cls)
        {
            var methodRenderer = CreateRenderer<PythonMethodRenderer>();

            foreach (var method in cls.Methods)
            {
                methodRenderer.Render(method);
            }
        }
    }
}
