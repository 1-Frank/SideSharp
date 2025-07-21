using Attribute_Collection;
using IlWeaver.StaticTools;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;

namespace IlWeaver.IlWeavingProcessors
{
    /// <summary>
    /// This Code is run during compilation
    /// </summary>
    public class LinkFieldSetter : IlWeavingProcessor
    {
        public AssemblyDefinition InjectILCode(AssemblyDefinition assemblyDefinition)
        {
            List<string> existingAttributes = ReflectionUtil.GetAllAttributesAssignableFrom(assemblyDefinition, typeof(FieldSetter));

            foreach (TypeDefinition? type in assemblyDefinition.MainModule.Types)
            {
                foreach (MemberReference? member in type.Fields
                    .OfType<MemberReference>()
                    .Concat(type.Properties.Cast<MemberReference>())
                    .Where(x =>
                    x is ICustomAttributeProvider castedX && castedX.HasCustomAttributes
                    ))
                {
                    foreach (CustomAttribute attribute in
                        (member as ICustomAttributeProvider)?.CustomAttributes?
                        .Where(attr => existingAttributes.Contains(attr.AttributeType.FullName))?
                        .ToList())
                    {
                        if (ReflectionUtil.IsBaseTypeRecursive(attribute.AttributeType.Resolve(), typeof(FieldSetter)))
                        {
                            assemblyDefinition = AddFieldInitCodeToStaticConstructor(type, assemblyDefinition, member, attribute);
                        }
                        _ = (member as ICustomAttributeProvider).CustomAttributes.Remove(attribute);
                    }
                }
            }
            return assemblyDefinition;
        }

        public AssemblyDefinition AddFieldInitCodeToStaticConstructor(
            TypeDefinition type,
            AssemblyDefinition assembly,
            MemberReference fieldOrProp,
            CustomAttribute attribute)
        {
            TypeReference attributeType = attribute.AttributeType;
            MethodReference constructor = attribute.Constructor;

            #region Get or create static constructor
            MethodDefinition? staticConstructor = type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            IList<Instruction> originalInstructions = [];
            if (staticConstructor == null)
            {
                MethodAttributes staticConstructorAttributes = MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                staticConstructor = new MethodDefinition(".cctor", staticConstructorAttributes, type.Module.TypeSystem.Void);
                type.Methods.Add(staticConstructor);
                staticConstructor.Body = new MethodBody(staticConstructor);
            }
            else
            {
                originalInstructions = staticConstructor.Body.Instructions.ToList();
            }
            staticConstructor.Body.Instructions.Clear();
            ILProcessor ilProcessor = staticConstructor.Body.GetILProcessor();
            #endregion

            #region method parameters are pushed onto the stack so for constructor parameters with primitives types this happens here
            foreach (CustomAttributeArgument arg in attribute.ConstructorArguments)
            {
                PushArgumentOntoStack(assembly, ilProcessor, arg);
            }
            #endregion

            #region attribute constructor call

            MethodReference ctorReference = assembly.MainModule.ImportReference(constructor);
            ilProcessor.Append(ilProcessor.Create(OpCodes.Newobj, ctorReference));

            #endregion

            #region GetValue of Attribute_Collection call gets added

            MethodDefinition? getValueMethod = attributeType.Resolve().Properties.FirstOrDefault(p => p.Name == "GetValue")?.GetMethod;
            MethodReference getValueMethodReference = assembly.MainModule.ImportReference(getValueMethod);
            ilProcessor.Append(ilProcessor.Create(OpCodes.Callvirt, getValueMethodReference));
            if (fieldOrProp is FieldReference field)
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Stsfld, field));
            }
            else if (fieldOrProp is PropertyReference property && property.Resolve() is PropertyDefinition propertyDef)
            {
                MethodReference setter = propertyDef.SetMethod;
                if (setter == null)
                {
                    throw new Exception("You applied the FieldSetter to a property without a setter :/");
                }
                ilProcessor.Append(ilProcessor.Create(OpCodes.Call, setter));
            }
            else
            {
                throw new Exception("AddFieldInitCode was called with an invalid MemberReference");
            }
            #endregion

            #region ReAdd Original Opcodes or Return

            foreach (Instruction? item in originalInstructions.ToList())
            {
                ilProcessor.Append(item);
            }
            #endregion

            if (ilProcessor.Body.Instructions.Last().OpCode != OpCodes.Ret)
            {
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));
            }
            return assembly;
        }

        private void PushArgumentOntoStack(
            AssemblyDefinition assembly,
            ILProcessor ilProcessor,
            CustomAttributeArgument arg)
        {
            TypeDefinition type = arg.Type.Resolve();
            object value = arg.Value;

            Type valueType = value.GetType();

            if (valueType == typeof(string))
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldstr, (string)value));
            }
            else if (valueType == typeof(int))
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I4, (int)value));
            }
            else if (valueType == typeof(long))
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I8, (long)value));
            }
            else if (valueType == typeof(bool))
            {
                ilProcessor.Append(ilProcessor.Create((bool)value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            }
            else if (valueType == typeof(double))
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_R8, (double)value));
            }
            else if (value is TypeReference typeReference)
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldtoken, typeReference));

                MethodReference getTypeFromHandle = assembly.MainModule.ImportReference(
                    typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) })
                );
                ilProcessor.Append(ilProcessor.Create(OpCodes.Call, getTypeFromHandle));
            }
            else if (value == null)
            {
                // Handle null reference types
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldnull));
            }
            else if (type.MetadataType == MetadataType.ValueType && type.Resolve().IsEnum)
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I4, (int)value));
            }
            else
            {
                throw new NotSupportedException($"Unsupported Constructor Type in Attribute for Field Setter only String, Int32, Int64, Boolean, Double, and Enum are supported");
            }
        }
    }
}
