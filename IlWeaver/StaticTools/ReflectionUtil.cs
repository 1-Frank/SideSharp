using Mono.Cecil;
using System.Reflection;

namespace IlWeaver.StaticTools
{
    public static class ReflectionUtil
    {
        public static List<string> GetAllAttributesAssignableFrom(AssemblyDefinition assembly, Type baseAspect)
        {
            // Finding the predefined aspects from the Attribute_Collection
            Assembly? baseAssembly = Assembly.GetAssembly(baseAspect);
            IEnumerable<string?>? baseAssemblyTypes = baseAssembly?.GetTypes()
                .Where(x =>
                baseAspect.IsAssignableFrom(x) &&
                !x.IsInterface &&
                !x.IsAbstract).Select(x => x.FullName);

            // Finding the user defined aspects from the passed assembly
            IEnumerable<string> allMethodInterceptorTypes = assembly.Modules
                .SelectMany(module => module.Types
                    .Where(type => !type.IsAbstract && !type.IsInterface && IsBaseTypeRecursive(type, baseAspect))
                    .Select(type => type.FullName));

            return baseAssemblyTypes != null ? [.. allMethodInterceptorTypes.Union(baseAssemblyTypes)] : allMethodInterceptorTypes.ToList();
        }

        public static bool IsBaseTypeRecursive(TypeDefinition type, Type baseType)
        {
            return type.BaseType != null && (type.BaseType.FullName == baseType.FullName || IsBaseTypeRecursive(type.BaseType.Resolve(), baseType));
        }
    }
}