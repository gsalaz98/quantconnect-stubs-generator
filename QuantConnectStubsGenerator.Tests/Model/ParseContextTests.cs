using System;
using System.Linq;
using NUnit.Framework;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Tests.Model
{
    [TestFixture]
    public class ParseContextTests
    {
        [Test]
        public void GetNamespacesShouldReturnAllRegisteredNamespaces()
        {
            var context = new ParseContext<PythonType>();

            var ns1 = new Namespace<PythonType>("ns1");
            var ns2 = new Namespace<PythonType>("ns2");

            context.RegisterNamespace(ns1);
            context.RegisterNamespace(ns2);

            var namespaces = context.GetNamespaces().ToList();

            Assert.AreEqual(2, namespaces.Count);
            Assert.IsTrue(namespaces.Contains(ns1));
            Assert.IsTrue(namespaces.Contains(ns2));
        }

        [Test]
        public void GetNamespaceByNameShouldReturnNamespaceIfItHasBeenRegistered()
        {
            var context = new ParseContext<PythonType>();
            var ns = new Namespace<PythonType>("Test");

            context.RegisterNamespace(ns);

            Assert.AreEqual(ns, context.GetNamespaceByName(ns.Name));
        }

        [Test]
        public void GetNamespaceByNameShouldThrowIfNamespaceHasNotBeenRegistered()
        {
            var context = new ParseContext<PythonType>();

            Assert.Throws<ArgumentException>(() => context.GetNamespaceByName("Test"));
        }

        [Test]
        public void HasNamespaceShouldReturnTrueIfNamespaceHasBeenRegistered()
        {
            var context = new ParseContext<PythonType>();
            var ns = new Namespace<PythonType>("Test");
            context.RegisterNamespace(ns);

            Assert.IsTrue(context.HasNamespace(ns.Name));
        }

        [Test]
        public void HasNamespaceShouldReturnFalseIfNamespaceHasNotBeenRegistered()
        {
            var context = new ParseContext<PythonType>();

            Assert.IsFalse(context.HasNamespace("Test"));
        }
    }
}
