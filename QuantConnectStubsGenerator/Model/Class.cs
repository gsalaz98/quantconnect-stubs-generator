using System.Collections.Generic;
using System.Linq;

namespace QuantConnectStubsGenerator.Model
{
    public class Class<T>
        where T : ILanguageType<T>, new()
    {
        public T Type { get; }

        public string Summary { get; set; }

        public bool Static { get; set; }
        public bool Interface { get; set; }

        public IList<T> InheritsFrom { get; set; } = new List<T>();
        public T MetaClass { get; set; }

        public Class<T> ParentClass { get; set; }
        public IList<Class<T>> InnerClasses { get; } = new List<Class<T>>();

        public IList<Property<T>> Properties { get; } = new List<Property<T>>();
        public IList<Method<T>> Methods { get; } = new List<Method<T>>();

        public Class(T type)
        {
            Type = type;
        }

        public bool IsEnum()
        {
            return InheritsFrom.Any(type => type.ToLanguageString() == "System.Enum");
        }

        public IEnumerable<T> GetUsedTypes()
        {
            var types = new HashSet<T>();

            // Parse types recursively to properly return deep generics
            var typesToProcess = new Queue<T>(GetUsedTypesToIterateOver());

            while (typesToProcess.Count > 0)
            {
                var currentType = typesToProcess.Dequeue();

                types.Add(currentType);

                foreach (var typeParameter in currentType.TypeParameters)
                {
                    typesToProcess.Enqueue(typeParameter);
                }
            }

            // Python classes with type parameters always extend typing.Generic[T, ...] where T = typing.TypeVar('T')
            if (Type.TypeParameters.Count > 0)
            {
                types.Add(new T().New("Generic", "typing"));
                types.Add(new T().New("TypeVar", "typing"));
            }

            // PropertyRenderer adds the @abc.abstractmethod decorator to abstract properties
            if (Properties.Any(p => !p.Static && p.Abstract))
            {
                types.Add(new T().New("abstractmethod", "abc"));
            }

            // MethodRenderer adds the @typing.overload decorator to overloaded methods
            if (Methods.Any(m => m.Overload))
            {
                types.Add(new T().New("overload", "typing"));
            }

            foreach (var innerClass in InnerClasses)
            {
                foreach (var usedType in innerClass.GetUsedTypes())
                {
                    types.Add(usedType);
                }
            }

            return types;
        }

        /// <summary>
        /// Returns the used types which need to be recursively iterated over in GetUsedTypes().
        /// </summary>
        public IEnumerable<T> GetUsedTypesToIterateOver()
        {
            yield return Type;

            foreach (var inheritedType in InheritsFrom)
            {
                yield return inheritedType;
            }

            if (MetaClass != null)
            {
                yield return MetaClass;
            }

            foreach (var property in Properties)
            {
                if (property.Type != null)
                {
                    yield return property.Type;
                }
            }

            foreach (var method in Methods)
            {
                yield return method.ReturnType;

                foreach (var parameter in method.Parameters)
                {
                    yield return parameter.Type;
                }
            }
        }
    }
}
