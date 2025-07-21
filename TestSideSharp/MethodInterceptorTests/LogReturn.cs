using Attribute_Collection;

namespace TestSideSharp.MethodInterceptorTests
{
    public class LogReturn : MethodInterceptor
    {
        public static List<string> LOG = [];
        public override void Run()
        {
            RunOriginalMethod();
            LOG.Add(OriginalReturnValue?.ToString() ?? "");
        }
    }

    public class LogReturnTest
    {
        [LogReturn]
        public int GetNumberOne()
        {
            return 1;
        }
        [Test]
        public void TestGetNumber()
        {
            _ = GetNumberOne();
            Assert.IsTrue(LogReturn.LOG.LastOrDefault() == "1");
        }
    }
}
