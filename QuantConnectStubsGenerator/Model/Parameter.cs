namespace QuantConnectStubsGenerator.Model
{
    public class Parameter<T>
        where T : ILanguageType<T>
    {
        public string Name { get; }
        public T Type { get; set; }

        public bool VarArgs { get; set; }
        public string Value { get; set; }

        public Parameter(string name, T type)
        {
            Name = name;
            Type = type;
        }
    }
}
