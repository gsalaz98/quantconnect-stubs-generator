using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnectStubsGenerator.Model
{
    public class Namespace<T>
        where T : ILanguageType<T>, new()
    {
        public string Name { get; }

        private readonly IDictionary<string, Class<T>> _classes = new Dictionary<string, Class<T>>();

        public Namespace(string name)
        {
            Name = name;
        }

        public IEnumerable<Class<T>> GetClasses()
        {
            return _classes.Values;
        }

        public IEnumerable<Class<T>> GetParentClasses()
        {
            return _classes.Values.Where(cls => cls.ParentClass == null);
        }

        public Class<T> GetClassByType(T type)
        {
            var key = GetClassKey(type);

            if (_classes.ContainsKey(key))
            {
                return _classes[key];
            }

            throw new ArgumentException($"No class has been registered with type '{type.ToLanguageString()}'");
        }

        public bool HasClass(T type)
        {
            return _classes.ContainsKey(GetClassKey(type));
        }

        public void RegisterClass(Class<T> cls)
        {
            _classes[GetClassKey(cls)] = cls;
        }

        private string GetClassKey(T type)
        {
            return $"{type.Namespace}.{type.Name}";
        }

        private string GetClassKey(Class<T> cls)
        {
            return GetClassKey(cls.Type);
        }
    }
}
