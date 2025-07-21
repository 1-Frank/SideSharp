using System;

namespace Attribute_Collection
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class MethodInterceptor : Attribute
    {
        public static object[] OriginalMethodParameters { get; }
        public static object OriginalReturnValue { get; }
        public static void RunOriginalMethod() { }
        public abstract void Run();
    }
}