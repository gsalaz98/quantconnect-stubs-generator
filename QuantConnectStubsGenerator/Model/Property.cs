namespace QuantConnectStubsGenerator.Model
{
    public class Property<T>
        where T : ILanguageType<T>
    {
        public string Name { get; }
        public T Type { get; set; }

        public bool ReadOnly { get; set; }
        public bool Static { get; set; }
        public bool Abstract { get; set; }

        public string Value { get; set; }

        public string Summary { get; set; }

        public Property(string name)
        {
            Name = name;
        }
    }
}
