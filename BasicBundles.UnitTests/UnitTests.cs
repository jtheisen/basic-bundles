using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IronStone.Web.BasicBundles.Testing
{
    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void RunAllTests()
        {
            Tests.RunTests();
        }
    }
}
