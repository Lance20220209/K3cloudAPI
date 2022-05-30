using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnyCubicK3cloudProject;

namespace UnitTestProject2
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            //我在vs进行修wo 的订单22
            ZWLF_CreateErpOrderTest zWLF_CreateErpOrderTest = new ZWLF_CreateErpOrderTest();
            zWLF_CreateErpOrderTest.CreateErpOrderTest();

        }
    }
}
