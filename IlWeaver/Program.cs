using Mono.Cecil;
using Mono.Cecil.Pdb;
using System.Diagnostics;

namespace IlWeaver
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            // Search all IlWeavers
            List<IlWeavingProcessor> processorList = AppDomain.CurrentDomain.GetAssemblies()
                                                    .SelectMany(s => s.GetTypes())
                                                    .Where(p => typeof(IlWeavingProcessor).IsAssignableFrom(p) && p.IsClass)
                                                    .Select(Activator.CreateInstance)
                                                    .OfType<IlWeavingProcessor>()
                                                    .ToList();


            if (args.Count() == 0)
            {
                args = new List<string>() { "D:\\SideSharp\\Test_Project\\bin\\Debug\\net8.0\\Test_Project.dll" }.ToArray();
            }

            foreach (string assemblyPath in args)
            {
                #region Weave IL Code
                using FileStream stream = new(assemblyPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                #region ReadAssembly
                AssemblyDefinition? assemblyDefinition = null;
                bool doesPdbExist = File.Exists(assemblyPath.Replace(".dll", ".pdb").Replace(".exe", ".pdb"));
                assemblyDefinition = doesPdbExist
                    ? AssemblyDefinition.ReadAssembly(stream, new ReaderParameters() { ReadSymbols = true, InMemory = true })
                    : AssemblyDefinition.ReadAssembly(stream);
                #endregion
                foreach (IlWeavingProcessor processor in processorList)
                {
                    assemblyDefinition = processor.InjectILCode(assemblyDefinition);
                }

                #region WriteAssembly
                stream.Position = 0;
                if (doesPdbExist)
                {
                    assemblyDefinition.Write(stream, new WriterParameters() {  SymbolWriterProvider = new PdbWriterProvider(), WriteSymbols = true });
                }
                else
                {
                    assemblyDefinition.Write(stream);
                }
                #endregion
                #endregion
            }
            Console.WriteLine("Injection took " + stopwatch.ElapsedMilliseconds);
        }
    }

    public interface IlWeavingProcessor
    {
        AssemblyDefinition InjectILCode(AssemblyDefinition assemblyDefinition);
    }
}
