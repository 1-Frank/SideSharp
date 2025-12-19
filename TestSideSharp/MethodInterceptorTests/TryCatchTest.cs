using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSideSharp.MethodInterceptorTests
{
    public class TryCatchMethod : Attribute_Collection.MethodInterceptor
    {
        public override void Run()
        {
            try
            {
                RunOriginalMethod();
            }
            catch (Exception ex)
            {
                Console.WriteLine("sasdasdasdasd");
            }
        }
    }

    public static class TryCatchTest
    {
        [TryCatchMethod]
        public static void Test()
        {
            Console.WriteLine("asdasdss");
            throw new Exception("Test");
        }
        [Test]
        public static void TestTryCatch()
        {
            Test();
        }
    }
}
