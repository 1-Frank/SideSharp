using Attribute_Collection;

namespace TestSideSharp.MethodInterceptorTests
{
    public class LogParameters : MethodInterceptor
    {
        public static List<object> LOG = [];
        public override void Run()
        {
            LogParameters.LOG.Clear();
            LogParameters.LOG.AddRange(OriginalMethodParameters);
            RunOriginalMethod();
        }
    }
    public class LogParameterTest
    {
        [LogParameters]
        public void doNothing(double val1, string val2, object val3)
        {
            _ = 0;
        }

        [Test]
        public void TestParameterWriting()
        {
            object[] objects = { 10.0, "asd", new() };
            doNothing((double)objects[0], (string)objects[1], objects[2]);
            Assert.IsTrue(objects.SequenceEqual(LogParameters.LOG.ToArray()));
        }
    }
}
