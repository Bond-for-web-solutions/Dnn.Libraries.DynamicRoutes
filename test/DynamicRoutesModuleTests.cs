using NUnit.Framework;

namespace Dnn.Libraries.DynamicRoutes.Tests
{
    [TestFixture]
    public class DynamicRoutesModuleTests
    {
        [Test]
        public void DynamicRoutes_ImplementsIHttpModule()
        {
            var module = new global::Dnn.Libraries.DynamicRoutes.DynamicRoutes();
            Assert.IsInstanceOf<System.Web.IHttpModule>(module);
        }

        [Test]
        public void DynamicRoutesFix_ImplementsIHttpModule()
        {
            var fix = new global::Dnn.Libraries.DynamicRoutes.DynamicRoutesFix();
            Assert.IsInstanceOf<System.Web.IHttpModule>(fix);
        }

        [Test]
        public void DynamicRoutesFix_Dispose_DoesNotThrow()
        {
            var fix = new global::Dnn.Libraries.DynamicRoutes.DynamicRoutesFix();
            Assert.DoesNotThrow(() => fix.Dispose());
        }

        [Test]
        public void ItemRouteActive_ConstantIsAccessible()
        {
            // The internal const is used by DynamicRoutesFix to check if routing happened
            var field = typeof(global::Dnn.Libraries.DynamicRoutes.DynamicRoutes).GetField("ItemRouteActive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public);
            Assert.IsNotNull(field, "ItemRouteActive field should exist");
            Assert.AreEqual("RouteActive", field.GetValue(null));
        }
    }
}
