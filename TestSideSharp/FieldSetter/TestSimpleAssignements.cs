using Attribute_Collection;

namespace TestSideSharp.FieldSetter
{
    public class StaticStringTest : FieldSetter<string>
    {
        public override string GetValue => "TestValue";
    }

    public class TestSimpleAssignements
    {
        [StaticStringTest]
        public static string StaticStringTestField;
        [StaticStringTest]
        public static string StaticStringTestProperty { get; set; }

        [Test]
        public void StaticPropertyStringAssignement()
        {
            Assert.IsTrue(StaticStringTestProperty == "TestValue");
        }
        [Test]
        public void StaticFieldStringAssignement()
        {
            Assert.IsTrue(StaticStringTestField == "TestValue");
        }
    }
}
