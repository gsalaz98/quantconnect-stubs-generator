using NUnit.Framework;
using QuantConnectStubsGenerator.Model;

namespace QuantConnectStubsGenerator.Tests.Model
{
    [TestFixture]
    public class PythonTypeTests
    {
        [Test]
        public void ToPythonStringCorrectlyFormatsAlias()
        {
            var type = new PythonType("Any", "typing")
            {
                Alias = "AnyAlias"
            };

            Assert.AreEqual("AnyAlias", type.ToLanguageString());
        }

        [Test]
        public void ToPythonStringCorrectlyIgnoresAlias()
        {
            var type = new PythonType("Any", "typing")
            {
                Alias = "AnyAlias"
            };

            Assert.AreEqual("typing.Any", type.ToLanguageString(true));
        }

        [Test]
        public void ToPythonStringCorrectlyFormatsNamedTypeParameter()
        {
            var type = new PythonType("MyClass.TKey", "QuantConnect.Data")
            {
                IsNamedTypeParameter = true
            };

            Assert.AreEqual("QuantConnect_Data_MyClass_TKey", type.ToLanguageString());
        }

        [Test]
        public void ToPythonStringCorrectlyAddsNamespace()
        {
            var type = new PythonType("MyClass", "QuantConnect");

            Assert.AreEqual("QuantConnect.MyClass", type.ToLanguageString());
        }

        [Test]
        public void ToPythonStringOmitsNamespaceWhenNamespaceIsNull()
        {
            var type = new PythonType("MyClass");

            Assert.AreEqual("MyClass", type.ToLanguageString());
        }

        [Test]
        public void ToPythonStringCorrectlyFormatsTypeParameters()
        {
            var type = new PythonType("MyClass", "QuantConnect");
            type.TypeParameters.Add(new PythonType("MyOtherClass", "QuantConnect"));
            type.TypeParameters.Add(new PythonType("MyOtherClass2", "QuantConnect"));

            Assert.AreEqual(
                "QuantConnect.MyClass[QuantConnect.MyOtherClass, QuantConnect.MyOtherClass2]",
                type.ToLanguageString());
        }

        [Test]
        public void ToPythonStringCorrectlyFormatsCallable()
        {
            var type = new PythonType("Callable", "typing");
            type.TypeParameters.Add(new PythonType("str"));
            type.TypeParameters.Add(new PythonType("str"));
            type.TypeParameters.Add(new PythonType("str"));

            Assert.AreEqual("typing.Callable[[str, str], str]", type.ToLanguageString());
        }
    }
}
