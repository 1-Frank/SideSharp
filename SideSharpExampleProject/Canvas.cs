using Attribute_Collection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideSharpExampleProject
{
    public static class Program
    {
        public static void Main()
        {
            CanvasWithAOP canvasWithAOP = new CanvasWithAOP();
            canvasWithAOP.AddDrawObjectThreadSafe(new CanvasRectangle());
            foreach (Type item in CanvasWithAOP.PossibleDrawObjects)
            {
                Console.WriteLine($"Available Export:{item}");
            }
        }
    }

    public class CanvasWithAOP
    {
        static CanvasWithAOP() { }
        public List<IDrawObject> DrawObjects { get; set; } = new List<IDrawObject>();
        public Size Size { get; set; }

        [ImplementingTypesSetter(typeof(IDrawObject))]
        public static IEnumerable<Type> PossibleDrawObjects { get; set; } = new List<Type>();
        public void AddDrawObjectThreadSafe(IDrawObject obj)
        {
            DrawObjects.Add(obj);
            //throw new Exception("digga");
        }

        public override string ToString()
        {
            return Size.ToString() + " - ";
        }
    }

    public class CanvasWithoutAOP
    {
        public List<IDrawObject> DrawObjects { get; set; }
        public Size Size { get; set; }

    }

    public interface IDrawObject
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string paint();
    }

    public class CanvasCircle : IDrawObject
    {
        public int X { get; set; }
        public int Y { get; set; }

        public string paint()
        {
            return "O";
        }
    }

    public class CanvasRectangle : IDrawObject
    {
        public int X { get; set; }
        public int Y { get; set; }

        public string paint()
        {
            return "|_|";
        }
    }

    public class LockingMethodAspect : MethodInterceptor
    {
        public override void Run()
        {
            {
                try
                {
                    RunOriginalMethod();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
