using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnectStubsGenerator.Model;
using QuikGraph;
using QuikGraph.Algorithms;

namespace QuantConnectStubsGenerator.Utility
{
    /// <summary>
    /// The DependencyGraph is used by the NamespaceRenderer as a sort of queue
    /// to render classes in such order to limit the amount of forward references.
    /// </summary>
    public class DependencyGraph<T>
        where T : ILanguageType<T>, new()
    {
        private readonly IDictionary<T, Class<T>> _classes = new Dictionary<T, Class<T>>();

        private readonly AdjacencyGraph<T, Edge<T>> _graph =
            new AdjacencyGraph<T, Edge<T>>();

        public void AddClass(Class<T> cls)
        {
            _classes[cls.Type] = cls;
            _graph.AddVertex(cls.Type);
        }

        public void AddDependency(Class<T> cls, T type)
        {
            if (!_classes.ContainsKey(cls.Type))
            {
                throw new ArgumentException($"'{cls.Type.ToLanguageString()}' has not been registered using AddClass");
            }

            type = GetParentType(type);

            // Classes can't depend on themselves
            if (Equals(cls.Type, type))
            {
                return;
            }

            // Only dependencies between the registered classes are considered
            if (!_classes.ContainsKey(type))
            {
                return;
            }

            var edge = new Edge<T>(cls.Type, type);
            _graph.AddEdge(edge);

            // We can't determine the best class order if there are cycles in their dependencies
            // If the new dependency creates a cycle, remove it
            if (!_graph.IsDirectedAcyclicGraph())
            {
                _graph.RemoveEdge(edge);
            }
        }

        public IEnumerable<Class<T>> GetClassesInOrder()
        {
            return _graph.TopologicalSort().Select(type => _classes[type]).Reverse();
        }

        private T GetParentType(T type)
        {
            if (!type.Name.Contains("."))
            {
                return type;
            }

            var t = new T().New(type.Name.Substring(0, type.Name.IndexOf('.')), type.Namespace);
            t.Alias = type.Alias;
            t.IsNamedTypeParameter = type.IsNamedTypeParameter;
            t.TypeParameters = type.TypeParameters;

            return t;
        }
    }
}
