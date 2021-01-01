using System.Collections.Generic;

namespace QuantConnectStubsGenerator.Model
{
    public class Method<T>
        where T : ILanguageType<T>
    {
        public string Name { get; }
        public T ReturnType { get; set; }

        public bool Static { get; set; }
        public bool Overload { get; set; }

        public string Summary { get; set; }

        public string File { get; set; }

        public IList<Parameter<T>> Parameters { get; } = new List<Parameter<T>>();

        public Method(string name, T returnType)
        {
            Name = name;
            ReturnType = returnType;
        }
    }
}
