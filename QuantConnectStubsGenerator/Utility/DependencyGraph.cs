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
    public class DependencyGraph
    {
        private readonly IDictionary<PythonType, Class> _classes = new Dictionary<PythonType, Class>();

        private readonly AdjacencyGraph<PythonType, Edge<PythonType>> _graph =
            new AdjacencyGraph<PythonType, Edge<PythonType>>();

        public void AddClass(Class cls)
        {
            _classes[cls.Type] = cls;
            _graph.AddVertex(cls.Type);
        }

        public void AddDependency(Class cls, PythonType type)
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

            var edge = new Edge<PythonType>(cls.Type, type);
            _graph.AddEdge(edge);

            // We can't determine the best class order if there are cycles in their dependencies
            // If the new dependency creates a cycle, remove it
            if (!_graph.IsDirectedAcyclicGraph())
            {
                _graph.RemoveEdge(edge);
            }
        }

        public IEnumerable<Class> GetClassesInOrder()
        {
            return _graph.TopologicalSort().Select(type => _classes[type]).Reverse();
        }

        private PythonType GetParentType(PythonType type)
        {
            if (!type.Name.Contains("."))
            {
                return type;
            }

            return new PythonType(type.Name.Substring(0, type.Name.IndexOf('.')), type.Namespace)
            {
                Alias = type.Alias,
                IsNamedTypeParameter = type.IsNamedTypeParameter,
                TypeParameters = type.TypeParameters
            };
        }
    }
}
