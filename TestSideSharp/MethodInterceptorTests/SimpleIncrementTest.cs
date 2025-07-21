namespace TestSideSharp.MethodInterceptorTests
{
    public class CounterMethodInterceptor : Attribute_Collection.MethodInterceptor
    {
        public override void Run()
        {
            RunOriginalMethod();
            SimpleIncrementTest.number++;
        }
    }
    public class SimpleIncrementTest
    {
        public static int number = 0;

        [CounterMethodInterceptor]
        public void TestMethod(int a)
        {
            number = a;
        }

        [Test]
        public void TestSimpleMethodInterceptor()
        {
            TestMethod(10);
            Assert.True(number == 11);
        }
    }
}
