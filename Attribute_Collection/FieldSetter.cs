using System;

namespace Attribute_Collection
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    // Exists to make attribute search faster and to allow different FieldSetter implementations in the future.
    public abstract class FieldSetter : Attribute
    {

    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public abstract class FieldSetter<T> : FieldSetter
    {
        public abstract T GetValue { get; }
    }
}
