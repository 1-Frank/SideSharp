
using Attribute_Collection;
using System.Drawing;

namespace SideSharpExampleProject
{
    public class InitializeSizeAttribute : FieldSetter<Size>
    {
        private Size _size;
        public InitializeSizeAttribute(int width, int height)
        {
            _size = new Size(width, height);
        }
        public override Size GetValue => _size;
    }
}