using System.Reflection;
using NUnit.Framework;

namespace Dnn.Libraries.DynamicRoutes.Tests
{
    [TestFixture]
    public class DynamicRoutesHelperTests
    {
        private static readonly MethodInfo IsDynamicMethod =
            typeof(global::Dnn.Libraries.DynamicRoutes.DynamicRoutes).GetMethod("IsDynamic",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo ParamNameMethod =
            typeof(global::Dnn.Libraries.DynamicRoutes.DynamicRoutes).GetMethod("ParamName",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo IsSystemPrefixMethod =
            typeof(global::Dnn.Libraries.DynamicRoutes.DynamicRoutes).GetMethod("IsSystemPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);

        // ── IsDynamic ────────────────────────────────────────────────

        [Test]
        public void IsDynamic_BracketedName_ReturnsTrue()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "[community]" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsDynamic_PlainName_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "home" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_NullName_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { null });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_EmptyString_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_SingleBracket_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "[" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_TwoBrackets_ReturnsFalse()
        {
            // "[]" has length 2, which fails length > 2 check
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "[]" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_OnlyOpenBracket_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "[abc" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_OnlyCloseBracket_ReturnsFalse()
        {
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "abc]" });
            Assert.IsFalse(result);
        }

        [Test]
        public void IsDynamic_NestedBrackets_ReturnsTrue()
        {
            // "[a[b]]" starts with [ and ends with ], length > 2
            var result = (bool)IsDynamicMethod.Invoke(null, new object[] { "[a[b]]" });
            Assert.IsTrue(result);
        }

        // ── ParamName ────────────────────────────────────────────────

        [Test]
        public void ParamName_ExtractsBracketedContent()
        {
            var result = (string)ParamNameMethod.Invoke(null, new object[] { "[community]" });
            Assert.AreEqual("community", result);
        }

        [Test]
        public void ParamName_SingleChar_ExtractsSingleChar()
        {
            var result = (string)ParamNameMethod.Invoke(null, new object[] { "[x]" });
            Assert.AreEqual("x", result);
        }

        [Test]
        public void ParamName_LongName_ExtractsCorrectly()
        {
            var result = (string)ParamNameMethod.Invoke(null, new object[] { "[my-long-param-name]" });
            Assert.AreEqual("my-long-param-name", result);
        }

        // ── IsSystemPrefix ──────────────────────────────────────────

        [Test]
        public void IsSystemPrefix_Api_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "api" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSystemPrefix_Login_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "login" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSystemPrefix_Register_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "register" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSystemPrefix_Logoff_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "logoff" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSystemPrefix_TabId_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "tabid" });
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSystemPrefix_CaseInsensitive_ReturnsTrue()
        {
            var result = (bool)IsSystemPrefixMethod.Invoke(null, new object[] { "API" });
            Assert.IsTrue(result);
        }
    }
}
